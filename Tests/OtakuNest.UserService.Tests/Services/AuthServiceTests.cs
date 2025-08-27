using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OtakuNest.Contracts;
using OtakuNest.UserService.Data;
using OtakuNest.UserService.DTOs;
using OtakuNest.UserService.Exceptions;
using OtakuNest.UserService.Models;
using OtakuNest.UserService.Services;
using Shouldly;

namespace OtakuNest.UserService.Tests.Services
{
    public class AuthServiceTests : IDisposable
    {
        private readonly UserDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly Mock<ITokenService> _tokenServiceMock;
        private readonly Mock<IPublishEndpoint> _publishEndpointMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly Mock<IResponseCookies> _cookiesMock;
        private readonly AuthService _service;
        private bool _disposed = false;

        private static readonly Guid UserId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        public AuthServiceTests()
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

            userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success)
                .Callback<ApplicationUser, string>(async (user, _) => {
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                });

            userManagerMock.Setup(u => u.FindByNameAsync("testuser1"))
                .ReturnsAsync(_context.Users.First(u => u.UserName == "testuser1"));

            userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "User"))
                .ReturnsAsync(IdentityResult.Success);

            _userManager = userManagerMock.Object;

            _tokenServiceMock = new Mock<ITokenService>();
            _tokenServiceMock.Setup(t => t.GenerateTokensAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(("header.payload.signature", "refresh-token"));

            _publishEndpointMock = new Mock<IPublishEndpoint>();

            _cookiesMock = new Mock<IResponseCookies>();
            var responseMock = new Mock<HttpResponse>();
            responseMock.Setup(r => r.Cookies).Returns(_cookiesMock.Object);

            var httpContextMock = new Mock<HttpContext>();
            httpContextMock.Setup(c => c.Response).Returns(responseMock.Object);

            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContextMock.Object);

            _service = new AuthService(
                _userManager,
                _tokenServiceMock.Object,
                _publishEndpointMock.Object,
                _httpContextAccessorMock.Object
            );
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
                    SecurityStamp = Guid.NewGuid().ToString()
                },
                new ApplicationUser
                {
                    Id = UserId2,
                    UserName = "testuser2",
                    Email = "test2@example.com",
                    SecurityStamp = Guid.NewGuid().ToString()
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

        #region RegisterAsync Tests

        [Fact]
        public async Task RegisterAsync_ShouldCreateUser_WhenDataIsValid()
        {
            // Arrange
            var dto = new UserRegisterDto
            {
                UserName = "newuser",
                Email = "newuser@example.com",
                Password = "P@ssw0rd!"
            };
            using var cts = new CancellationTokenSource();

            // Act
            var token = await _service.RegisterAsync(dto, cts.Token);

            // Assert
            token.ShouldNotBeNullOrEmpty();
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.UserName == dto.UserName);
            userInDb.ShouldNotBeNull();
            userInDb.Email.ShouldBe(dto.Email);
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrow_WhenUserAlreadyExists()
        {
            // Arrange
            var dto = new UserRegisterDto
            {
                UserName = "testuser1", 
                Email = "exists@example.com",
                Password = "P@ssw0rd!"
            };
            using var cts = new CancellationTokenSource();

            // Act & Assert
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await _service.RegisterAsync(dto, cts.Token)
            );
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrow_WhenCreateFails()
        {
            // Arrange
            var dto = new UserRegisterDto
            {
                UserName = "failuser",
                Email = "fail@example.com",
                Password = "badpassword"
            };

            var userManagerMock = Mock.Get(_userManager);
            userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), dto.Password))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

            using var cts = new CancellationTokenSource();

            // Act & Assert
            var ex = await Should.ThrowAsync<UserCreationException>(async () =>
                await _service.RegisterAsync(dto, cts.Token)
            );
            ex.Message.ShouldContain("Password too weak");
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrow_WhenAddRoleFails()
        {
            // Arrange
            var dto = new UserRegisterDto
            {
                UserName = "rolfailuser",
                Email = "rolfail@example.com",
                Password = "P@ssw0rd!"
            };

            var userManagerMock = Mock.Get(_userManager);
            userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "User"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role not assigned" }));

            using var cts = new CancellationTokenSource();

            // Act & Assert
            var ex = await Should.ThrowAsync<RoleAssignmentException>(async () =>
                await _service.RegisterAsync(dto, cts.Token)
            );
            ex.Message.ShouldContain("Role not assigned");
        }

        [Fact]
        public async Task RegisterAsync_ShouldAppendRefreshTokenCookie()
        {
            // Arrange
            var dto = new UserRegisterDto
            {
                UserName = "cookieuser",
                Email = "cookie@example.com",
                Password = "P@ssw0rd!"
            };
            using var cts = new CancellationTokenSource();

            // Act
            var token = await _service.RegisterAsync(dto, cts.Token);

            // Assert
            token.ShouldNotBeNullOrEmpty();

            _cookiesMock.Verify(c => c.Append(
                "refreshToken",
                "refresh-token",
                It.Is<CookieOptions>(o => o.HttpOnly && o.Secure && o.SameSite == SameSiteMode.Strict)
            ), Times.Once);
        }

        [Theory]
        [InlineData("user1", "user1@example.com", "Password1!")]
        [InlineData("user2", "user2@example.com", "Password2@")]
        public async Task RegisterAsync_ShouldCreateMultipleUsers_UsingTheory(string username, string email, string password)
        {
            // Arrange
            var dto = new UserRegisterDto
            {
                UserName = username,
                Email = email,
                Password = password
            };
            using var cts = new CancellationTokenSource();

            // Act
            var token = await _service.RegisterAsync(dto, cts.Token);

            // Assert
            token.ShouldNotBeNullOrEmpty();
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            userInDb.ShouldNotBeNull();
            userInDb.Email.ShouldBe(email);
        }

        [Fact]
        public async Task RegisterAsync_ShouldPublishUserCreatedEvent()
        {
            // Arrange
            var dto = new UserRegisterDto
            {
                UserName = "eventuser",
                Email = "event@example.com",
                Password = "P@ssw0rd!"
            };
            using var cts = new CancellationTokenSource();

            // Act
            await _service.RegisterAsync(dto, cts.Token);

            // Assert
            _publishEndpointMock.Verify(p => p.Publish(
                It.Is<UserCreatedEvent>(e => e.UserName == dto.UserName && e.Email == dto.Email),
                cts.Token
            ), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_ShouldReturnValidAccessToken()
        {
            // Arrange
            var dto = new UserRegisterDto
            {
                UserName = "tokenuser",
                Email = "token@example.com",
                Password = "P@ssw0rd!"
            };
            using var cts = new CancellationTokenSource();

            // Act
            var token = await _service.RegisterAsync(dto, cts.Token);

            // Assert
            token.ShouldNotBeNullOrEmpty();
            token.Split('.').Length.ShouldBe(3); 
        }

        [Fact]
        public async Task RegisterAsync_ShouldAllowDifferentUsernameWithSameEmail()
        {
            // Arrange
            var existingUser = await _context.Users.FirstAsync();
            var dto = new UserRegisterDto
            {
                UserName = "newusername",
                Email = existingUser.Email!,
                Password = "P@ssw0rd!"
            };
            using var cts = new CancellationTokenSource();

            // Act
            var token = await _service.RegisterAsync(dto, cts.Token);

            // Assert
            token.ShouldNotBeNullOrEmpty();
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.UserName == dto.UserName);
            userInDb.ShouldNotBeNull();
            userInDb.Email.ShouldBe(dto.Email);
        }

        [Theory]
        [InlineData(true, false)]  
        [InlineData(false, true)]  
        public async Task RegisterAsync_ShouldThrow_WhenCreateOrRoleFails(bool failCreate, bool failRole)
        {
            // Arrange
            var dto = new UserRegisterDto
            {
                UserName = "failcombo",
                Email = "failcombo@example.com",
                Password = "P@ssw0rd!"
            };

            var userManagerMock = Mock.Get(_userManager);
            if (failCreate)
                userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), dto.Password))
                    .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Create failed" }));
            if (failRole)
                userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "User"))
                    .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role failed" }));

            using var cts = new CancellationTokenSource();

            // Act & Assert
            if (failCreate)
                await Should.ThrowAsync<UserCreationException>(async () =>
                    await _service.RegisterAsync(dto, cts.Token));
            if (failRole)
                await Should.ThrowAsync<RoleAssignmentException>(async () =>
                    await _service.RegisterAsync(dto, cts.Token));
        }

        #endregion

        #region LoginAsync Tests

        [Fact]
        public async Task LoginAsync_ShouldReturnAccessToken_WhenCredentialsAreValid()
        {
            // Arrange
            var dto = new UserLoginDto
            {
                Email = "test1@example.com",
                Password = "P@ssw0rd!"
            };
            var user = await _context.Users.FirstAsync(u => u.Email == dto.Email);

            var userManagerMock = Mock.Get(_userManager);
            userManagerMock.Setup(u => u.FindByEmailAsync(dto.Email))
                .ReturnsAsync(user);
            userManagerMock.Setup(u => u.CheckPasswordAsync(user, dto.Password))
                .ReturnsAsync(true);

            // Act
            var token = await _service.LoginAsync(dto, CancellationToken.None);

            // Assert
            token.ShouldNotBeNullOrEmpty();
            _cookiesMock.Verify(c => c.Append(
                "refreshToken",
                "refresh-token",
                It.Is<CookieOptions>(o => o.HttpOnly && o.Secure && o.SameSite == SameSiteMode.Strict)
            ), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldThrow_WhenUserNotFound()
        {
            // Arrange
            var dto = new UserLoginDto
            {
                Email = "unknown@example.com",
                Password = "P@ssw0rd!"
            };
            var userManagerMock = Mock.Get(_userManager);
            userManagerMock.Setup(u => u.FindByEmailAsync(dto.Email))
                .ReturnsAsync((ApplicationUser?)null);

            // Act & Assert
            await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
                await _service.LoginAsync(dto, CancellationToken.None)
            );
        }

        [Fact]
        public async Task LoginAsync_ShouldThrow_WhenPasswordInvalid()
        {
            // Arrange
            var dto = new UserLoginDto
            {
                Email = "test1@example.com",
                Password = "WrongPassword"
            };
            var user = await _context.Users.FirstAsync(u => u.Email == dto.Email);

            var userManagerMock = Mock.Get(_userManager);
            userManagerMock.Setup(u => u.FindByEmailAsync(dto.Email))
                .ReturnsAsync(user);
            userManagerMock.Setup(u => u.CheckPasswordAsync(user, dto.Password))
                .ReturnsAsync(false);

            // Act & Assert
            await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
                await _service.LoginAsync(dto, CancellationToken.None)
            );
        }

        [Theory]
        [InlineData("test1@example.com", "Password1!")]
        [InlineData("test2@example.com", "Password2@")]
        public async Task LoginAsync_ShouldReturnToken_ForMultipleUsers(string email, string password)
        {
            // Arrange
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            var dto = new UserLoginDto { Email = email, Password = password };

            var userManagerMock = Mock.Get(_userManager);
            userManagerMock.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(user);
            userManagerMock.Setup(u => u.CheckPasswordAsync(user!, password)).ReturnsAsync(true);

            // Act
            var token = await _service.LoginAsync(dto, CancellationToken.None);

            // Assert
            token.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public async Task LoginAsync_ShouldAppendRefreshTokenCookie_WhenLoginSuccessful()
        {
            // Arrange
            var user = await _context.Users.FirstAsync();
            var dto = new UserLoginDto
            {
                Email = user.Email!,
                Password = "Password123!"
            };

            Mock.Get(_userManager).Setup(u => u.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
            Mock.Get(_userManager).Setup(u => u.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(true);

            // Act
            var token = await _service.LoginAsync(dto, CancellationToken.None);

            // Assert
            token.ShouldNotBeNullOrEmpty();
            _cookiesMock.Verify(c => c.Append(
                "refreshToken",
                "refresh-token",
                It.Is<CookieOptions>(o => o.HttpOnly && o.Secure && o.SameSite == SameSiteMode.Strict)
            ), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldThrow_WhenGenerateTokensFails()
        {
            // Arrange
            var user = await _context.Users.FirstAsync();
            var dto = new UserLoginDto
            {
                Email = user.Email!,
                Password = "Password123!"
            };

            Mock.Get(_userManager).Setup(u => u.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
            Mock.Get(_userManager).Setup(u => u.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(true);

            _tokenServiceMock.Setup(t => t.GenerateTokensAsync(user))
                .ThrowsAsync(new InvalidOperationException("Token generation failed"));

            // Act & Assert
            var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await _service.LoginAsync(dto, CancellationToken.None)
            );
            ex.Message.ShouldBe("Token generation failed");
        }

        [Fact]
        public async Task LoginAsync_ShouldSetCookieExpirationCorrectly()
        {
            // Arrange
            var user = await _context.Users.FirstAsync();
            var dto = new UserLoginDto
            {
                Email = user.Email!,
                Password = "Password123!"
            };

            Mock.Get(_userManager).Setup(u => u.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
            Mock.Get(_userManager).Setup(u => u.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(true);

            // Act
            var token = await _service.LoginAsync(dto, CancellationToken.None);

            // Assert
            token.ShouldNotBeNullOrEmpty();
            _cookiesMock.Verify(c => c.Append(
                "refreshToken",
                "refresh-token",
                It.Is<CookieOptions>(o =>
                    o.HttpOnly &&
                    o.Secure &&
                    o.SameSite == SameSiteMode.Strict &&
                    o.Expires.HasValue &&
                    (o.Expires.Value - DateTime.UtcNow).TotalDays <= 7.1 &&
                    (o.Expires.Value - DateTime.UtcNow).TotalDays >= 6.9
                )
            ), Times.Once);
        }

        #endregion

        #region RefreshTokenAsync Tests

        [Fact]
        public async Task RefreshTokenAsync_ShouldThrow_WhenTokenNotFound()
        {
            // Arrange
            var refreshToken = "invalid-token";
            _tokenServiceMock.Setup(t => t.GetRefreshTokenEntityAsync(refreshToken))
                .ReturnsAsync((RefreshToken?)null);

            // Act & Assert
            await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
                await _service.RefreshTokenAsync(refreshToken)
            );
        }

        [Fact]
        public async Task RefreshTokenAsync_ShouldThrow_WhenTokenRevoked()
        {
            // Arrange
            var refreshToken = "revoked-token";
            var tokenEntity = new RefreshToken { Revoked = DateTime.UtcNow.AddMinutes(-1), Expires = DateTime.UtcNow.AddMinutes(10) };
            _tokenServiceMock.Setup(t => t.GetRefreshTokenEntityAsync(refreshToken))
                .ReturnsAsync(tokenEntity);

            // Act & Assert
            await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
                await _service.RefreshTokenAsync(refreshToken)
            );
        }

        [Fact]
        public async Task RefreshTokenAsync_ShouldThrow_WhenTokenExpired()
        {
            // Arrange
            var refreshToken = "expired-token";
            var tokenEntity = new RefreshToken { Revoked = null, Expires = DateTime.UtcNow.AddMinutes(-1) };
            _tokenServiceMock.Setup(t => t.GetRefreshTokenEntityAsync(refreshToken))
                .ReturnsAsync(tokenEntity);

            // Act & Assert
            await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
                await _service.RefreshTokenAsync(refreshToken)
            );
        }

        [Fact]
        public async Task RefreshTokenAsync_ShouldThrow_WhenUserNotFound()
        {
            // Arrange
            var refreshToken = "token-without-user";
            var tokenEntity = new RefreshToken { Revoked = null, Expires = DateTime.UtcNow.AddMinutes(10), UserId = Guid.NewGuid() };
            _tokenServiceMock.Setup(t => t.GetRefreshTokenEntityAsync(refreshToken))
                .ReturnsAsync(tokenEntity);

            var userManagerMock = Mock.Get(_userManager);
            userManagerMock.Setup(u => u.FindByIdAsync(tokenEntity.UserId.ToString()))
                .ReturnsAsync((ApplicationUser?)null);

            // Act & Assert
            await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
                await _service.RefreshTokenAsync(refreshToken)
            );
        }

        [Fact]
        public async Task RefreshTokenAsync_ShouldReturnTokens_WhenValid()
        {
            // Arrange
            var refreshToken = "valid-token";
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "testuser" };
            var tokenEntity = new RefreshToken { Revoked = null, Expires = DateTime.UtcNow.AddMinutes(10), User = user };
            _tokenServiceMock.Setup(t => t.GetRefreshTokenEntityAsync(refreshToken))
                .ReturnsAsync(tokenEntity);

            _tokenServiceMock.Setup(t => t.RevokeRefreshTokenAsync(refreshToken))
                .Returns(Task.CompletedTask);

            _tokenServiceMock.Setup(t => t.GenerateTokensAsync(user))
                .ReturnsAsync(("new-access-token", "new-refresh-token"));

            // Act
            var result = await _service.RefreshTokenAsync(refreshToken);

            // Assert
            result.AccessToken.ShouldBe("new-access-token");
            result.RefreshToken.ShouldBe("new-refresh-token");
            _tokenServiceMock.Verify(t => t.RevokeRefreshTokenAsync(refreshToken), Times.Once);
            _tokenServiceMock.Verify(t => t.GenerateTokensAsync(user), Times.Once);
        }

        #endregion

        #region Additional Tests

        [Fact]
        public async Task GetRefreshTokenEntityAsync_ShouldReturnTokenEntity()
        {
            // Arrange
            var refreshToken = "token123";
            var expectedEntity = new RefreshToken { Token = refreshToken };
            _tokenServiceMock.Setup(t => t.GetRefreshTokenEntityAsync(refreshToken))
                .ReturnsAsync(expectedEntity);

            // Act
            var result = await _service.GetRefreshTokenEntityAsync(refreshToken);

            // Assert
            result.ShouldBe(expectedEntity);
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_ShouldCallTokenService()
        {
            // Arrange
            var refreshToken = "token123";

            // Act
            await _service.RevokeRefreshTokenAsync(refreshToken);

            // Assert
            _tokenServiceMock.Verify(t => t.RevokeRefreshTokenAsync(refreshToken), Times.Once);
        }

        #endregion
    }
}
