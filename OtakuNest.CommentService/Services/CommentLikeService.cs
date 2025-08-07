using Microsoft.EntityFrameworkCore;
using OtakuNest.CommentService.Data;
using OtakuNest.CommentService.DTOs;
using OtakuNest.CommentService.Models;

namespace OtakuNest.CommentService.Services
{
    public class CommentLikeService : ICommentLikeService
    {
        private readonly CommentDbContext _context;

        public CommentLikeService(CommentDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetLikesCountAsync(Guid commentId, CancellationToken cancellationToken = default)
        {
            return await _context.CommentLikes
                .Where(l => l.CommentId == commentId)
                .CountAsync(cancellationToken);
        }

        public async Task<bool> HasUserLikedAsync(LikeCommentDto dto, CancellationToken cancellationToken = default)
        {
            return await _context.CommentLikes
                .AnyAsync(l => l.CommentId == dto.CommentId && l.UserId == dto.UserId, cancellationToken);
        }

        public async Task<bool> AddLikeAsync(LikeCommentDto dto, CancellationToken cancellationToken = default)
        {
            var alreadyLiked = await _context.CommentLikes
                .AnyAsync(l => l.CommentId == dto.CommentId && l.UserId == dto.UserId, cancellationToken);

            if (alreadyLiked)
                return false;

            var commentExists = await _context.Comments
                .AnyAsync(c => c.Id == dto.CommentId, cancellationToken);

            if (!commentExists)
                return false;

            var like = new CommentLike
            {
                CommentId = dto.CommentId,
                UserId = dto.UserId
            };

            _context.CommentLikes.Add(like);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> RemoveLikeAsync(LikeCommentDto dto, CancellationToken cancellationToken = default)
        {
            var like = await _context.CommentLikes
                .FirstOrDefaultAsync(l => l.CommentId == dto.CommentId && l.UserId == dto.UserId, cancellationToken);

            if (like == null)
                return false;

            _context.CommentLikes.Remove(like);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
