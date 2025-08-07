using OtakuNest.CommentService.DTOs;

namespace OtakuNest.CommentService.Services
{
    public interface ICommentLikeService
    {
        Task<int> GetLikesCountAsync(Guid commentId, CancellationToken cancellationToken = default);
        Task<bool> HasUserLikedAsync(LikeCommentDto dto, CancellationToken cancellationToken = default);
        Task<bool> AddLikeAsync(LikeCommentDto dto, CancellationToken cancellationToken = default);
        Task<bool> RemoveLikeAsync(LikeCommentDto dto, CancellationToken cancellationToken = default);
    }
}
