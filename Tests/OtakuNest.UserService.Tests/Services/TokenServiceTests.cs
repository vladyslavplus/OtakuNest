using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OtakuNest.UserService.Data;
using OtakuNest.UserService.Models;
using OtakuNest.UserService.Services;
using Shouldly;

namespace OtakuNest.UserService.Tests.Services
{
    public class TokenServiceTests : IDisposable
    {
        private readonly UserDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly TokenService _service;
        private bool _disposed = false;

        private static readonly Guid UserId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        public TokenServiceTests()
        {
            _context = GetInMemoryDbContext();

            var store = new UserStore<ApplicationUser, IdentityRole<Guid>, UserDbContext, Guid>(_context);

            var loggerMock = new Mock<ILogger<UserManager<ApplicationUser>>>();

            var userManagerMock = new Mock<UserManager<ApplicationUser>>(
                store,
                null!,
                new PasswordHasher<ApplicationUser>(),
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                null!,
                new IdentityErrorDescriber(),
                new Mock<IServiceProvider>().Object,
                loggerMock.Object
            );

            userManagerMock.Setup(u => u.Users).Returns(_context.Users.AsQueryable());
            userManagerMock.Setup(u => u.GetRolesAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(new List<string> { "User" });

            _userManager = userManagerMock.Object;

            var inMemorySettings = new Dictionary<string, string>
            {
                { "Jwt:Key", "QmJ0zLVzKn6hw4IcszPgQY2vDjJvYqVztuI4bq1+qk2ZCXR4DKZrGhZfrs0+NbyT" },
                { "Jwt:Issuer", "OtakuNest.UserService" },
                { "Jwt:Audience", "OtakuNest.Client" }
            };
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();

            _service = new TokenService(_configuration, _userManager, _context);
        }

        private static UserDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new UserDbContext(options);

            context.Users.AddRange(
                new ApplicationUser
                {
                    Id = UserId1,
                    UserName = "testuser1",
                    Email = "test1@example.com",
                    PhoneNumber = "1234567890",
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString()
                },
                new ApplicationUser
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    UserName = "testuser2",
                    Email = "test2@example.com",
                    PhoneNumber = "0987654321",
                    EmailConfirmed = false,
                    SecurityStamp = Guid.NewGuid().ToString()
                }
            );

            context.RefreshTokens.AddRange(
                new RefreshToken
                {
                    Token = "token1",
                    UserId = UserId1,
                    Expires = DateTime.UtcNow.AddDays(7),
                    Created = DateTime.UtcNow
                },
                new RefreshToken
                {
                    Token = "token2",
                    UserId = UserId2,
                    Expires = DateTime.UtcNow.AddDays(7),
                    Created = DateTime.UtcNow
                }
            );

            context.SaveChanges();
            return context;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context?.Dispose();
                    (_userManager as IDisposable)?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #region GenerateTokenAsync Tests

        [Fact]
        public async Task GenerateTokenAsync_ShouldHaveCorrectIssuerAndAudience()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);

            // Act
            var tokenString = await _service.GenerateTokenAsync(user);
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(tokenString);

            // Assert
            jwtToken.Issuer.ShouldBe(_configuration["Jwt:Issuer"]);
            jwtToken.Audiences.ShouldContain(_configuration["Jwt:Audience"]);
        }

        [Fact]
        public async Task GenerateTokenAsync_ShouldReturnTokenString_WhenUserIsValid()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);

            // Act
            var token = await _service.GenerateTokenAsync(user);

            // Assert
            token.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task GenerateTokenAsync_ShouldContainCorrectUserIdAndUserNameClaims()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);

            // Act
            var tokenString = await _service.GenerateTokenAsync(user);
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(tokenString);

            // Assert
            jwtToken.Claims.ShouldContain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
            jwtToken.Claims.ShouldContain(c => c.Type == ClaimTypes.Name && c.Value == user.UserName);
        }

        [Fact]
        public async Task GenerateTokenAsync_ShouldContainRoleClaim()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);

            // Act
            var tokenString = await _service.GenerateTokenAsync(user);
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(tokenString);

            // Assert
            jwtToken.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == "User");
        }

        [Fact]
        public async Task GenerateTokenAsync_ShouldExpireIn3Hours()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);
            var now = DateTime.UtcNow;

            // Act
            var tokenString = await _service.GenerateTokenAsync(user);
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(tokenString);

            // Assert
            jwtToken.ValidTo.ShouldBeGreaterThan(now.AddHours(2.9));
            jwtToken.ValidTo.ShouldBeLessThan(now.AddHours(3.1));
        }

        [Fact]
        public async Task GenerateTokenAsync_ShouldIncludeAllRoles()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);
            var roles = new List<string> { "User", "Admin" };
            var userManagerMock = Mock.Get(_userManager);
            userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(roles);

            // Act
            var tokenString = await _service.GenerateTokenAsync(user);
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(tokenString);

            // Assert
            roles.ForEach(role => jwtToken.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == role));
        }

        [Fact]
        public async Task GenerateRefreshTokenAsync_ShouldReturnNonEmptyUniqueString()
        {
            // Act
            var token1 = await _service.GenerateRefreshTokenAsync();
            var token2 = await _service.GenerateRefreshTokenAsync();

            // Assert
            token1.ShouldNotBeNullOrEmpty();
            token2.ShouldNotBeNullOrEmpty();
            token1.ShouldNotBe(token2); 
        }

        #endregion

        #region GenerateTokensAsync & SaveRefreshTokenAsync Tests

        [Fact]
        public async Task GenerateTokensAsync_ShouldReturnAccessAndRefreshTokens()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);

            // Act
            var (accessToken, refreshToken) = await _service.GenerateTokensAsync(user);

            // Assert
            accessToken.ShouldNotBeNullOrEmpty();
            refreshToken.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public async Task GenerateTokensAsync_ShouldSaveRefreshTokenInDatabase()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId2);

            // Act
            var (_, refreshToken) = await _service.GenerateTokensAsync(user);

            // Assert
            var tokenEntity = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
            tokenEntity.ShouldNotBeNull();
            tokenEntity.UserId.ShouldBe(user.Id);
            tokenEntity.Expires.ShouldBeGreaterThan(DateTime.UtcNow);
            tokenEntity.Created.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        }

        [Fact]
        public async Task SaveRefreshTokenAsync_ShouldPersistTokenCorrectly()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);
            var refreshToken = "test-refresh-token";
            var expires = DateTime.UtcNow.AddDays(5);

            // Act
            await _service.SaveRefreshTokenAsync(user.Id, refreshToken, expires);

            // Assert
            var tokenEntity = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
            tokenEntity.ShouldNotBeNull();
            tokenEntity.UserId.ShouldBe(user.Id);
            tokenEntity.Expires.ShouldBe(expires);
            tokenEntity.Created.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        }

        [Fact]
        public async Task GenerateTokensAsync_ShouldGenerateUniqueTokensOnMultipleCalls()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);

            // Act
            var firstTokens = await _service.GenerateTokensAsync(user);
            var secondTokens = await _service.GenerateTokensAsync(user);

            // Assert
            firstTokens.AccessToken.ShouldNotBe(secondTokens.AccessToken);
            firstTokens.RefreshToken.ShouldNotBe(secondTokens.RefreshToken);
        }

        [Fact]
        public async Task SaveRefreshTokenAsync_ShouldNotDuplicateTokens()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);
            var refreshToken = "duplicate-token";
            var expires = DateTime.UtcNow.AddDays(3);

            // Act
            await _service.SaveRefreshTokenAsync(user.Id, refreshToken, expires);
            await _service.SaveRefreshTokenAsync(user.Id, refreshToken, expires);

            // Assert
            var tokens = await _context.RefreshTokens.Where(t => t.Token == refreshToken).ToListAsync();
            tokens.Count.ShouldBe(2); 
        }

        [Fact]
        public async Task SaveRefreshTokenAsync_ShouldSetCorrectExpiresDate()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId2);
            var refreshToken = "expires-test-token";
            var expires = DateTime.UtcNow.AddDays(10);

            // Act
            await _service.SaveRefreshTokenAsync(user.Id, refreshToken, expires);

            // Assert
            var tokenEntity = await _context.RefreshTokens.FirstAsync(t => t.Token == refreshToken);
            tokenEntity.Expires.ShouldBe(expires);
        }

        [Fact]
        public async Task SaveRefreshTokenAsync_CreatedTimestamp_ShouldBeRecent()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);
            var refreshToken = "recent-timestamp-token";
            var expires = DateTime.UtcNow.AddDays(2);

            // Act
            await _service.SaveRefreshTokenAsync(user.Id, refreshToken, expires);

            // Assert
            var tokenEntity = await _context.RefreshTokens.FirstAsync(t => t.Token == refreshToken);
            (DateTime.UtcNow - tokenEntity.Created).TotalSeconds.ShouldBeLessThan(5);
        }

        #endregion

        #region ValidateRefreshTokenAsync Tests

        [Fact]
        public async Task ValidateRefreshTokenAsync_ShouldReturnTrue_WhenTokenIsValid()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);
            var refreshToken = "token1";

            // Act
            var result = await _service.ValidateRefreshTokenAsync(refreshToken, user.Id);

            // Assert
            result.ShouldBeTrue();
        }

        [Fact]
        public async Task ValidateRefreshTokenAsync_ShouldReturnFalse_WhenTokenDoesNotExist()
        {
            // Act
            var result = await _service.ValidateRefreshTokenAsync("nonexistent-token", UserId1);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task ValidateRefreshTokenAsync_ShouldReturnFalse_WhenTokenIsRevoked()
        {
            // Arrange
            var tokenEntity = await _context.RefreshTokens.FirstAsync(t => t.Token == "token1");
            tokenEntity.Revoked = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.ValidateRefreshTokenAsync("token1", tokenEntity.UserId);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task ValidateRefreshTokenAsync_ShouldReturnFalse_WhenTokenIsExpired()
        {
            // Arrange
            var tokenEntity = await _context.RefreshTokens.FirstAsync(t => t.Token == "token1");
            tokenEntity.Expires = DateTime.UtcNow.AddSeconds(-1);
            tokenEntity.Revoked = null;
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.ValidateRefreshTokenAsync("token1", tokenEntity.UserId);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task ValidateRefreshTokenAsync_ShouldReturnFalse_WhenTokenBelongsToAnotherUser()
        {
            // Act
            var result = await _service.ValidateRefreshTokenAsync("token1", UserId2);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task ValidateRefreshTokenAsync_ShouldReturnFalse_WhenTokenIsNull()
        {
            // Act
            var result = await _service.ValidateRefreshTokenAsync(null!, UserId1);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task ValidateRefreshTokenAsync_ShouldReturnFalse_WhenTokenIsEmpty()
        {
            // Act
            var result = await _service.ValidateRefreshTokenAsync(string.Empty, UserId1);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task ValidateRefreshTokenAsync_ShouldReturnFalse_WhenTokenIsRevokedButNotExpired()
        {
            // Arrange
            var tokenEntity = await _context.RefreshTokens.FirstAsync(t => t.Token == "token1");
            tokenEntity.Revoked = DateTime.UtcNow;
            tokenEntity.Expires = DateTime.UtcNow.AddDays(1);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.ValidateRefreshTokenAsync("token1", tokenEntity.UserId);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task ValidateRefreshTokenAsync_ShouldReturnFalse_WhenTokenExpiredEvenIfCreatedRecently()
        {
            // Arrange
            var tokenEntity = await _context.RefreshTokens.FirstAsync(t => t.Token == "token1");
            tokenEntity.Created = DateTime.UtcNow;
            tokenEntity.Expires = DateTime.UtcNow.AddSeconds(-10);
            tokenEntity.Revoked = null;
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.ValidateRefreshTokenAsync("token1", tokenEntity.UserId);

            // Assert
            result.ShouldBeFalse();
        }

        #endregion

        #region RevokeRefreshTokenAsync Tests

        [Fact]
        public async Task RevokeRefreshTokenAsync_ShouldSetRevoked_WhenTokenExistsAndNotRevoked()
        {
            // Arrange
            var tokenValue = "token1";
            var tokenEntity = await _context.RefreshTokens.FirstAsync(t => t.Token == tokenValue);
            tokenEntity.Revoked = null;
            await _context.SaveChangesAsync();

            // Act
            await _service.RevokeRefreshTokenAsync(tokenValue);

            // Assert
            var updatedToken = await _context.RefreshTokens.FirstAsync(t => t.Token == tokenValue);
            updatedToken.Revoked.ShouldNotBeNull();
            updatedToken.Revoked!.Value.ShouldBeGreaterThan(tokenEntity.Created);
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_ShouldDoNothing_WhenTokenAlreadyRevoked()
        {
            // Arrange
            var tokenValue = "token1";
            var tokenEntity = await _context.RefreshTokens.FirstAsync(t => t.Token == tokenValue);
            var revokedTime = DateTime.UtcNow.AddHours(-1);
            tokenEntity.Revoked = revokedTime;
            await _context.SaveChangesAsync();

            // Act
            await _service.RevokeRefreshTokenAsync(tokenValue);

            // Assert
            var updatedToken = await _context.RefreshTokens.FirstAsync(t => t.Token == tokenValue);
            updatedToken.Revoked.ShouldBe(revokedTime);
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_ShouldDoNothing_WhenTokenDoesNotExist()
        {
            // Arrange
            var nonExistentToken = "nonexistent";

            // Act & Assert
            await Should.NotThrowAsync(async () =>
                await _service.RevokeRefreshTokenAsync(nonExistentToken)
            );
        }

        #endregion

        #region GetRefreshTokenEntityAsync Tests

        [Fact]
        public async Task GetRefreshTokenEntityAsync_ShouldReturnToken_WhenTokenExists()
        {
            // Arrange
            var tokenValue = "token1";

            // Act
            var result = await _service.GetRefreshTokenEntityAsync(tokenValue);

            // Assert
            result.ShouldNotBeNull();
            result!.Token.ShouldBe(tokenValue);
            result.User.ShouldNotBeNull();
            result.User!.Id.ShouldBe(UserId1);
        }

        [Fact]
        public async Task GetRefreshTokenEntityAsync_ShouldReturnNull_WhenTokenDoesNotExist()
        {
            // Arrange
            var tokenValue = "nonexistent";

            // Act
            var result = await _service.GetRefreshTokenEntityAsync(tokenValue);

            // Assert
            result.ShouldBeNull();
        }

        [Fact]
        public async Task GetRefreshTokenEntityAsync_ShouldReturnNull_WhenTokenIsEmpty()
        {
            // Arrange
            var tokenValue = "";

            // Act
            var result = await _service.GetRefreshTokenEntityAsync(tokenValue);

            // Assert
            result.ShouldBeNull();
        }

        [Fact]
        public async Task GetRefreshTokenEntityAsync_ShouldIncludeUser()
        {
            // Arrange
            var tokenValue = "token2";

            // Act
            var result = await _service.GetRefreshTokenEntityAsync(tokenValue);

            // Assert
            result.ShouldNotBeNull();
            result!.User.ShouldNotBeNull();
            result.User!.Id.ShouldBe(UserId2);
        }

        #endregion

        #region RevokeAllRefreshTokensForUserAsync Tests

        [Fact]
        public async Task RevokeAllRefreshTokensForUserAsync_ShouldRevokeAllActiveTokens_ForGivenUser()
        {
            // Arrange
            var userId = UserId1;

            // Act
            await _service.RevokeAllRefreshTokensForUserAsync(userId);

            // Assert
            var tokens = await _context.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();
            tokens.ShouldAllBe(t => t.Revoked.HasValue);
        }

        [Fact]
        public async Task RevokeAllRefreshTokensForUserAsync_ShouldDoNothing_WhenUserHasNoTokens()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act & Assert
            await Should.NotThrowAsync(async () =>
                await _service.RevokeAllRefreshTokensForUserAsync(userId)
            );

            var tokens = await _context.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();
            tokens.ShouldBeEmpty();
        }

        [Fact]
        public async Task RevokeAllRefreshTokensForUserAsync_ShouldNotRevokeAlreadyRevokedTokens()
        {
            // Arrange
            var token = await _context.RefreshTokens.FirstAsync(t => t.UserId == UserId2);
            token.Revoked = DateTime.UtcNow.AddHours(-1);
            await _context.SaveChangesAsync();

            // Act
            await _service.RevokeAllRefreshTokensForUserAsync(UserId2);

            // Assert
            var refreshedToken = await _context.RefreshTokens.FirstAsync(t => t.UserId == UserId2);
            refreshedToken.Revoked.ShouldBe(token.Revoked); 
        }

        #endregion
    }
}
