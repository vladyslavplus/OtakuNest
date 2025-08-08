using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OtakuNest.CommentService.DTOs;
using OtakuNest.CommentService.Services;
using OtakuNest.Common.Extensions;

namespace OtakuNest.CommentService.Controllers
{
    [ApiController]
    [Route("api/comments/{commentId:guid}/likes")]
    public class CommentLikesController : ControllerBase
    {
        private readonly ICommentLikeService _likeService;

        public CommentLikesController(ICommentLikeService likeService)
        {
            _likeService = likeService;
        }

        [HttpGet("count")]
        public async Task<ActionResult<int>> GetLikesCount(Guid commentId, CancellationToken cancellationToken)
        {
            var count = await _likeService.GetLikesCountAsync(commentId, cancellationToken);
            return Ok(count);
        }

        [Authorize]
        [HttpGet("user")]
        public async Task<ActionResult<bool>> HasUserLiked(Guid commentId, CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            var hasLiked = await _likeService.HasUserLikedAsync(new LikeCommentDto
            {
                CommentId = commentId,
                UserId = userId
            }, cancellationToken);

            return Ok(hasLiked);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddLike(Guid commentId, CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();

            var success = await _likeService.AddLikeAsync(new LikeCommentDto
            {
                CommentId = commentId,
                UserId = userId
            }, cancellationToken);

            return success ? Ok() : BadRequest("You have already liked this comment or comment does not exist.");
        }

        [Authorize]
        [HttpDelete]
        public async Task<IActionResult> RemoveLike(Guid commentId, CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            var success = await _likeService.RemoveLikeAsync(new LikeCommentDto
            {
                CommentId = commentId,
                UserId = userId
            }, cancellationToken);

            return success ? NoContent() : NotFound("Like not found.");
        }
    }
}
