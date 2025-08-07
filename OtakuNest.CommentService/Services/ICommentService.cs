using OtakuNest.CommentService.DTOs;
using OtakuNest.CommentService.Parameters;
using OtakuNest.Common.Helpers;

namespace OtakuNest.CommentService.Services
{
    public interface ICommentService
    {
        Task<PagedList<CommentDto>> GetAllAsync(CommentParameters parameters, CancellationToken cancellationToken = default);
        Task<CommentDto?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken = default);
        Task<CommentDto> CreateAsync(CreateCommentDto dto, Guid userId, CancellationToken cancellationToken = default);
        Task<CommentDto> ReplyAsync(ReplyToCommentDto dto, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(Guid commentId, Guid userId, UpdateCommentDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default);
    }
}
