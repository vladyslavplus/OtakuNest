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

        public async Task<PagedList<CommentDto>> GetAllAsync(
            CommentParameters parameters,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Comments
                .AsQueryable();

            if (parameters.ProductId != Guid.Empty)
                query = query.Where(c => c.ProductId == parameters.ProductId);

            if (parameters.ParentCommentId.HasValue)
                query = query.Where(c => c.ParentCommentId == parameters.ParentCommentId.Value);
            else
                query = query.Where(c => c.ParentCommentId == null);

            if (!string.IsNullOrWhiteSpace(parameters.Content))
            {
                var content = parameters.Content.Trim();
                query = query.Where(c => c.Content.Contains(content));
            }

            query = _sortHelper.ApplySort(query, parameters.OrderBy);

            var projected = query.Select(c => new CommentDto
            {
                Id = c.Id,
                ProductId = c.ProductId,
                UserId = c.UserId,
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                ParentCommentId = c.ParentCommentId,
                LikesCount = c.Likes.Count,
                Replies = c.Replies.Select(r => new ReplyDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    Content = r.Content,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    LikesCount = r.Likes.Count
                }).ToList()
            });

            return await PagedList<CommentDto>.ToPagedListAsync(
                projected.AsNoTracking(),
                parameters.PageNumber,
                parameters.PageSize,
                cancellationToken
            );
        }

        public async Task<CommentDto?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken = default)
        {
            var comment = await _context.Comments
                .Include(c => c.Replies)
                .Include(c => c.Likes)
                .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);

            return comment is null ? null : ToCommentDto(comment);
        }

        public async Task<CommentDto> CreateAsync(CreateCommentDto dto, Guid userId, CancellationToken cancellationToken = default)
        {
            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = dto.ProductId,
                UserId = userId,                
                Content = dto.Content,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync(cancellationToken);

            return ToCommentDto(comment);
        }

        public async Task<CommentDto> ReplyAsync(ReplyToCommentDto dto, Guid userId, CancellationToken cancellationToken = default)
        {
            var parent = await _context.Comments
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.ParentCommentId, cancellationToken);

            if (parent == null)
                throw new KeyNotFoundException("Parent comment not found");

            var reply = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = dto.ProductId,
                ParentCommentId = dto.ParentCommentId,
                UserId = userId,
                Content = dto.Content,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(reply);
            await _context.SaveChangesAsync(cancellationToken);

            return ToCommentDto(reply);
        }

        public async Task<bool> UpdateAsync(Guid commentId, Guid userId, UpdateCommentDto dto, CancellationToken cancellationToken = default)
        {
            var comment = await _context.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.UserId == userId, cancellationToken);

            if (comment == null) return false;

            comment.Content = dto.Content;
            comment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default)
        {
            var comment = await _context.Comments
                .Include(c => c.Replies)
                .FirstOrDefaultAsync(c => c.Id == commentId && c.UserId == userId, cancellationToken);

            if (comment == null) return false;

            if (comment.Replies?.Any() == true)
                _context.Comments.RemoveRange(comment.Replies);

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        private static CommentDto ToCommentDto(Comment comment)
        {
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
                Replies = comment.Replies?.Select(r => new ReplyDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    Content = r.Content,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    LikesCount = r.Likes?.Count ?? 0
                }).ToList() ?? new()
            };
        }
    }
}
