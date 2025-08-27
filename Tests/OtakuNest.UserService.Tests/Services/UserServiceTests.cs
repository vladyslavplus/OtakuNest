using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OtakuNest.Common.Helpers;
using OtakuNest.Common.Interfaces;
using OtakuNest.UserService.Data;
using OtakuNest.UserService.DTOs;
using OtakuNest.UserService.Models;
using OtakuNest.UserService.Parameters;
using Shouldly;

namespace OtakuNest.UserService.Tests.Services
{
    public class UserServiceTests : IDisposable
    {
        private readonly UserDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserService.Services.UserService _service;
        private bool _disposed = false;

        private static readonly Guid UserId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        public UserServiceTests()
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
                new Mock<ILookupNormalizer>().Object,
                new IdentityErrorDescriber(),
                new Mock<IServiceProvider>().Object,
                loggerMock.Object
            );

            userManagerMock.Setup(u => u.Users).Returns(_context.Users.AsQueryable());

            userManagerMock.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);

            userManagerMock.Setup(u => u.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync("fake-token");

            userManagerMock.Setup(u => u.ResetPasswordAsync(It.IsAny<ApplicationUser>(), "fake-token", It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            var isInMemory = true;
            var sortHelper = new SortHelper<ApplicationUser>();

            _userManager = userManagerMock.Object;
            _service = new UserService.Services.UserService(_userManager, sortHelper, isInMemory);
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
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString()
                },
                new ApplicationUser
                {
                    Id = UserId2,
                    UserName = "testuser2",
                    Email = "test2@example.com",
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    EmailConfirmed = false,
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
                    _userManager?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #region GetUsersAsync Tests

        [Fact]
        public async Task GetUsersAsync_ShouldReturnAllUsers_WhenNoFiltersApplied()
        {
            // Arrange
            var parameters = new UserParameters();

            // Act
            var result = await _service.GetUsersAsync(parameters);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(2);
            result.ShouldContain(u => u.Id == UserId1);
            result.ShouldContain(u => u.Id == UserId2);
        }

        [Theory]
        [InlineData("testuser1", 1)]
        [InlineData("testuser2", 1)]
        [InlineData("nonexistent", 0)]
        public async Task GetUsersAsync_ShouldFilterByUserName(string username, int expectedCount)
        {
            // Arrange
            var parameters = new UserParameters { UserName = username };

            // Act
            var result = await _service.GetUsersAsync(parameters);

            // Assert
            result.Count.ShouldBe(expectedCount);
        }

        [Theory]
        [InlineData(true, 1)]
        [InlineData(false, 1)]
        public async Task GetUsersAsync_ShouldFilterByEmailConfirmed(bool emailConfirmed, int expectedCount)
        {
            // Arrange
            var parameters = new UserParameters { EmailConfirmed = emailConfirmed };

            // Act
            var result = await _service.GetUsersAsync(parameters);

            // Assert
            result.Count.ShouldBe(expectedCount);
            result.All(u => u.EmailConfirmed == emailConfirmed).ShouldBeTrue();
        }

        [Theory]
        [InlineData("test1@example.com", 1)]
        [InlineData("test2@example.com", 1)]
        [InlineData("nonexistent@example.com", 0)]
        public async Task GetUsersAsync_ShouldFilterByEmail(string email, int expectedCount)
        {
            // Arrange
            var parameters = new UserParameters { Email = email };

            // Act
            var result = await _service.GetUsersAsync(parameters);

            // Assert
            result.Count.ShouldBe(expectedCount);
            if (expectedCount > 0)
                result.All(u => u.Email == email).ShouldBeTrue();
        }

        [Theory]
        [InlineData("1234567890", 1)]
        [InlineData("0987654321", 0)]
        public async Task GetUsersAsync_ShouldFilterByPhoneNumber(string phone, int expectedCount)
        {
            // Arrange
            var parameters = new UserParameters { PhoneNumber = phone };

            // Act
            var result = await _service.GetUsersAsync(parameters);

            // Assert
            result.Count.ShouldBe(expectedCount);
        }

        [Fact]
        public async Task GetUsersAsync_ShouldFilterByCreatedAtRange()
        {
            // Arrange
            var parameters = new UserParameters
            {
                CreatedAtFrom = DateTime.UtcNow.AddDays(-3),
                CreatedAtTo = DateTime.UtcNow
            };

            // Act
            var result = await _service.GetUsersAsync(parameters);

            // Assert
            result.Count.ShouldBe(1);
            result[0].Id.ShouldBe(UserId2);
        }

        [Fact]
        public async Task GetUsersAsync_ShouldFilterByCreatedAtFromOnly()
        {
            // Arrange
            var parameters = new UserParameters
            {
                CreatedAtFrom = DateTime.UtcNow.AddDays(-4)
            };

            // Act
            var result = await _service.GetUsersAsync(parameters);

            // Assert
            result.Count.ShouldBe(1);
            result[0].Id.ShouldBe(UserId2);
        }

        [Fact]
        public async Task GetUsersAsync_ShouldFilterByCreatedAtToOnly()
        {
            // Arrange
            var parameters = new UserParameters
            {
                CreatedAtTo = DateTime.UtcNow.AddDays(-3)
            };

            // Act
            var result = await _service.GetUsersAsync(parameters);

            // Assert
            result.Count.ShouldBe(1);
            result[0].Id.ShouldBe(UserId1);
        }

        [Fact]
        public async Task GetUsersAsync_ShouldApplyPagination()
        {
            // Arrange
            var parameters = new UserParameters { PageNumber = 2, PageSize = 1 };

            // Act
            var result = await _service.GetUsersAsync(parameters);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);
            result.CurrentPage.ShouldBe(2);
            result.PageSize.ShouldBe(1);
        }

        [Fact]
        public async Task GetUsersAsync_ShouldApplySorting_ByUserNameAscending()
        {
            // Arrange
            var parameters = new UserParameters { OrderBy = "UserName asc" };

            // Act
            var result = await _service.GetUsersAsync(parameters);

            // Assert
            result[0].UserName.ShouldBe("testuser1");
            result[1].UserName.ShouldBe("testuser2");
        }

        [Fact]
        public async Task GetUsersAsync_ShouldApplySorting_ByCreatedAtDescending()
        {
            // Arrange
            var parameters = new UserParameters { OrderBy = "CreatedAt desc" };

            // Act
            var result = await _service.GetUsersAsync(parameters);

            // Assert
            result[0].Id.ShouldBe(UserId2); 
        }

        [Fact]
        public async Task GetUsersAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource(); 
            await cts.CancelAsync();
            var parameters = new UserParameters();

            // Act & Assert
            await Should.ThrowAsync<TaskCanceledException>(async () =>
                await _service.GetUsersAsync(parameters, cts.Token)
            );
        }

        #endregion

        #region GetUserByIdAsync Tests

        [Fact]
        public async Task GetUserByIdAsync_ShouldReturnUser_WhenUserExists()
        {
            // Arrange
            var userId = UserId1;

            // Act
            var result = await _service.GetUserByIdAsync(userId);

            // Assert
            result.ShouldNotBeNull();
            result!.Id.ShouldBe(userId);
        }

        [Fact]
        public async Task GetUserByIdAsync_ShouldReturnNull_WhenUserDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            var result = await _service.GetUserByIdAsync(userId);

            // Assert
            result.ShouldBeNull();
        }

        [Fact]
        public async Task GetUserByIdAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var userId = UserId1;

            // Act & Assert
            await Should.ThrowAsync<TaskCanceledException>(async () =>
                await _service.GetUserByIdAsync(userId, cts.Token)
            );
        }

        [Fact]
        public async Task GetUserByIdAsync_ShouldReturnCorrectUser_AmongMultipleUsers()
        {
            // Arrange
            var userId = UserId2;

            // Act
            var result = await _service.GetUserByIdAsync(userId);

            // Assert
            result.ShouldNotBeNull();
            result!.Id.ShouldBe(userId);
            result.UserName.ShouldBe("testuser2");
        }

        [Fact]
        public async Task GetUserByIdAsync_ShouldNotTrackChanges()
        {
            // Arrange
            var userId = UserId1;

            // Act
            var result = await _service.GetUserByIdAsync(userId);
            result!.UserName = "changedName";

            var fromDb = await _service.GetUserByIdAsync(userId);

            // Assert
            fromDb!.UserName.ShouldNotBe("changedName");
            fromDb.UserName.ShouldBe("testuser1");
        }

        [Fact]
        public async Task GetUserByIdAsync_ShouldReturnNull_WhenIdIsEmptyGuid()
        {
            // Arrange
            var userId = Guid.Empty;

            // Act
            var result = await _service.GetUserByIdAsync(userId);

            // Assert
            result.ShouldBeNull();
        }

        #endregion

        #region UpdateUserAsync Tests

        [Fact]
        public async Task UpdateUserAsync_ShouldReturnFalse_WhenUserDoesNotExist()
        {
            // Arrange
            var dto = new UpdateUserDto { UserName = "newname" };

            // Act
            var result = await _service.UpdateUserAsync(Guid.NewGuid(), dto);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateUserAsync_ShouldUpdateUserName_WhenProvided()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);
            var dto = new UpdateUserDto { UserName = "updatedname" };

            // Act
            var result = await _service.UpdateUserAsync(UserId1, dto);

            // Assert
            result.ShouldBeTrue();
            user.UserName.ShouldBe("updatedname");
        }

        [Fact]
        public async Task UpdateUserAsync_ShouldUpdateEmailAndResetEmailConfirmed_WhenProvided()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);
            var dto = new UpdateUserDto { Email = "newemail@example.com" };

            // Act
            var result = await _service.UpdateUserAsync(UserId1, dto);

            // Assert
            result.ShouldBeTrue();
            user.Email.ShouldBe("newemail@example.com");
            user.EmailConfirmed.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateUserAsync_ShouldUpdatePhoneNumber_WhenProvided()
        {
            // Arrange
            var user = await _context.Users.FirstAsync(u => u.Id == UserId1);
            var dto = new UpdateUserDto { PhoneNumber = "1234567890" };

            // Act
            var result = await _service.UpdateUserAsync(UserId1, dto);

            // Assert
            result.ShouldBeTrue();
            user.PhoneNumber.ShouldBe("1234567890");
        }

        [Fact]
        public async Task UpdateUserAsync_ShouldUpdatePassword_WhenProvided()
        {
            // Arrange
            await _context.Users.FirstAsync(u => u.Id == UserId1);
            var dto = new UpdateUserDto { Password = "NewP@ssw0rd!" };

            // Act
            var result = await _service.UpdateUserAsync(UserId1, dto);

            // Assert
            result.ShouldBeTrue();
        }

        [Fact]
        public async Task UpdateUserAsync_ShouldReturnFalse_WhenUpdateFails()
        {
            // Arrange
            var userManagerMock = new Mock<UserManager<ApplicationUser>>(
                new Mock<IUserStore<ApplicationUser>>().Object,
                null!, null!, null!, null!, null!, null!, null!, null!
            );
            userManagerMock.Setup(u => u.Users).Returns(_context.Users.AsQueryable());
            userManagerMock.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Failed());

            var service = new UserService.Services.UserService(userManagerMock.Object, new SortHelper<ApplicationUser>());
            var dto = new UpdateUserDto { UserName = "failtest" };

            // Act
            var result = await service.UpdateUserAsync(UserId1, dto);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateUserAsync_ShouldReturnFalse_WhenPasswordResetFails()
        {
            // Arrange
            var userManagerMock = new Mock<UserManager<ApplicationUser>>(
                new Mock<IUserStore<ApplicationUser>>().Object,
                null!, null!, null!, null!, null!, null!, null!, null!
            );
            userManagerMock.Setup(u => u.Users).Returns(_context.Users.AsQueryable());
            userManagerMock.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
            userManagerMock.Setup(u => u.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync("token");
            userManagerMock.Setup(u => u.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed());

            var service = new UserService.Services.UserService(userManagerMock.Object, new SortHelper<ApplicationUser>());
            var dto = new UpdateUserDto { Password = "FailP@ss!" };

            // Act
            var result = await service.UpdateUserAsync(UserId1, dto);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateUserAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var dto = new UpdateUserDto { UserName = "newname" };

            // Act & Assert
            await Should.ThrowAsync<TaskCanceledException>(async () =>
                await _service.UpdateUserAsync(UserId1, dto, cts.Token)
            );
        }

        #endregion
    }
}
