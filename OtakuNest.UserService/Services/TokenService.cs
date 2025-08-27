using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OtakuNest.UserService.Data;
using OtakuNest.UserService.Models;

namespace OtakuNest.UserService.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserDbContext _context;

        public TokenService(IConfiguration configuration, UserManager<ApplicationUser> userManager, UserDbContext context)
        {
            _configuration = configuration;
            _userManager = userManager;
            _context = context;
        }

        public async Task<string> GenerateTokenAsync(ApplicationUser user)
        {
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var role in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            var authSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                expires: DateTime.UtcNow.AddHours(3),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public Task<string> GenerateRefreshTokenAsync()
        {
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public async Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(ApplicationUser user)
        {
            var accessToken = await GenerateTokenAsync(user);
            var refreshToken = await GenerateRefreshTokenAsync();

            var expires = DateTime.UtcNow.AddDays(7);

            await SaveRefreshTokenAsync(user.Id, refreshToken, expires);

            return (accessToken, refreshToken);
        }

        public async Task SaveRefreshTokenAsync(Guid userId, string refreshToken, DateTime expires)
        {
            var tokenEntity = new RefreshToken
            {
                Token = refreshToken,
                UserId = userId,
                Expires = expires,
                Created = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(tokenEntity);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ValidateRefreshTokenAsync(string refreshToken, Guid userId)
        {
            var token = await _context.RefreshTokens
                .Where(t => t.Token == refreshToken && t.UserId == userId && t.Revoked == null && t.Expires > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            return token != null;
        }

        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            var token = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
            if (token != null && token.Revoked == null)
            {
                token.Revoked = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<RefreshToken?> GetRefreshTokenEntityAsync(string refreshToken)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(t => t.Token == refreshToken);
        }

        public async Task RevokeAllRefreshTokensForUserAsync(Guid userId)
        {
            var tokens = await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.Revoked == null && t.Expires > DateTime.UtcNow)
                .ToListAsync();

            foreach (var token in tokens)
            {
                token.Revoked = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }
}
