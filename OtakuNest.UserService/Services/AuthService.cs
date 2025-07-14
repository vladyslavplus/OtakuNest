using MassTransit;
using Microsoft.AspNetCore.Identity;
using OtakuNest.Contracts;
using OtakuNest.UserService.DTOs;
using OtakuNest.UserService.Exceptions;
using OtakuNest.UserService.Models;

namespace OtakuNest.UserService.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IPublishEndpoint _publishEndpoint;

        public AuthService(UserManager<ApplicationUser> userManager, ITokenService tokenService, IPublishEndpoint publishEndpoint)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _publishEndpoint = publishEndpoint;
        }

        public async Task<string> RegisterAsync(UserRegisterDto dto, CancellationToken cancellationToken)
        {
            var userExists = await _userManager.FindByNameAsync(dto.UserName);
            if (userExists != null)
                throw new InvalidOperationException("User already exists.");

            var user = new ApplicationUser
            {
                UserName = dto.UserName,
                Email = dto.Email,
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new UserCreationException($"User creation failed: {errors}");
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "User");
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                throw new RoleAssignmentException($"Adding role failed: {errors}");
            }

            var userCreatedEvent = new UserCreatedEvent(
                user.Id,
                user.UserName!,
                user.Email!,
                DateTime.UtcNow);

            await _publishEndpoint.Publish(userCreatedEvent, cancellationToken);

            return await _tokenService.GenerateTokenAsync(user);
        }

        public async Task<string> LoginAsync(UserLoginDto dto, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                throw new UnauthorizedAccessException("User not found.");

            var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!passwordValid)
                throw new UnauthorizedAccessException("Invalid password.");

            return await _tokenService.GenerateTokenAsync(user);
        }
    }
}
