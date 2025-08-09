using Microsoft.EntityFrameworkCore;
using OtakuNest.CommentService.Data;
using OtakuNest.CommentService.DTOs;
using OtakuNest.CommentService.Models;
using OtakuNest.CommentService.Parameters;
using OtakuNest.Common.Helpers;
using OtakuNest.Common.Interfaces;

namespace OtakuNest.CommentService.Services
{
    public class CommentService : ICommentService
    {
        private readonly CommentDbContext _context;
        private readonly ISortHelper<Comment> _sortHelper;

        public CommentService(CommentDbContext context, ISortHelper<Comment> sortHelper)
        {
            _context = context;
            _sortHelper = sortHelper;
        }

        public async Task<PagedList<CommentDto>> GetAllAsync(CommentParameters parameters, CancellationToken cancellationToken = default)
        {
            if (parameters.ProductId == Guid.Empty)
                throw new ArgumentException("ProductId must be provided");

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

            var dtos = rootComments.Select(c => MapCommentToDto(c, allComments)).ToList();

            return new PagedList<CommentDto>(
                dtos,
                totalRootCount,
                parameters.PageNumber,
                parameters.PageSize);
        }

        public async Task<CommentDto?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken = default)
        {
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

            return MapCommentToDto(comment, allComments);
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

            return new CommentDto
            {
                Id = comment.Id,
                ProductId = comment.ProductId,
                UserId = comment.UserId,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                LikesCount = 0,
                Replies = new List<ReplyDto>()
            };
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

            return MapCommentToDto(reply, allComments);
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

        private ReplyDto MapCommentToReplyDto(Comment comment, List<Comment> allComments)
        {
            var children = allComments
                .Where(c => c.ParentCommentId == comment.Id)
                .OrderBy(c => c.CreatedAt)
                .ToList();

            return new ReplyDto
            {
                Id = comment.Id,
                UserId = comment.UserId,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                LikesCount = comment.Likes?.Count ?? 0,
                Replies = children.Select(c => MapCommentToReplyDto(c, allComments)).ToList()
            };
        }

        private CommentDto MapCommentToDto(Comment comment, List<Comment> allComments)
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
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                ParentCommentId = comment.ParentCommentId,
                LikesCount = comment.Likes?.Count ?? 0,
                Replies = directReplies.Select(c => MapCommentToReplyDto(c, allComments)).ToList()
            };
        }
    }
}