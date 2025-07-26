using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OtakuNest.Common.Helpers;
using OtakuNest.UserService.DTOs;
using OtakuNest.UserService.Models;
using OtakuNest.UserService.Parameters;
using OtakuNest.UserService.Services;

namespace OtakuNest.UserService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedList<ApplicationUser>>> GetUsers([FromQuery] UserParameters parameters, CancellationToken cancellationToken)
        {
            var users = await _userService.GetUsersAsync(parameters, cancellationToken);
            return Ok(users);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApplicationUser>> GetUserById(Guid id, CancellationToken cancellationToken)
        {
            var user = await _userService.GetUserByIdAsync(id, cancellationToken);

            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserDto dto, CancellationToken cancellationToken)
        {
            var success = await _userService.UpdateUserAsync(id, dto, cancellationToken);
            if (!success)
                return BadRequest("Failed to update user");

            return NoContent();
        }
    }
}
