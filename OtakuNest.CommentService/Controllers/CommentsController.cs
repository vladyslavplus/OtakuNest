using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OtakuNest.CommentService.DTOs;
using OtakuNest.CommentService.Parameters;
using OtakuNest.CommentService.Services;
using OtakuNest.Common.Extensions;
using OtakuNest.Common.Helpers;

namespace OtakuNest.CommentService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentService _commentService;

        public CommentsController(ICommentService commentService)
        {
            _commentService = commentService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedList<CommentDto>>> GetAll([FromQuery] CommentParameters parameters, CancellationToken cancellationToken)
        {
            var comments = await _commentService.GetAllAsync(parameters, cancellationToken);
            return Ok(comments);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<CommentDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var comment = await _commentService.GetByIdAsync(id, cancellationToken);
            if (comment == null)
                return NotFound();

            return Ok(comment);
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<CommentDto>> Create([FromBody] CreateCommentDto dto, CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            var created = await _commentService.CreateAsync(dto, userId, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [Authorize]
        [HttpPost("reply")]
        public async Task<ActionResult<CommentDto>> Reply([FromBody] ReplyToCommentDto dto, CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            var reply = await _commentService.ReplyAsync(dto, userId, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = reply.Id }, reply);
        }

        [Authorize]
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCommentDto dto, CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            var updated = await _commentService.UpdateAsync(id, userId, dto, cancellationToken);
            if (!updated)
                return NotFound();

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            var deleted = await _commentService.DeleteAsync(id, userId, cancellationToken);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}
