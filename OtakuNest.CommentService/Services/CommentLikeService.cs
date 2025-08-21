using Microsoft.EntityFrameworkCore;
using OtakuNest.CommentService.Data;
using OtakuNest.CommentService.DTOs;
using OtakuNest.CommentService.Models;
using OtakuNest.Common.Services.Caching;

namespace OtakuNest.CommentService.Services
{
    public class CommentLikeService : ICommentLikeService
    {
        private const string CommentListKeysSet = "comments:list:keys"; 
        private readonly CommentDbContext _context;
        private readonly IRedisCacheService _cacheService;

        public CommentLikeService(
            CommentDbContext context,
            IRedisCacheService cacheService)
        {
            _context = context;
            _cacheService = cacheService;
        }

        public async Task<int> GetLikesCountAsync(Guid commentId, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"comment:likes:count:{commentId}";

            var cachedCount = await _cacheService.GetDataAsync<int?>(cacheKey);
            if (cachedCount.HasValue)
                return cachedCount.Value;

            var count = await _context.CommentLikes
                .Where(l => l.CommentId == commentId)
                .CountAsync(cancellationToken);

            await _cacheService.SetDataAsync(cacheKey, count, TimeSpan.FromMinutes(15));
            return count;
        }

        public async Task<bool> HasUserLikedAsync(LikeCommentDto dto, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"comment:like:user:{dto.CommentId}:{dto.UserId}";

            var cachedResult = await _cacheService.GetDataAsync<bool?>(cacheKey);
            if (cachedResult.HasValue)
                return cachedResult.Value;

            var hasLiked = await _context.CommentLikes
                .AnyAsync(l => l.CommentId == dto.CommentId && l.UserId == dto.UserId, cancellationToken);

            await _cacheService.SetDataAsync(cacheKey, hasLiked, TimeSpan.FromMinutes(10));
            return hasLiked;
        }

        public async Task<bool> AddLikeAsync(LikeCommentDto dto, CancellationToken cancellationToken = default)
        {
            var alreadyLiked = await _context.CommentLikes
                .AnyAsync(l => l.CommentId == dto.CommentId && l.UserId == dto.UserId, cancellationToken);

            if (alreadyLiked)
                return false;

            var comment = await _context.Comments
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.CommentId, cancellationToken);

            if (comment == null)
                return false;

            var like = new CommentLike
            {
                CommentId = dto.CommentId,
                UserId = dto.UserId
            };

            _context.CommentLikes.Add(like);
            await _context.SaveChangesAsync(cancellationToken);

            await InvalidateLikeCacheAsync(dto.CommentId, dto.UserId);
            await InvalidateCommentCacheAsync(dto.CommentId);

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

            await InvalidateLikeCacheAsync(dto.CommentId, dto.UserId);
            await InvalidateCommentCacheAsync(dto.CommentId);

            return true;
        }

        private async Task InvalidateLikeCacheAsync(Guid commentId, Guid userId)
        {
            await _cacheService.RemoveDataAsync($"comment:likes:count:{commentId}");
            await _cacheService.RemoveDataAsync($"comment:like:user:{commentId}:{userId}");
        }

        private async Task InvalidateCommentCacheAsync(Guid commentId)
        {
            await _cacheService.RemoveDataAsync($"comment:{commentId}");

            var comment = await _context.Comments
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment?.ParentCommentId != null)
            {
                await _cacheService.RemoveDataAsync($"comment:{comment.ParentCommentId}");
            }

            await InvalidateCommentListCacheAsync();
        }

        private async Task InvalidateCommentListCacheAsync()
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