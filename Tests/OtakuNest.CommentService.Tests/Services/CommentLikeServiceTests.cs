using Microsoft.EntityFrameworkCore;
using Moq;
using OtakuNest.CommentService.Data;
using OtakuNest.CommentService.DTOs;
using OtakuNest.CommentService.Models;
using OtakuNest.CommentService.Services;
using OtakuNest.Common.Services.Caching;
using Shouldly;

namespace OtakuNest.CommentService.Tests.Services
{
    public class CommentLikeServiceTests : IDisposable
    {
        private readonly CommentDbContext _context;
        private readonly CommentLikeService _service;
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private bool _disposed = false;

        private static readonly Guid CommentId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid CommentId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid CommentId3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid ParentCommentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid UserId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private const int CacheExpirationMinutes = 15;

        public CommentLikeServiceTests()
        {
            _context = GetInMemoryDbContext();
            _cacheServiceMock = new Mock<IRedisCacheService>();
            _service = CreateService(_context);
        }

        private static CommentDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<CommentDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var context = new CommentDbContext(options);

            context.Comments.AddRange(
                new Comment
                {
                    Id = CommentId1,
                    Content = "Test comment 1",
                    UserId = UserId1,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new Comment
                {
                    Id = CommentId2,
                    Content = "Test comment 2",
                    UserId = UserId2,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new Comment
                {
                    Id = CommentId3,
                    Content = "Reply to comment",
                    UserId = UserId1,
                    ParentCommentId = ParentCommentId,
                    CreatedAt = DateTime.UtcNow
                },
                new Comment
                {
                    Id = ParentCommentId,
                    Content = "Parent comment",
                    UserId = UserId2,
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                }
            );

            context.CommentLikes.AddRange(
                new CommentLike
                {
                    Id = Guid.NewGuid(),
                    CommentId = CommentId1,
                    UserId = UserId1,
                    LikedAt = DateTime.UtcNow.AddDays(-1)
                },
                new CommentLike
                {
                    Id = Guid.NewGuid(),
                    CommentId = CommentId1,
                    UserId = UserId2,
                    LikedAt = DateTime.UtcNow
                },
                new CommentLike
                {
                    Id = Guid.NewGuid(),
                    CommentId = CommentId2,
                    UserId = UserId1,
                    LikedAt = DateTime.UtcNow.AddMinutes(-30)
                }
            );

            context.SaveChanges();
            return context;
        }

        private CommentLikeService CreateService(CommentDbContext context)
        {
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<int?>(It.IsAny<string>()))
                .ReturnsAsync((int?)null);

            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            _cacheServiceMock
                .Setup(x => x.RemoveDataAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _cacheServiceMock
                .Setup(x => x.AddToSetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _cacheServiceMock
                .Setup(x => x.GetSetMembersAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<string>());

            _cacheServiceMock
                .Setup(x => x.ClearSetAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            return new CommentLikeService(
                context,
                _cacheServiceMock.Object
            );
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #region GetLikesCountAsync Tests

        [Fact]
        public async Task GetLikesCountAsync_ReturnsCachedValue_WhenCacheHasData()
        {
            // Arrange
            var expectedCount = 5;
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<int?>(It.IsAny<string>()))
                .ReturnsAsync(expectedCount);

            // Act
            var result = await _service.GetLikesCountAsync(CommentId1);

            // Assert
            result.ShouldBe(expectedCount);
            _cacheServiceMock.Verify(x => x.GetDataAsync<int?>($"comment:likes:count:{CommentId1}"), Times.Once);
            _cacheServiceMock.Verify(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()), Times.Never);
        }

        [Fact]
        public async Task GetLikesCountAsync_ReturnsDbValue_WhenCacheIsEmpty()
        {
            // Arrange
            _cacheServiceMock.Reset(); 
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<int?>(It.IsAny<string>()))
                .ReturnsAsync((int?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.GetLikesCountAsync(CommentId1);

            // Assert
            result.ShouldBe(2);
            _cacheServiceMock.Verify(x => x.GetDataAsync<int?>($"comment:likes:count:{CommentId1}"), Times.Once);
            _cacheServiceMock.Verify(x => x.SetDataAsync($"comment:likes:count:{CommentId1}", 2, TimeSpan.FromMinutes(CacheExpirationMinutes)), Times.Once);
        }

        [Fact]
        public async Task GetLikesCountAsync_ReturnsZero_WhenNoLikesExist()
        {
            // Arrange
            var nonExistentCommentId = Guid.NewGuid();
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<int?>(It.IsAny<string>()))
                .ReturnsAsync((int?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.GetLikesCountAsync(nonExistentCommentId);

            // Assert
            result.ShouldBe(0);
            _cacheServiceMock.Verify(x => x.SetDataAsync($"comment:likes:count:{nonExistentCommentId}", 0, TimeSpan.FromMinutes(CacheExpirationMinutes)), Times.Once);
        }

        [Fact]
        public async Task GetLikesCountAsync_CallsSetDataWithCorrectTtl()
        {
            // Arrange
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<int?>(It.IsAny<string>()))
                .ReturnsAsync((int?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.GetLikesCountAsync(CommentId2);

            // Assert
            result.ShouldBe(1); 
            _cacheServiceMock.Verify(
                x => x.SetDataAsync($"comment:likes:count:{CommentId2}", 1, TimeSpan.FromMinutes(CacheExpirationMinutes)),
                Times.Once);
        }
        [Fact]
        public async Task GetLikesCountAsync_PassesCancellationToken_ToDbQuery()
        {
            // Arrange
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<int?>(It.IsAny<string>()))
                .ReturnsAsync((int?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.GetLikesCountAsync(CommentId1, cancellationToken);

            // Assert
            result.ShouldBe(2);
        }

        [Fact]
        public async Task GetLikesCountAsync_ThrowsOperationCanceled_WhenCancellationRequested()
        {
            // Arrange
            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<int?>(It.IsAny<string>()))
                .ReturnsAsync((int?)null);

            // Act & Assert
            await Should.ThrowAsync<OperationCanceledException>(() =>
                _service.GetLikesCountAsync(CommentId1, cancellationTokenSource.Token));
        }

        [Fact]
        public async Task GetLikesCountAsync_HandlesCacheException_AndFallsBackToDb()
        {
            // Arrange
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<int?>(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Redis connection failed"));
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act & Assert
            await Should.ThrowAsync<InvalidOperationException>(() =>
                _service.GetLikesCountAsync(CommentId1));
        }

        [Theory]
        [InlineData("11111111-1111-1111-1111-111111111111", 2)]
        [InlineData("22222222-2222-2222-2222-222222222222", 1)]
        public async Task GetLikesCountAsync_ReturnsCorrectCount_ForVariousComments(string commentIdStr, int expectedCount)
        {
            // Arrange
            var commentId = Guid.Parse(commentIdStr);
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<int?>(It.IsAny<string>()))
                .ReturnsAsync((int?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.GetLikesCountAsync(commentId);

            // Assert
            result.ShouldBe(expectedCount);
        }

        [Fact]
        public async Task GetLikesCountAsync_CacheKey_IsFormattedCorrectly()
        {
            // Arrange
            var testCommentId = Guid.NewGuid();
            var expectedCacheKey = $"comment:likes:count:{testCommentId}";
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<int?>(expectedCacheKey))
                .ReturnsAsync((int?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.GetLikesCountAsync(testCommentId);

            // Assert
            _cacheServiceMock.Verify(x => x.GetDataAsync<int?>(expectedCacheKey), Times.Once);
            _cacheServiceMock.Verify(x => x.SetDataAsync(expectedCacheKey, It.IsAny<object>(), It.IsAny<TimeSpan?>()), Times.Once);
        }

        #endregion

        #region HasUserLikedAsync Tests

        [Fact]
        public async Task HasUserLikedAsync_ReturnsCachedValue_WhenCacheHasData()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId1 };
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<bool?>(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.HasUserLikedAsync(dto);

            // Assert
            result.ShouldBeTrue();
            _cacheServiceMock.Verify(x => x.GetDataAsync<bool?>($"comment:like:user:{dto.CommentId}:{dto.UserId}"), Times.Once);
            _cacheServiceMock.Verify(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()), Times.Never);
        }

        [Fact]
        public async Task HasUserLikedAsync_ReturnsDbValue_WhenCacheIsEmpty()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId1 };
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<bool?>(It.IsAny<string>()))
                .ReturnsAsync((bool?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.HasUserLikedAsync(dto);

            // Assert
            result.ShouldBeTrue(); 
            _cacheServiceMock.Verify(x => x.GetDataAsync<bool?>($"comment:like:user:{dto.CommentId}:{dto.UserId}"), Times.Once);
            _cacheServiceMock.Verify(x => x.SetDataAsync($"comment:like:user:{dto.CommentId}:{dto.UserId}", true, TimeSpan.FromMinutes(10)), Times.Once);
        }

        [Fact]
        public async Task HasUserLikedAsync_ReturnsFalse_WhenUserHasNotLiked()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = nonExistentUserId };
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<bool?>(It.IsAny<string>()))
                .ReturnsAsync((bool?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.HasUserLikedAsync(dto);

            // Assert
            result.ShouldBeFalse();
            _cacheServiceMock.Verify(x => x.SetDataAsync($"comment:like:user:{dto.CommentId}:{dto.UserId}", false, TimeSpan.FromMinutes(10)), Times.Once);
        }

        [Fact]
        public async Task HasUserLikedAsync_ReturnsFalse_WhenCommentDoesNotExist()
        {
            // Arrange
            var nonExistentCommentId = Guid.NewGuid();
            var dto = new LikeCommentDto { CommentId = nonExistentCommentId, UserId = UserId1 };
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<bool?>(It.IsAny<string>()))
                .ReturnsAsync((bool?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.HasUserLikedAsync(dto);

            // Assert
            result.ShouldBeFalse();
            _cacheServiceMock.Verify(x => x.SetDataAsync($"comment:like:user:{dto.CommentId}:{dto.UserId}", false, TimeSpan.FromMinutes(10)), Times.Once);
        }

        [Fact]
        public async Task HasUserLikedAsync_CallsSetDataWithCorrectTtl()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId2, UserId = UserId1 };
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<bool?>(It.IsAny<string>()))
                .ReturnsAsync((bool?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.HasUserLikedAsync(dto);

            // Assert
            result.ShouldBeTrue();
            _cacheServiceMock.Verify(
                x => x.SetDataAsync($"comment:like:user:{dto.CommentId}:{dto.UserId}", true, TimeSpan.FromMinutes(10)),
                Times.Once);
        }

        [Fact]
        public async Task HasUserLikedAsync_PassesCancellationToken_ToDbQuery()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId1 };
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<bool?>(It.IsAny<string>()))
                .ReturnsAsync((bool?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.HasUserLikedAsync(dto, cancellationToken);

            // Assert
            result.ShouldBeTrue();
        }

        [Fact]
        public async Task HasUserLikedAsync_ThrowsOperationCanceled_WhenCancellationRequested()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId1 };
            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<bool?>(It.IsAny<string>()))
                .ReturnsAsync((bool?)null);

            // Act & Assert
            await Should.ThrowAsync<OperationCanceledException>(() =>
                _service.HasUserLikedAsync(dto, cancellationTokenSource.Token));
        }

        [Fact]
        public async Task HasUserLikedAsync_HandlesCacheException_AndFallsBackToDb()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId1 };
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<bool?>(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Redis connection failed"));
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act & Assert
            await Should.ThrowAsync<InvalidOperationException>(() =>
                _service.HasUserLikedAsync(dto));
        }

        [Theory]
        [InlineData("11111111-1111-1111-1111-111111111111", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", true)]
        [InlineData("11111111-1111-1111-1111-111111111111", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", true)]
        [InlineData("22222222-2222-2222-2222-222222222222", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", true)]
        [InlineData("22222222-2222-2222-2222-222222222222", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", false)]
        public async Task HasUserLikedAsync_ReturnsCorrectResult_ForVariousUserCommentCombinations(string commentIdStr, string userIdStr, bool expectedResult)
        {
            // Arrange
            var commentId = Guid.Parse(commentIdStr);
            var userId = Guid.Parse(userIdStr);
            var dto = new LikeCommentDto { CommentId = commentId, UserId = userId };
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<bool?>(It.IsAny<string>()))
                .ReturnsAsync((bool?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.HasUserLikedAsync(dto);

            // Assert
            result.ShouldBe(expectedResult);
        }

        [Fact]
        public async Task HasUserLikedAsync_CacheKey_IsFormattedCorrectly()
        {
            // Arrange
            var testCommentId = Guid.NewGuid();
            var testUserId = Guid.NewGuid();
            var dto = new LikeCommentDto { CommentId = testCommentId, UserId = testUserId };
            var expectedCacheKey = $"comment:like:user:{testCommentId}:{testUserId}";
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<bool?>(expectedCacheKey))
                .ReturnsAsync((bool?)null);
            _cacheServiceMock
                .Setup(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.HasUserLikedAsync(dto);

            // Assert
            _cacheServiceMock.Verify(x => x.GetDataAsync<bool?>(expectedCacheKey), Times.Once);
            _cacheServiceMock.Verify(x => x.SetDataAsync(expectedCacheKey, It.IsAny<object>(), It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task HasUserLikedAsync_ReturnsCachedFalseValue_WhenCacheHasFalseData()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId1 };
            _cacheServiceMock.Reset();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<bool?>(It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.HasUserLikedAsync(dto);

            // Assert
            result.ShouldBeFalse();
            _cacheServiceMock.Verify(x => x.GetDataAsync<bool?>($"comment:like:user:{dto.CommentId}:{dto.UserId}"), Times.Once);
            _cacheServiceMock.Verify(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()), Times.Never);
        }

        #endregion

        #region AddLikeAsync Tests

        [Fact]
        public async Task AddLikeAsync_ReturnsTrue_WhenLikeAddedSuccessfully()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId2, UserId = UserId2 };
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            var result = await _service.AddLikeAsync(dto);

            // Assert
            result.ShouldBeTrue();

            var likeExists = await _context.CommentLikes
                .AnyAsync(l => l.CommentId == dto.CommentId && l.UserId == dto.UserId);
            likeExists.ShouldBeTrue();

            VerifyCacheInvalidation(dto.CommentId, dto.UserId);
        }

        [Fact]
        public async Task AddLikeAsync_ReturnsFalse_WhenUserAlreadyLikedComment()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId1 }; 
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            var result = await _service.AddLikeAsync(dto);

            // Assert
            result.ShouldBeFalse();

            var likesCount = await _context.CommentLikes
                .CountAsync(l => l.CommentId == dto.CommentId && l.UserId == dto.UserId);
            likesCount.ShouldBe(1); 

            _cacheServiceMock.Verify(x => x.RemoveDataAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AddLikeAsync_ReturnsFalse_WhenCommentDoesNotExist()
        {
            // Arrange
            var nonExistentCommentId = Guid.NewGuid();
            var dto = new LikeCommentDto { CommentId = nonExistentCommentId, UserId = UserId1 };
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            var result = await _service.AddLikeAsync(dto);

            // Assert
            result.ShouldBeFalse();

            var likeExists = await _context.CommentLikes
                .AnyAsync(l => l.CommentId == dto.CommentId && l.UserId == dto.UserId);
            likeExists.ShouldBeFalse();

            _cacheServiceMock.Verify(x => x.RemoveDataAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AddLikeAsync_PassesCancellationToken_ToDbOperations()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId2, UserId = UserId2 };
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            var result = await _service.AddLikeAsync(dto, cancellationToken);

            // Assert
            result.ShouldBeTrue();
        }

        [Fact]
        public async Task AddLikeAsync_ThrowsOperationCanceled_WhenCancellationRequested()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId2, UserId = UserId2 };
            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act & Assert
            await Should.ThrowAsync<OperationCanceledException>(() =>
                _service.AddLikeAsync(dto, cancellationTokenSource.Token));
        }

        [Fact]
        public async Task AddLikeAsync_InvalidatesCorrectCacheKeys_WhenLikeAdded()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId2, UserId = UserId2 };
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            await _service.AddLikeAsync(dto);

            // Assert
            VerifyCacheInvalidation(dto.CommentId, dto.UserId);
        }

        [Fact]
        public async Task AddLikeAsync_InvalidatesParentCommentCache_WhenCommentHasParent()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId3, UserId = UserId2 }; // CommentId3 has ParentCommentId
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            await _service.AddLikeAsync(dto);

            // Assert
            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{CommentId3}"), Times.Once);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{ParentCommentId}"), Times.Once);
        }

        [Fact]
        public async Task AddLikeAsync_SetsCorrectLikeProperties()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId2, UserId = UserId2 };
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            await _service.AddLikeAsync(dto);

            // Assert
            var addedLike = await _context.CommentLikes
                .FirstOrDefaultAsync(l => l.CommentId == dto.CommentId && l.UserId == dto.UserId);

            addedLike.ShouldNotBeNull();
            addedLike.CommentId.ShouldBe(dto.CommentId);
            addedLike.UserId.ShouldBe(dto.UserId);
            addedLike.Id.ShouldNotBe(Guid.Empty);
            addedLike.LikedAt.ShouldBeInRange(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        }

        [Theory]
        [InlineData("22222222-2222-2222-2222-222222222222", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", true)]  
        [InlineData("11111111-1111-1111-1111-111111111111", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", false)] 
        public async Task AddLikeAsync_ReturnsCorrectResult_ForVariousScenarios(string commentIdStr, string userIdStr, bool expectedResult)
        {
            // Arrange
            var commentId = Guid.Parse(commentIdStr);
            var userId = Guid.Parse(userIdStr);
            var dto = new LikeCommentDto { CommentId = commentId, UserId = userId };
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            var result = await _service.AddLikeAsync(dto);

            // Assert
            result.ShouldBe(expectedResult);
        }

        [Fact]
        public async Task AddLikeAsync_InvalidatesCommentListCache()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId2, UserId = UserId2 };
            var mockKeys = new List<string> { "comment:list:1", "comment:list:2" };
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();
            _cacheServiceMock
                .Setup(x => x.GetSetMembersAsync("comments:list:keys"))
                .ReturnsAsync(mockKeys);

            // Act
            await _service.AddLikeAsync(dto);

            // Assert
            _cacheServiceMock.Verify(x => x.GetSetMembersAsync("comments:list:keys"), Times.Once);
            foreach (var key in mockKeys)
            {
                _cacheServiceMock.Verify(x => x.RemoveDataAsync(key), Times.Once);
            }
            _cacheServiceMock.Verify(x => x.ClearSetAsync("comments:list:keys"), Times.Once);
        }

        #endregion

        #region RemoveLikeAsync Tests

        [Fact]
        public async Task RemoveLikeAsync_ReturnsTrue_WhenLikeRemovedSuccessfully()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId1 }; 
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            var result = await _service.RemoveLikeAsync(dto);

            // Assert
            result.ShouldBeTrue();

            var likeExists = await _context.CommentLikes
                .AnyAsync(l => l.CommentId == dto.CommentId && l.UserId == dto.UserId);
            likeExists.ShouldBeFalse();

            VerifyCacheInvalidation(dto.CommentId, dto.UserId);
        }

        [Fact]
        public async Task RemoveLikeAsync_ReturnsFalse_WhenLikeDoesNotExist()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = nonExistentUserId };
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            var result = await _service.RemoveLikeAsync(dto);

            // Assert
            result.ShouldBeFalse();

            _cacheServiceMock.Verify(x => x.RemoveDataAsync(It.IsAny<string>()), Times.Never);
            _cacheServiceMock.Verify(x => x.GetSetMembersAsync(It.IsAny<string>()), Times.Never);
            _cacheServiceMock.Verify(x => x.ClearSetAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RemoveLikeAsync_ReturnsFalse_WhenCommentHasNoLikes()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId2, UserId = UserId2 }; 
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            var result = await _service.RemoveLikeAsync(dto);

            // Assert
            result.ShouldBeFalse();

            _cacheServiceMock.Verify(x => x.RemoveDataAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RemoveLikeAsync_PassesCancellationToken_ToDbOperations()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId1 };
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            var result = await _service.RemoveLikeAsync(dto, cancellationToken);

            // Assert
            result.ShouldBeTrue();
        }

        [Fact]
        public async Task RemoveLikeAsync_ThrowsOperationCanceled_WhenCancellationRequested()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId1 };
            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act & Assert
            await Should.ThrowAsync<OperationCanceledException>(() =>
                _service.RemoveLikeAsync(dto, cancellationTokenSource.Token));
        }

        [Fact]
        public async Task RemoveLikeAsync_InvalidatesCorrectCacheKeys_WhenLikeRemoved()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId2 };
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            await _service.RemoveLikeAsync(dto);

            // Assert
            VerifyCacheInvalidation(dto.CommentId, dto.UserId);
        }

        [Fact]
        public async Task RemoveLikeAsync_InvalidatesParentCommentCache_WhenCommentHasParent()
        {
            // Arrange - add a like to CommentId3 (which has a parent)
            var addDto = new LikeCommentDto { CommentId = CommentId3, UserId = UserId2 };
            await _service.AddLikeAsync(addDto);

            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act 
            var result = await _service.RemoveLikeAsync(addDto);

            // Assert
            result.ShouldBeTrue();
            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{CommentId3}"), Times.Once);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{ParentCommentId}"), Times.Once);
        }

        [Theory]
        [InlineData("11111111-1111-1111-1111-111111111111", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", true)]  
        [InlineData("11111111-1111-1111-1111-111111111111", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", true)]  
        [InlineData("22222222-2222-2222-2222-222222222222", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", false)] 
        public async Task RemoveLikeAsync_ReturnsCorrectResult_ForVariousScenarios(string commentIdStr, string userIdStr, bool expectedResult)
        {
            // Arrange
            var commentId = Guid.Parse(commentIdStr);
            var userId = Guid.Parse(userIdStr);
            var dto = new LikeCommentDto { CommentId = commentId, UserId = userId };
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            var result = await _service.RemoveLikeAsync(dto);

            // Assert
            result.ShouldBe(expectedResult);
        }

        [Fact]
        public async Task RemoveLikeAsync_DecreasesLikeCount_WhenLikeRemoved()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId1 };
            var initialCount = await _context.CommentLikes
                .CountAsync(l => l.CommentId == dto.CommentId);
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();

            // Act
            var result = await _service.RemoveLikeAsync(dto);

            // Assert
            result.ShouldBeTrue();
            var finalCount = await _context.CommentLikes
                .CountAsync(l => l.CommentId == dto.CommentId);
            finalCount.ShouldBe(initialCount - 1);
        }

        [Fact]
        public async Task RemoveLikeAsync_InvalidatesCommentListCache()
        {
            // Arrange
            var dto = new LikeCommentDto { CommentId = CommentId1, UserId = UserId1 };
            var mockKeys = new List<string> { "comment:list:1", "comment:list:2" };
            _cacheServiceMock.Reset();
            SetupCacheForAddLike();
            _cacheServiceMock
                .Setup(x => x.GetSetMembersAsync("comments:list:keys"))
                .ReturnsAsync(mockKeys);

            // Act
            await _service.RemoveLikeAsync(dto);

            // Assert
            _cacheServiceMock.Verify(x => x.GetSetMembersAsync("comments:list:keys"), Times.Once);
            foreach (var key in mockKeys)
            {
                _cacheServiceMock.Verify(x => x.RemoveDataAsync(key), Times.Once);
            }
            _cacheServiceMock.Verify(x => x.ClearSetAsync("comments:list:keys"), Times.Once);
        }

        #endregion

        #region Helper Methods

        private void SetupCacheForAddLike()
        {
            _cacheServiceMock
                .Setup(x => x.RemoveDataAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _cacheServiceMock
                .Setup(x => x.GetSetMembersAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<string>());
            _cacheServiceMock
                .Setup(x => x.ClearSetAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        }

        private void VerifyCacheInvalidation(Guid commentId, Guid userId)
        {
            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:likes:count:{commentId}"), Times.Once);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:like:user:{commentId}:{userId}"), Times.Once);

            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{commentId}"), Times.Once);

            _cacheServiceMock.Verify(x => x.GetSetMembersAsync("comments:list:keys"), Times.Once);
            _cacheServiceMock.Verify(x => x.ClearSetAsync("comments:list:keys"), Times.Once);
        }

        #endregion
    }
}