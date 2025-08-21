using MassTransit;
using Microsoft.EntityFrameworkCore;
using OtakuNest.CommentService.Data;
using OtakuNest.CommentService.DTOs;
using OtakuNest.CommentService.Models;
using OtakuNest.CommentService.Parameters;
using OtakuNest.Common.Helpers;
using OtakuNest.Common.Interfaces;
using OtakuNest.Common.Services.Caching;
using OtakuNest.Contracts;

namespace OtakuNest.CommentService.Services
{
    public class CommentService : ICommentService
    {
        private const string CommentListKeysSet = "comments:list:keys";
        private readonly CommentDbContext _context;
        private readonly ISortHelper<Comment> _sortHelper;
        private readonly IRequestClient<GetUsersByIdsRequest> _userClient;
        private readonly IRedisCacheService _cacheService;

        public CommentService(
            CommentDbContext context,
            ISortHelper<Comment> sortHelper,
            IRequestClient<GetUsersByIdsRequest> userClient,
            IRedisCacheService cacheService)
        {
            _context = context;
            _sortHelper = sortHelper;
            _userClient = userClient;
            _cacheService = cacheService;
        }

        public async Task<PagedList<CommentDto>> GetAllAsync(CommentParameters parameters, CancellationToken cancellationToken = default)
        {
            if (parameters.ProductId == Guid.Empty)
                throw new ArgumentException("ProductId must be provided");

            var cacheKey = GenerateListCacheKey(parameters);

            var cachedDto = await _cacheService.GetDataAsync<PagedListCacheDto<CommentDto>>(cacheKey);
            if (cachedDto != null)
            {
                return new PagedList<CommentDto>(
                    cachedDto.Items,
                    cachedDto.TotalCount,
                    cachedDto.PageNumber,
                    cachedDto.PageSize
                );
            }

            var rootCommentsQuery = _context.Comments
                .Where(c => c.ProductId == parameters.ProductId && c.ParentCommentId == null)
                .Include(c => c.Likes)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(parameters.Content))
            {
                var content = parameters.Content.Trim();
                rootCommentsQuery = rootCommentsQuery.Where(c => c.Content.Contains(content));
            }

            rootCommentsQuery = _sortHelper.ApplySort(rootCommentsQuery, parameters.OrderBy);

            var totalRootCount = await rootCommentsQuery.CountAsync(cancellationToken);

            var rootComments = await rootCommentsQuery
                .Skip((parameters.PageNumber - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync(cancellationToken);

            var allComments = await _context.Comments
                .Where(c => c.ProductId == parameters.ProductId)
                .Include(c => c.Likes)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var userMap = await GetUserMapAsync(allComments, cancellationToken);
            var dtos = rootComments.Select(c => MapCommentToDto(c, allComments, userMap)).ToList();

            var cacheDto = new PagedListCacheDto<CommentDto>
            {
                Items = dtos,
                TotalCount = totalRootCount,
                PageNumber = parameters.PageNumber,
                PageSize = parameters.PageSize
            };

            await _cacheService.SetDataAsync(cacheKey, cacheDto, TimeSpan.FromMinutes(3));
            await _cacheService.AddToSetAsync(CommentListKeysSet, cacheKey);

            return new PagedList<CommentDto>(
                dtos, 
                totalRootCount, 
                parameters.PageNumber, 
                parameters.PageSize);
        }

        public async Task<CommentDto?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"comment:{commentId}";

            var cachedComment = await _cacheService.GetDataAsync<CommentDto>(cacheKey);
            if (cachedComment != null)
                return cachedComment;

            var comment = await _context.Comments
                .Include(c => c.Likes)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);

            if (comment == null)
                return null;

            var allComments = await _context.Comments
                .Where(c => c.ProductId == comment.ProductId)
                .Include(c => c.Likes)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var userMap = await GetUserMapAsync(allComments, cancellationToken);
            var dto = MapCommentToDto(comment, allComments, userMap);

            await _cacheService.SetDataAsync(cacheKey, dto, TimeSpan.FromMinutes(30));
            return dto;
        }

        public async Task<CommentDto> CreateAsync(CreateCommentDto dto, Guid userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dto.Content))
                throw new ArgumentException("Comment content cannot be empty");

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = dto.ProductId,
                UserId = userId,
                Content = dto.Content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync(cancellationToken);

            var allComments = await _context.Comments
                .Where(c => c.ProductId == dto.ProductId)
                .Include(c => c.Likes)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var userMap = await GetUserMapAsync(allComments, cancellationToken);

            var commentDto = MapCommentToDto(comment, allComments, userMap);

            await _cacheService.SetDataAsync($"comment:{comment.Id}", commentDto, TimeSpan.FromMinutes(30));
            await InvalidateListCacheAsync();

            return commentDto;
        }

        public async Task<CommentDto> ReplyAsync(ReplyToCommentDto dto, Guid userId, CancellationToken cancellationToken = default)
        {
            var parent = await _context.Comments
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.ParentCommentId && c.ProductId == dto.ProductId, cancellationToken);

            if (parent == null)
                throw new KeyNotFoundException("Parent comment not found");

            if (string.IsNullOrWhiteSpace(dto.Content))
                throw new ArgumentException("Reply content cannot be empty");

            var reply = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = dto.ProductId,
                ParentCommentId = dto.ParentCommentId,
                UserId = userId,
                Content = dto.Content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(reply);
            await _context.SaveChangesAsync(cancellationToken);

            var allComments = await _context.Comments
                .Where(c => c.ProductId == dto.ProductId)
                .Include(c => c.Likes)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var userMap = await GetUserMapAsync(allComments, cancellationToken);
            var replyDto = MapCommentToDto(reply, allComments, userMap);

            await _cacheService.SetDataAsync($"comment:{reply.Id}", replyDto, TimeSpan.FromMinutes(30));
            await InvalidateListCacheAsync();

            return replyDto;
        }

        public async Task<bool> UpdateAsync(Guid commentId, Guid userId, UpdateCommentDto dto, CancellationToken cancellationToken = default)
        {
            var comment = await _context.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.UserId == userId, cancellationToken);

            if (comment == null)
                return false;

            if (string.IsNullOrWhiteSpace(dto.Content))
                throw new ArgumentException("Comment content cannot be empty");

            comment.Content = dto.Content.Trim();
            comment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            var allComments = await _context.Comments
                .Where(c => c.ProductId == comment.ProductId)
                .Include(c => c.Likes)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var userMap = await GetUserMapAsync(allComments, cancellationToken);
            var updatedDto = MapCommentToDto(comment, allComments, userMap);

            await _cacheService.SetDataAsync($"comment:{comment.Id}", updatedDto, TimeSpan.FromMinutes(30));
            await InvalidateListCacheAsync();

            return true;
        }

        public async Task<bool> DeleteAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default)
        {
            var comment = await _context.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.UserId == userId, cancellationToken);

            if (comment == null)
                return false;

            var allComments = await _context.Comments
                .Where(c => c.ProductId == comment.ProductId)
                .ToListAsync(cancellationToken);

            var toDelete = GetChildrenRecursive(allComments, commentId);
            toDelete.Add(comment);

            _context.Comments.RemoveRange(toDelete);
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var c in toDelete)
                await _cacheService.RemoveDataAsync($"comment:{c.Id}");

            var parentIds = toDelete
                .Where(c => c.ParentCommentId != null)
                .Select(c => c.ParentCommentId!.Value)
                .Distinct();

            foreach (var parentId in parentIds)
                await _cacheService.RemoveDataAsync($"comment:{parentId}");

            await InvalidateListCacheAsync();

            return true;
        }

        private static List<Comment> GetChildrenRecursive(List<Comment> allComments, Guid parentId)
        {
            var children = allComments
                .Where(c => c.ParentCommentId == parentId)
                .ToList();

            var result = new List<Comment>(children);

            foreach (var child in children)
                result.AddRange(GetChildrenRecursive(allComments, child.Id));

            return result;
        }

        private ReplyDto MapCommentToReplyDto(Comment comment, List<Comment> allComments, Dictionary<Guid, string> userMap)
        {
            var children = allComments
                .Where(c => c.ParentCommentId == comment.Id)
                .OrderBy(c => c.CreatedAt)
                .ToList();

            return new ReplyDto
            {
                Id = comment.Id,
                UserId = comment.UserId,
                UserName = userMap.TryGetValue(comment.UserId, out var name) ? name : "Unknown",
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                LikesCount = comment.Likes?.Count ?? 0,
                Replies = children.Select(c => MapCommentToReplyDto(c, allComments, userMap)).ToList()
            };
        }

        private CommentDto MapCommentToDto(Comment comment, List<Comment> allComments, Dictionary<Guid, string> userMap)
        {
            var directReplies = allComments
                .Where(c => c.ParentCommentId == comment.Id)
                .OrderBy(c => c.CreatedAt)
                .ToList();

            return new CommentDto
            {
                Id = comment.Id,
                ProductId = comment.ProductId,
                UserId = comment.UserId,
                UserName = userMap.TryGetValue(comment.UserId, out var name) ? name : "Unknown",
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                ParentCommentId = comment.ParentCommentId,
                LikesCount = comment.Likes?.Count ?? 0,
                Replies = directReplies.Select(c => MapCommentToReplyDto(c, allComments, userMap)).ToList()
            };
        }

        private async Task<Dictionary<Guid, string>> GetUserMapAsync(IEnumerable<Comment> comments, CancellationToken cancellationToken)
        {
            var userIds = comments.Select(c => c.UserId).Distinct().ToList();
            if (!userIds.Any())
                return new Dictionary<Guid, string>();

            var response = await _userClient.GetResponse<GetUsersByIdsResponse>(
                new GetUsersByIdsRequest(userIds), cancellationToken);

            return response.Message.Users.ToDictionary(u => u.Id, u => u.UserName);
        }

        private static string GenerateListCacheKey(CommentParameters parameters)
        {
            return $"comments:product:{parameters.ProductId}"
                   + $":page:{parameters.PageNumber}:size:{parameters.PageSize}"
                   + $":order:{parameters.OrderBy ?? "default"}"
                   + $":content:{parameters.Content ?? ""}";
        }

        private async Task InvalidateListCacheAsync()
        {
            var keys = await _cacheService.GetSetMembersAsync(CommentListKeysSet);
            foreach (var key in keys)
            {
                await _cacheService.RemoveDataAsync(key);
            }
            await _cacheService.ClearSetAsync(CommentListKeysSet);
        }
    }
}