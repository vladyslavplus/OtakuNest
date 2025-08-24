using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using OtakuNest.CommentService.Data;
using OtakuNest.CommentService.DTOs;
using OtakuNest.CommentService.Models;
using OtakuNest.CommentService.Parameters;
using OtakuNest.Common.Helpers;
using OtakuNest.Common.Services.Caching;
using OtakuNest.Contracts;
using Shouldly;

namespace OtakuNest.CommentService.Tests.Services
{
    public class CommentServiceTests : IDisposable
    {
        private readonly CommentDbContext _context;
        private readonly CommentService.Services.CommentService _service;
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private bool _disposed = false;

        private static readonly Guid CommentId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid CommentId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid CommentId3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid ParentCommentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid ProductId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid UserId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private const string CommentListKeysSet = "comments:list:keys";

        public CommentServiceTests()
        {
            _context = GetInMemoryDbContext();
            _cacheServiceMock = new Mock<IRedisCacheService>();
            _service = CreateService(_context);
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

        private CommentService.Services.CommentService CreateService(CommentDbContext context)
        {
            var sortHelper = new SortHelper<Comment>();

            var userClientMock = new Mock<IRequestClient<GetUsersByIdsRequest>>();

            userClientMock
                .Setup(c => c.GetResponse<GetUsersByIdsResponse>(
                    It.IsAny<GetUsersByIdsRequest>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<RequestTimeout>() 
                ))
                .ReturnsAsync(() =>
                {
                    var response = new GetUsersByIdsResponse(new List<UserShortInfo>
                    {
                        new UserShortInfo(UserId1, "User1"),
                        new UserShortInfo(UserId2, "User2")
                    });

                    return Mock.Of<Response<GetUsersByIdsResponse>>(r => r.Message == response);
                });

            _cacheServiceMock
                .Setup(x => x.GetDataAsync<PagedListCacheDto<CommentDto>>(It.IsAny<string>()))
                .ReturnsAsync((PagedListCacheDto<CommentDto>?)null);

            _cacheServiceMock
                .Setup(x => x.GetDataAsync<CommentDto>(It.IsAny<string>()))
                .ReturnsAsync((CommentDto?)null);

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

            return new CommentService.Services.CommentService(
                context,
                sortHelper,
                userClientMock.Object,
                _cacheServiceMock.Object
            );
        }

        #region GetAllAsync Tests

        [Fact]
        public async Task GetAllAsync_ThrowsArgumentException_WhenProductIdIsEmpty()
        {
            // Arrange
            var parameters = new CommentParameters { ProductId = Guid.Empty };

            // Act & Assert
            await Should.ThrowAsync<ArgumentException>(() => _service.GetAllAsync(parameters));
        }

        [Fact]
        public async Task GetAllAsync_HandlesLargePageSize_LimitsToMaximumAllowed()
        {
            // Arrange
            await AddAndSaveAsync(
                CreateComment("Comment 1"),
                CreateComment("Comment 2"),
                CreateComment("Comment 3")
            );

            var parameters = new CommentParameters
            {
                ProductId = ProductId,
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            // Act
            var result = await _service.GetAllAsync(parameters);

            // Assert
            result.ShouldNotBeNull();
            result.PageSize.ShouldBeLessThanOrEqualTo(100);
        }

        [Fact]
        public async Task GetAllAsync_HandlesVeryLongContentFilter_ProcessesCorrectly()
        {
            // Arrange
            var longContent = new string('a', 10000); 
            await AddAndSaveAsync(CreateComment($"Contains {longContent} text"));

            var parameters = new CommentParameters
            {
                ProductId = ProductId,
                Content = longContent.Substring(0, 50), 
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllAsync(parameters);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);
        }

        [Fact]
        public async Task GetAllAsync_HandlesUnicodeContent_ProcessesCorrectly()
        {
            // Arrange
            await AddAndSaveAsync(
                CreateComment("Привіт 🌟 тестовий коментар"),
                CreateComment("Hello world"),
                CreateComment("测试评论 中文")
            );

            var parameters = new CommentParameters
            {
                ProductId = ProductId,
                Content = "🌟",
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllAsync(parameters);

            // Assert
            result.ShouldHaveSingleItem();
            result[0].Content.ShouldContain("🌟");
        }

        [Fact]
        public async Task GetAllAsync_HandlesPageNumberBeyondAvailableData_ReturnsEmptyResults()
        {
            // Arrange
            await AddAndSaveAsync(
                CreateComment("Comment 1"),
                CreateComment("Comment 2")
            );

            var parameters = new CommentParameters
            {
                ProductId = ProductId,
                PageNumber = 1000, 
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllAsync(parameters);

            // Assert
            result.ShouldBeEmpty();
            result.CurrentPage.ShouldBe(1000);
            result.TotalPages.ShouldBe(1); 
        }

        [Fact]
        public async Task GetAllAsync_ReturnsCachedValue_WhenCacheExists()
        {
            // Arrange
            var expectedDto = new CommentDto { Id = CommentId1, Content = "From cache" };

            var cached = new PagedListCacheDto<CommentDto>
            {
                Items = new List<CommentDto> { expectedDto },
                TotalCount = 1,
                PageNumber = 1,
                PageSize = 10
            };

            _cacheServiceMock
                .Setup(x => x.GetDataAsync<PagedListCacheDto<CommentDto>>(It.IsAny<string>()))
                .ReturnsAsync(cached);

            var parameters = new CommentParameters { ProductId = ProductId, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllAsync(parameters);

            // Assert
            result.ShouldHaveSingleItem();
            result[0].Content.ShouldBe("From cache");

            _cacheServiceMock.Verify(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()), Times.Never);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsDbValue_WhenCacheIsEmpty()
        {
            // Arrange
            await AddAndSaveAsync(CreateComment("Db comment", userId: UserId1));

            var parameters = new CommentParameters { ProductId = ProductId, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllAsync(parameters);

            // Assert
            result.ShouldContain(x => x.Content == "Db comment");
            _cacheServiceMock.Verify(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()), Times.Once);
            _cacheServiceMock.Verify(x => x.AddToSetAsync("comments:list:keys", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_AppliesContentFilter()
        {
            // Arrange
            await AddAndSaveAsync(
                CreateComment("Hello world", userId: UserId1),
                CreateComment("Other text", userId: UserId2)
            );

            var parameters = new CommentParameters { ProductId = ProductId, Content = "world", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllAsync(parameters);

            // Assert
            result.Count.ShouldBe(1);
            result[0].Content.ShouldContain("world");
        }

        [Fact]
        public async Task GetAllAsync_ReturnsPagedResults_WhenMultipleCommentsExist()
        {
            // Arrange
            for (int i = 0; i < 15; i++)
            {
                _context.Comments.Add(CreateComment($"Comment {i}", createdAt: DateTime.UtcNow.AddMinutes(-i)));
            }
            await _context.SaveChangesAsync();

            var parameters = new CommentParameters { ProductId = ProductId, PageNumber = 2, PageSize = 5 };

            // Act
            var result = await _service.GetAllAsync(parameters);

            // Assert
            result.PageSize.ShouldBe(5);
            result.CurrentPage.ShouldBe(2);
            result.Count.ShouldBe(5);
        }

        [Fact]
        public async Task GetAllAsync_MapsRepliesCorrectly()
        {
            // Arrange
            var parent = CreateComment("Parent", userId: UserId1);
            var child = CreateComment("Child reply", parentId: parent.Id, userId: UserId2, createdAt: DateTime.UtcNow.AddMinutes(1));

            await AddAndSaveAsync(parent, child);

            var parameters = new CommentParameters { ProductId = ProductId, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllAsync(parameters);

            // Assert
            var parentDto = result[0];
            parentDto.Replies.ShouldHaveSingleItem();
            parentDto.Replies[0].Content.ShouldBe("Child reply");
        }

        [Fact]
        public async Task GetAllAsync_ThrowsOperationCanceled_WhenTokenCancelled()
        {
            // Arrange
            var parameters = new CommentParameters { ProductId = ProductId, PageNumber = 1, PageSize = 10 };
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Should.ThrowAsync<OperationCanceledException>(() => _service.GetAllAsync(parameters, cts.Token));
        }

        [Fact]
        public async Task GetAllAsync_AppliesOrderBy()
        {
            // Arrange
            await AddAndSaveAsync(
                CreateComment("B comment", createdAt: DateTime.UtcNow.AddMinutes(-2)),
                CreateComment("A comment", createdAt: DateTime.UtcNow.AddMinutes(-1))
            );

            var parameters = new CommentParameters { ProductId = ProductId, OrderBy = "Content", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllAsync(parameters);

            // Assert
            result[0].Content.ShouldBe("A comment");
            result[1].Content.ShouldBe("B comment");
        }

        [Theory]
        [InlineData(1, 10)]
        [InlineData(2, 10)]
        [InlineData(1, 5)]
        public async Task GetAllAsync_DifferentParametersHaveDifferentCacheKeys_Theory(int pageNumber, int pageSize)
        {
            // Arrange
            await AddAndSaveAsync(CreateComment("Test"));

            var parameters = new CommentParameters
            {
                ProductId = ProductId,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            // Act
            await _service.GetAllAsync(parameters);

            // Assert
            _cacheServiceMock.Verify(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetAllAsync_MapsLikesCountCorrectly()
        {
            // Arrange
            var comment = CreateComment("With likes");
            comment.Likes.Add(new CommentLike { Id = Guid.NewGuid(), UserId = UserId1, CommentId = comment.Id, LikedAt = DateTime.UtcNow });
            await AddAndSaveAsync(comment);

            var parameters = new CommentParameters { ProductId = ProductId, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllAsync(parameters);

            // Assert
            result[0].LikesCount.ShouldBe(1);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsEmptyList_WhenNoCommentsExist()
        {
            // Arrange
            var parameters = new CommentParameters { ProductId = Guid.NewGuid(), PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllAsync(parameters);

            // Assert
            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetAllAsync_WithThousandComments_CompletesWithinTimeout()
        {
            // Arrange
            var comments = new List<Comment>();
            for (int i = 1; i <= 1000; i++)
            {
                comments.Add(CreateComment($"Performance test comment {i}",
                    createdAt: DateTime.UtcNow.AddMinutes(-i)));
            }

            await AddAndSaveAsync(comments.ToArray());

            var parameters = new CommentParameters
            {
                ProductId = ProductId,
                PageNumber = 1,
                PageSize = 50
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _service.GetAllAsync(parameters, cts.Token);
            stopwatch.Stop();

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(50);
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000);
        }

        [Fact]
        public async Task GetAllAsync_WithComplexNesting_CompletesWithinTimeout()
        {
            // Arrange 
            var rootComments = new List<Comment>();

            for (int i = 1; i <= 100; i++)
            {
                var parent = CreateComment($"Root comment {i}");
                rootComments.Add(parent);

                for (int j = 1; j <= 5; j++)
                {
                    var child = CreateComment($"Child {j} of root {i}", parent.Id,
                        createdAt: DateTime.UtcNow.AddMinutes(j));
                    rootComments.Add(child);

                    for (int k = 1; k <= 2; k++)
                    {
                        var grandChild = CreateComment($"Grandchild {k} of child {j}", child.Id,
                            createdAt: DateTime.UtcNow.AddMinutes(j * 10 + k));
                        rootComments.Add(grandChild);
                    }
                }
            }

            await AddAndSaveAsync(rootComments.ToArray());

            var parameters = new CommentParameters
            {
                ProductId = ProductId,
                PageNumber = 1,
                PageSize = 20
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _service.GetAllAsync(parameters, cts.Token);
            stopwatch.Stop();

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(20);
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10000); 

            var firstComment = result.FirstOrDefault();
            if (firstComment?.Replies?.Any() == true)
            {
                firstComment.Replies.Count.ShouldBeGreaterThan(0);
            }
        }

        #endregion

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_ReturnsCachedComment_WhenCacheExists()
        {
            // Arrange
            var cached = new CommentDto { Id = CommentId1, Content = "Cached comment" };
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<CommentDto>($"comment:{CommentId1}"))
                .ReturnsAsync(cached);

            // Act
            var result = await _service.GetByIdAsync(CommentId1);

            // Assert
            result.ShouldNotBeNull();
            result.Content.ShouldBe("Cached comment");
            _cacheServiceMock.Verify(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()), Times.Never);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNull_WhenCommentNotFound()
        {
            // Arrange
            var missingId = Guid.NewGuid();
            _cacheServiceMock
                .Setup(x => x.GetDataAsync<CommentDto>($"comment:{missingId}"))
                .ReturnsAsync((CommentDto?)null);

            // Act
            var result = await _service.GetByIdAsync(missingId);

            // Assert
            result.ShouldBeNull();
            _cacheServiceMock.Verify(x => x.SetDataAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()), Times.Never);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsDbCommentAndCachesIt_WhenNotCached()
        {
            // Arrange
            var comment = CreateComment("DB comment", userId: UserId1);
            await AddAndSaveAsync(comment);

            _cacheServiceMock
                .Setup(x => x.GetDataAsync<CommentDto>($"comment:{comment.Id}"))
                .ReturnsAsync((CommentDto?)null);

            // Act
            var result = await _service.GetByIdAsync(comment.Id);

            // Assert
            result.ShouldNotBeNull();
            result.Content.ShouldBe("DB comment");
            _cacheServiceMock.Verify(x => x.SetDataAsync($"comment:{comment.Id}", It.IsAny<CommentDto>(), TimeSpan.FromMinutes(30)), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_MapsRepliesAndLikesCorrectly()
        {
            // Arrange
            var parent = CreateComment("Parent", userId: UserId1);
            var child = CreateComment("Child reply", parentId: parent.Id, userId: UserId2, createdAt: DateTime.UtcNow.AddMinutes(1));

            await AddAndSaveAsync(parent, child);

            // Act
            var result = await _service.GetByIdAsync(parent.Id);

            // Assert
            result.ShouldNotBeNull();
            result.Replies.ShouldHaveSingleItem();
            result.Replies[0].Content.ShouldBe("Child reply");
        }

        [Fact]
        public async Task GetByIdAsync_ThrowsOperationCanceledException_WhenTokenCancelled()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Should.ThrowAsync<OperationCanceledException>(() => _service.GetByIdAsync(CommentId1, cts.Token));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetByIdAsync_CacheVsDbBehavior(bool isCached)
        {
            // Arrange
            var comment = CreateComment("Test comment", userId: UserId1);
            if (!isCached)
                await AddAndSaveAsync(comment);

            _cacheServiceMock
                .Setup(x => x.GetDataAsync<CommentDto>($"comment:{comment.Id}"))
                .ReturnsAsync(isCached ? new CommentDto { Id = comment.Id, Content = "Cached" } : null);

            // Act
            var result = await _service.GetByIdAsync(comment.Id);

            // Assert
            if (isCached)
                result!.Content.ShouldBe("Cached");
            else
                result!.Content.ShouldBe("Test comment");
        }

        [Fact]
        public async Task GetByIdAsync_CachesCommentAfterDbFetch()
        {
            // Arrange
            var comment = CreateComment("DB comment");
            await AddAndSaveAsync(comment);

            _cacheServiceMock
                .Setup(x => x.GetDataAsync<CommentDto>($"comment:{comment.Id}"))
                .ReturnsAsync((CommentDto?)null);

            // Act
            var firstCall = await _service.GetByIdAsync(comment.Id);

            _cacheServiceMock
                .Setup(x => x.GetDataAsync<CommentDto>($"comment:{comment.Id}"))
                .ReturnsAsync(firstCall);

            var secondCall = await _service.GetByIdAsync(comment.Id);

            // Assert
            firstCall.ShouldNotBeNull();
            secondCall.ShouldNotBeNull();
            secondCall.ShouldBe(firstCall);
            _cacheServiceMock.Verify(x => x.SetDataAsync($"comment:{comment.Id}", It.IsAny<CommentDto>(), TimeSpan.FromMinutes(30)), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_HandlesCommentWithoutLikes()
        {
            // Arrange
            var comment = CreateComment("No likes comment");
            await AddAndSaveAsync(comment);

            // Act
            var result = await _service.GetByIdAsync(comment.Id);

            // Assert
            result.ShouldNotBeNull();
            result.LikesCount.ShouldBe(0);
        }

        [Fact]
        public async Task GetByIdAsync_MapsLikesCorrectly()
        {
            // Arrange
            var comment = CreateComment("Comment with likes");
            await AddAndSaveAsync(comment);

            _context.CommentLikes.AddRange(
                new CommentLike { Id = Guid.NewGuid(), CommentId = comment.Id, UserId = UserId1, LikedAt = DateTime.UtcNow },
                new CommentLike { Id = Guid.NewGuid(), CommentId = comment.Id, UserId = UserId2, LikedAt = DateTime.UtcNow }
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetByIdAsync(comment.Id);

            // Assert
            result.ShouldNotBeNull();
            result.LikesCount.ShouldBe(2);
        }

        [Fact]
        public async Task GetByIdAsync_MapsNestedRepliesCorrectly()
        {
            // Arrange
            var parent = CreateComment("Parent");
            var child = CreateComment("Child reply", parentId: parent.Id);
            var grandChild = CreateComment("Grandchild reply", parentId: child.Id);
            await AddAndSaveAsync(parent, child, grandChild);

            // Act
            var result = await _service.GetByIdAsync(parent.Id);

            // Assert
            result.ShouldNotBeNull();
            result.Replies.ShouldHaveSingleItem();
            result.Replies[0].Replies.ShouldHaveSingleItem();
            result.Replies[0].Replies[0].Content.ShouldBe("Grandchild reply");
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_ThrowsArgumentException_WhenContentIsEmpty()
        {
            // Arrange
            var dto = new CreateCommentDto { ProductId = ProductId, Content = " " };

            // Act & Assert
            await Should.ThrowAsync<ArgumentException>(() => _service.CreateAsync(dto, UserId1));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAsync_ThrowsArgumentException_ForInvalidContent(string content)
        {
            // Arrange
            var dto = new CreateCommentDto { ProductId = ProductId, Content = content };

            // Act & Assert
            await Should.ThrowAsync<ArgumentException>(() => _service.CreateAsync(dto, UserId1));
        }

        [Fact]
        public async Task CreateAsync_CreatesCommentAndReturnsDto()
        {
            // Arrange
            var dto = new CreateCommentDto { ProductId = ProductId, Content = "New comment" };

            // Act
            var result = await _service.CreateAsync(dto, UserId1);

            // Assert
            result.ShouldNotBeNull();
            result.Content.ShouldBe("New comment");
            result.UserId.ShouldBe(UserId1);
            result.ProductId.ShouldBe(ProductId);

            var savedComment = await _context.Comments.FindAsync(result.Id);
            savedComment.ShouldNotBeNull();
            savedComment.Content.ShouldBe("New comment");
        }

        [Fact]
        public async Task CreateAsync_SetsCommentInCache()
        {
            // Arrange
            var dto = new CreateCommentDto { ProductId = ProductId, Content = "Cache test" };

            // Act
            var result = await _service.CreateAsync(dto, UserId1);

            // Assert
            _cacheServiceMock.Verify(x => x.SetDataAsync(
                $"comment:{result.Id}", It.IsAny<CommentDto>(), TimeSpan.FromMinutes(30)), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_InvalidatesListCache()
        {
            // Arrange
            var dto = new CreateCommentDto { ProductId = ProductId, Content = "Invalidate test" };

            _cacheServiceMock
                .Setup(x => x.GetSetMembersAsync(CommentListKeysSet))
                .ReturnsAsync(new List<string> { "key1", "key2" });

            // Act
            await _service.CreateAsync(dto, UserId1);

            // Assert
            _cacheServiceMock.Verify(x => x.RemoveDataAsync("key1"), Times.Once);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync("key2"), Times.Once);
            _cacheServiceMock.Verify(x => x.ClearSetAsync(CommentListKeysSet), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_MapsRepliesCorrectly()
        {
            // Arrange
            var parent = CreateComment("Parent comment");
            await AddAndSaveAsync(parent);

            var dto = new CreateCommentDto { ProductId = ProductId, Content = "Child comment" };

            // Act
            var result = await _service.CreateAsync(dto, UserId1);

            // Assert
            result.ShouldNotBeNull();
            result.Replies.ShouldBeEmpty(); 
        }

        [Theory]
        [InlineData("First comment")]
        [InlineData("Second comment")]
        [InlineData("Another comment")]
        public async Task CreateAsync_CreatesComment_ForMultipleContents(string content)
        {
            // Arrange
            var dto = new CreateCommentDto { ProductId = ProductId, Content = content };

            // Act
            var result = await _service.CreateAsync(dto, UserId2);

            // Assert
            result.ShouldNotBeNull();
            result.Content.ShouldBe(content);
            result.UserId.ShouldBe(UserId2);

            var saved = await _context.Comments.FindAsync(result.Id);
            saved.ShouldNotBeNull();
            saved.Content.ShouldBe(content);
        }

        [Fact]
        public async Task CreateAsync_WithParentId_SetsParentCorrectly()
        {
            // Arrange
            var parent = CreateComment("Parent comment");
            await AddAndSaveAsync(parent);

            var dto = new CreateCommentDto
            {
                ProductId = ProductId,
                Content = "Child comment"
            };

            // Act
            var result = await _service.CreateAsync(dto, UserId1);

            // Assert
            result.ParentCommentId.ShouldBeNull(); 
        }

        [Fact]
        public async Task CreateAsync_CachesCommentAndInvalidatesList_ForMultipleComments()
        {
            // Arrange
            _cacheServiceMock
                .Setup(x => x.GetSetMembersAsync(CommentListKeysSet))
                .ReturnsAsync(new List<string> { "keyA", "keyB" });

            var dto1 = new CreateCommentDto { ProductId = ProductId, Content = "Comment 1" };
            var dto2 = new CreateCommentDto { ProductId = ProductId, Content = "Comment 2" };

            // Act
            var result1 = await _service.CreateAsync(dto1, UserId1);
            var result2 = await _service.CreateAsync(dto2, UserId2);

            // Assert
            _cacheServiceMock.Verify(x => x.SetDataAsync($"comment:{result1.Id}", It.IsAny<CommentDto>(), TimeSpan.FromMinutes(30)), Times.Once);
            _cacheServiceMock.Verify(x => x.SetDataAsync($"comment:{result2.Id}", It.IsAny<CommentDto>(), TimeSpan.FromMinutes(30)), Times.Once);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync("keyA"), Times.Exactly(2));
            _cacheServiceMock.Verify(x => x.RemoveDataAsync("keyB"), Times.Exactly(2));
            _cacheServiceMock.Verify(x => x.ClearSetAsync(CommentListKeysSet), Times.Exactly(2));
        }

        [Fact]
        public async Task CreateAsync_ThrowsOperationCanceled_WhenTokenCancelled()
        {
            // Arrange
            var dto = new CreateCommentDto { ProductId = ProductId, Content = "Cancel test" };
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Should.ThrowAsync<OperationCanceledException>(() => _service.CreateAsync(dto, UserId1, cts.Token));
        }

        [Fact]
        public async Task CreateAsync_BulkCreation_CompletesWithinTimeout()
        {
            // Arrange
            var createTasks = new List<Task<CommentDto>>();

            for (int i = 1; i <= 100; i++)
            {
                var dto = new CreateCommentDto
                {
                    ProductId = ProductId,
                    Content = $"Bulk create comment {i}"
                };
                createTasks.Add(_service.CreateAsync(dto, UserId1));
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var results = await Task.WhenAll(createTasks);
            stopwatch.Stop();

            // Assert
            results.Length.ShouldBe(100);
            results.All(r => r != null).ShouldBeTrue();
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(20000);

            var uniqueIds = results.Select(r => r.Id).Distinct().Count();
            uniqueIds.ShouldBe(100);
        }

        #endregion

        #region ReplyAsync Tests

        [Fact]
        public async Task ReplyAsync_ThrowsKeyNotFound_WhenParentDoesNotExist()
        {
            // Arrange
            var dto = new ReplyToCommentDto { ProductId = ProductId, ParentCommentId = Guid.NewGuid(), Content = "Reply" };

            // Act & Assert
            await Should.ThrowAsync<KeyNotFoundException>(() => _service.ReplyAsync(dto, UserId1));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ReplyAsync_ThrowsArgumentException_WhenContentInvalid(string content)
        {
            // Arrange
            var parent = CreateComment("Parent comment");
            await AddAndSaveAsync(parent);

            var dto = new ReplyToCommentDto { ProductId = ProductId, ParentCommentId = parent.Id, Content = content };

            // Act & Assert
            await Should.ThrowAsync<ArgumentException>(() => _service.ReplyAsync(dto, UserId1));
        }

        [Fact]
        public async Task ReplyAsync_CreatesReplyAndReturnsDto()
        {
            // Arrange
            var parent = CreateComment("Parent comment");
            await AddAndSaveAsync(parent);

            var dto = new ReplyToCommentDto { ProductId = ProductId, ParentCommentId = parent.Id, Content = "Child reply" };

            // Act
            var result = await _service.ReplyAsync(dto, UserId2);

            // Assert
            result.ShouldNotBeNull();
            result.Content.ShouldBe("Child reply");
            result.ParentCommentId.ShouldBe(parent.Id);
            result.UserId.ShouldBe(UserId2);

            var saved = await _context.Comments.FindAsync(result.Id);
            saved.ShouldNotBeNull();
            saved.Content.ShouldBe("Child reply");
            saved.ParentCommentId.ShouldBe(parent.Id);
        }

        [Fact]
        public async Task ReplyAsync_SetsReplyInCache()
        {
            // Arrange
            var parent = CreateComment("Parent comment");
            await AddAndSaveAsync(parent);

            var dto = new ReplyToCommentDto { ProductId = ProductId, ParentCommentId = parent.Id, Content = "Cache reply" };

            // Act
            var result = await _service.ReplyAsync(dto, UserId1);

            // Assert
            _cacheServiceMock.Verify(x => x.SetDataAsync(
                $"comment:{result.Id}", It.IsAny<CommentDto>(), TimeSpan.FromMinutes(30)), Times.Once);
        }

        [Fact]
        public async Task ReplyAsync_InvalidatesListCache()
        {
            // Arrange
            var parent = CreateComment("Parent comment");
            await AddAndSaveAsync(parent);

            _cacheServiceMock.Setup(x => x.GetSetMembersAsync(CommentListKeysSet))
                .ReturnsAsync(new List<string> { "key1", "key2" });

            var dto = new ReplyToCommentDto { ProductId = ProductId, ParentCommentId = parent.Id, Content = "Invalidate reply" };

            // Act
            await _service.ReplyAsync(dto, UserId2);

            // Assert
            _cacheServiceMock.Verify(x => x.RemoveDataAsync("key1"), Times.Once);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync("key2"), Times.Once);
            _cacheServiceMock.Verify(x => x.ClearSetAsync(CommentListKeysSet), Times.Once);
        }

        [Fact]
        public async Task ReplyAsync_MapsRepliesCorrectly()
        {
            // Arrange
            var parent = CreateComment("Parent comment");
            await AddAndSaveAsync(parent);

            var dto = new ReplyToCommentDto { ProductId = ProductId, ParentCommentId = parent.Id, Content = "Child reply" };

            // Act
            var result = await _service.ReplyAsync(dto, UserId1);

            // Assert
            result.ShouldNotBeNull();
            result.Replies.ShouldBeEmpty(); 
        }

        [Fact]
        public async Task ReplyAsync_WithNestedReplies_MapsRepliesRecursively()
        {
            // Arrange
            var parent = CreateComment("Parent comment");
            await AddAndSaveAsync(parent);

            var childDto = new ReplyToCommentDto
            {
                ProductId = ProductId,
                ParentCommentId = parent.Id,
                Content = "Child reply"
            };
            var childResult = await _service.ReplyAsync(childDto, UserId1);

            var grandChildDto = new ReplyToCommentDto
            {
                ProductId = ProductId,
                ParentCommentId = childResult.Id,
                Content = "Grandchild reply"
            };
            var grandChildResult = await _service.ReplyAsync(grandChildDto, UserId2);

            // Act
            var finalParent = await _service.GetByIdAsync(parent.Id);

            // Assert
            finalParent.ShouldNotBeNull();
            finalParent.Replies.Count.ShouldBe(1); 
            finalParent.Replies[0].Content.ShouldBe("Child reply");

            var childReplies = finalParent.Replies[0].Replies;
            childReplies.Count.ShouldBe(1); 
            childReplies[0].Content.ShouldBe("Grandchild reply");
            childReplies[0].Id.ShouldBe(grandChildResult.Id); 
        }

        [Fact]
        public async Task ReplyAsync_ThrowsOperationCanceled_WhenTokenCancelled()
        {
            // Arrange
            var parent = CreateComment("Parent comment");
            await AddAndSaveAsync(parent);

            var dto = new ReplyToCommentDto { ProductId = ProductId, ParentCommentId = parent.Id, Content = "Cancel test" };
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Should.ThrowAsync<OperationCanceledException>(() => _service.ReplyAsync(dto, UserId1, cts.Token));
        }

        [Theory]
        [InlineData("First reply")]
        [InlineData("Second reply")]
        [InlineData("Another reply")]
        public async Task ReplyAsync_CreatesMultipleReplies_ForSameParent(string content)
        {
            // Arrange
            var parent = CreateComment("Parent comment");
            await AddAndSaveAsync(parent);

            var dto = new ReplyToCommentDto { ProductId = ProductId, ParentCommentId = parent.Id, Content = content };

            // Act
            var result = await _service.ReplyAsync(dto, UserId2);

            // Assert
            result.ShouldNotBeNull();
            result.Content.ShouldBe(content);
            result.ParentCommentId.ShouldBe(parent.Id);

            var saved = await _context.Comments.FindAsync(result.Id);
            saved.ShouldNotBeNull();
            saved.Content.ShouldBe(content);
            saved.ParentCommentId.ShouldBe(parent.Id);
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_ReturnsFalse_WhenCommentNotFound()
        {
            // Arrange
            var dto = new UpdateCommentDto { Content = "Updated content" };
            var nonExistingId = Guid.NewGuid();

            // Act
            var result = await _service.UpdateAsync(nonExistingId, UserId1, dto);

            // Assert
            result.ShouldBeFalse();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task UpdateAsync_ThrowsArgumentException_WhenContentInvalid(string content)
        {
            // Arrange
            var comment = CreateComment("Original content", UserId1);
            await AddAndSaveAsync(comment);

            var dto = new UpdateCommentDto { Content = content };

            // Act & Assert
            await Should.ThrowAsync<ArgumentException>(() => _service.UpdateAsync(comment.Id, UserId1, dto));
        }

        [Fact]
        public async Task UpdateAsync_SuccessfullyUpdatesComment()
        {
            // Arrange
            var comment = CreateComment("Original content", UserId1);
            await AddAndSaveAsync(comment);

            var dto = new UpdateCommentDto { Content = " Updated content " };

            // Act
            var result = await _service.UpdateAsync(comment.Id, UserId1, dto);

            // Assert
            result.ShouldBeTrue();

            var saved = await _context.Comments.FindAsync(comment.Id);
            saved.ShouldNotBeNull();
            saved.Content.ShouldBe("Updated content");
            saved.UpdatedAt.ShouldNotBeNull();
        }

        [Fact]
        public async Task UpdateAsync_UpdatesComment_WithNestedReplies_MapsRepliesRecursively()
        {
            // Arrange
            var parent = CreateComment("Parent comment", UserId1);
            await AddAndSaveAsync(parent);

            var child = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = parent.Id,
                UserId = UserId2,
                Content = "Child comment",
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(child);
            await _context.SaveChangesAsync();

            var dto = new UpdateCommentDto { Content = "Updated parent comment" };

            // Act
            var result = await _service.UpdateAsync(parent.Id, UserId1, dto);

            var updatedParent = await _service.GetByIdAsync(parent.Id);

            // Assert
            result.ShouldBeTrue();
            updatedParent.ShouldNotBeNull();
            updatedParent.Content.ShouldBe("Updated parent comment");

            updatedParent.Replies.Count.ShouldBe(1);
            updatedParent.Replies[0].Content.ShouldBe("Child comment");
            updatedParent.Replies[0].UserId.ShouldBe(UserId2);
        }

        [Fact]
        public async Task UpdateAsync_UpdatesParent_WithMultipleNestedReplies_MapsAllRepliesCorrectly()
        {
            // Arrange
            var parent = CreateComment("Parent comment", UserId1);
            await AddAndSaveAsync(parent);

            var child1 = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = parent.Id,
                UserId = UserId2,
                Content = "Child 1",
                CreatedAt = DateTime.UtcNow
            };
            var child2 = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = parent.Id,
                UserId = UserId2,
                Content = "Child 2",
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.AddRange(child1, child2);
            await _context.SaveChangesAsync();

            var grandChild = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = child1.Id,
                UserId = UserId1,
                Content = "Grandchild 1",
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(grandChild);
            await _context.SaveChangesAsync();

            var dto = new UpdateCommentDto { Content = "Updated Parent" };

            // Act
            var result = await _service.UpdateAsync(parent.Id, UserId1, dto);

            var updatedParent = await _service.GetByIdAsync(parent.Id);

            // Assert
            result.ShouldBeTrue();
            updatedParent.ShouldNotBeNull();
            updatedParent.Content.ShouldBe("Updated Parent");

            updatedParent.Replies.Count.ShouldBe(2);
            var child1Dto = updatedParent.Replies.First(c => c.Id == child1.Id);
            var child2Dto = updatedParent.Replies.First(c => c.Id == child2.Id);

            child1Dto.Content.ShouldBe("Child 1");
            child2Dto.Content.ShouldBe("Child 2");

            child1Dto.Replies.Count.ShouldBe(1);
            child1Dto.Replies[0].Content.ShouldBe("Grandchild 1");
            child1Dto.Replies[0].Id.ShouldBe(grandChild.Id);

            child2Dto.Replies.ShouldBeEmpty();
        }

        [Fact]
        public async Task UpdateAsync_SetsUpdatedCommentInCache_AndInvalidatesListCache()
        {
            // Arrange
            var comment = CreateComment("Original content", UserId1);
            await AddAndSaveAsync(comment);

            _cacheServiceMock
                .Setup(x => x.GetSetMembersAsync(CommentListKeysSet))
                .ReturnsAsync(new List<string> { "key1", "key2" });

            var dto = new UpdateCommentDto { Content = "Updated content" };

            // Act
            var result = await _service.UpdateAsync(comment.Id, UserId1, dto);

            // Assert
            result.ShouldBeTrue();

            _cacheServiceMock.Verify(x => x.SetDataAsync(
                $"comment:{comment.Id}", It.IsAny<CommentDto>(), TimeSpan.FromMinutes(30)), Times.Once);

            _cacheServiceMock.Verify(x => x.RemoveDataAsync("key1"), Times.Once);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync("key2"), Times.Once);
            _cacheServiceMock.Verify(x => x.ClearSetAsync(CommentListKeysSet), Times.Once);
        }

        [Theory]
        [InlineData("New content 1")]
        [InlineData("Another updated comment")]
        public async Task UpdateAsync_UpdatesComment_WithDifferentContents(string content)
        {
            // Arrange
            var comment = CreateComment("Original content", UserId1);
            await AddAndSaveAsync(comment);

            var dto = new UpdateCommentDto { Content = content };

            // Act
            var result = await _service.UpdateAsync(comment.Id, UserId1, dto);

            // Assert
            result.ShouldBeTrue();

            var saved = await _context.Comments.FindAsync(comment.Id);
            saved.ShouldNotBeNull();
            saved.Content.ShouldBe(content);
        }

        [Fact]
        public async Task UpdateAsync_ThrowsOperationCanceled_WhenTokenCancelled()
        {
            // Arrange
            var comment = CreateComment("Original content", UserId1);
            await AddAndSaveAsync(comment);

            var dto = new UpdateCommentDto { Content = "Cancel test" };
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Should.ThrowAsync<OperationCanceledException>(() =>
                _service.UpdateAsync(comment.Id, UserId1, dto, cts.Token));
        }

        #endregion

        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_ReturnsFalse_WhenCommentNotFound()
        {
            // Arrange
            var nonExistingId = Guid.NewGuid();

            // Act
            var result = await _service.DeleteAsync(nonExistingId, UserId1);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_RemovesSingleComment()
        {
            // Arrange
            var comment = CreateComment("Single comment", UserId1);
            await AddAndSaveAsync(comment);

            // Act
            var result = await _service.DeleteAsync(comment.Id, UserId1);

            // Assert
            result.ShouldBeTrue();
            (await _context.Comments.FindAsync(comment.Id)).ShouldBeNull();

            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{comment.Id}"), Times.Once);
            _cacheServiceMock.Verify(x => x.ClearSetAsync(CommentListKeysSet), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_RemovesCommentWithNestedReplies()
        {
            // Arrange
            var parent = CreateComment("Parent comment", UserId1);
            await AddAndSaveAsync(parent);

            var child1 = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = parent.Id,
                UserId = UserId2,
                Content = "Child 1",
                CreatedAt = DateTime.UtcNow
            };
            var child2 = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = parent.Id,
                UserId = UserId2,
                Content = "Child 2",
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.AddRange(child1, child2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.DeleteAsync(parent.Id, UserId1);

            // Assert
            result.ShouldBeTrue();

            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{parent.Id}"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{child1.Id}"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{child2.Id}"), Times.AtLeastOnce);

            _cacheServiceMock.Verify(x => x.GetSetMembersAsync(CommentListKeysSet), Times.Once);
            _cacheServiceMock.Verify(x => x.ClearSetAsync(CommentListKeysSet), Times.Once);

            (await _context.Comments.FindAsync(parent.Id)).ShouldBeNull();
            (await _context.Comments.FindAsync(child1.Id)).ShouldBeNull();
            (await _context.Comments.FindAsync(child2.Id)).ShouldBeNull();
        }

        [Fact]
        public async Task DeleteAsync_RemovesDeepNestedRepliesAndCaches()
        {
            // Arrange
            var parent = CreateComment("Parent comment", UserId1);
            await AddAndSaveAsync(parent);

            var child = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = parent.Id,
                UserId = UserId2,
                Content = "Child comment",
                CreatedAt = DateTime.UtcNow
            };
            var grandChild = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = child.Id,
                UserId = UserId1,
                Content = "Grandchild comment",
                CreatedAt = DateTime.UtcNow
            };
            var greatGrandChild = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = grandChild.Id,
                UserId = UserId2,
                Content = "Great-grandchild comment",
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.AddRange(child, grandChild, greatGrandChild);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.DeleteAsync(parent.Id, UserId1);

            // Assert
            result.ShouldBeTrue();
            (await _context.Comments.FindAsync(parent.Id)).ShouldBeNull();
            (await _context.Comments.FindAsync(child.Id)).ShouldBeNull();
            (await _context.Comments.FindAsync(grandChild.Id)).ShouldBeNull();
            (await _context.Comments.FindAsync(greatGrandChild.Id)).ShouldBeNull();

            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{parent.Id}"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{child.Id}"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{grandChild.Id}"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{greatGrandChild.Id}"), Times.AtLeastOnce);

            _cacheServiceMock.Verify(x => x.GetSetMembersAsync(CommentListKeysSet), Times.Once);
            _cacheServiceMock.Verify(x => x.ClearSetAsync(CommentListKeysSet), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_RemovesMultipleChildrenFromDifferentParents()
        {
            // Arrange
            var parent1 = CreateComment("Parent 1", UserId1);
            var parent2 = CreateComment("Parent 2", UserId1);
            await AddAndSaveAsync(parent1);
            await AddAndSaveAsync(parent2);

            var child1 = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = parent1.Id,
                UserId = UserId2,
                Content = "Child 1",
                CreatedAt = DateTime.UtcNow
            };
            var child2 = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = parent2.Id,
                UserId = UserId2,
                Content = "Child 2",
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.AddRange(child1, child2);
            await _context.SaveChangesAsync();

            // Act
            var result1 = await _service.DeleteAsync(parent1.Id, UserId1);
            var result2 = await _service.DeleteAsync(parent2.Id, UserId1);

            // Assert
            result1.ShouldBeTrue();
            result2.ShouldBeTrue();

            (await _context.Comments.FindAsync(parent1.Id)).ShouldBeNull();
            (await _context.Comments.FindAsync(child1.Id)).ShouldBeNull();

            (await _context.Comments.FindAsync(parent2.Id)).ShouldBeNull();
            (await _context.Comments.FindAsync(child2.Id)).ShouldBeNull();
        }

        [Fact]
        public async Task DeleteAsync_RemovesCacheForParentsOfDeletedReplies()
        {
            // Arrange
            var parent = CreateComment("Parent comment", UserId1);
            await AddAndSaveAsync(parent);

            var child = new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = parent.Id,
                UserId = UserId2,
                Content = "Child comment",
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(child);
            await _context.SaveChangesAsync();

            var result = await _service.DeleteAsync(parent.Id, UserId1);

            // Assert
            result.ShouldBeTrue();

            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{parent.Id}"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(x => x.RemoveDataAsync($"comment:{child.Id}"), Times.AtLeastOnce);

            _cacheServiceMock.Verify(x => x.GetSetMembersAsync(CommentListKeysSet), Times.Once);
            _cacheServiceMock.Verify(x => x.ClearSetAsync(CommentListKeysSet), Times.Once);

            (await _context.Comments.FindAsync(parent.Id)).ShouldBeNull();
            (await _context.Comments.FindAsync(child.Id)).ShouldBeNull();
        }

        [Fact]
        public async Task DeleteAsync_ThrowsOperationCanceled_WhenTokenCancelled()
        {
            // Arrange
            var comment = CreateComment("Comment to cancel", UserId1);
            await AddAndSaveAsync(comment);

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Should.ThrowAsync<OperationCanceledException>(() =>
                _service.DeleteAsync(comment.Id, UserId1, cts.Token));
        }

        [Fact]
        public async Task DeleteAsync_WithManyNestedComments_CompletesWithinTimeout()
        {
            // Arrange 
            var parent = CreateComment("Parent with many children");
            var allComments = new List<Comment> { parent };

            for (int i = 1; i <= 50; i++)
            {
                var child = CreateComment($"Child comment {i}", parent.Id);
                allComments.Add(child);

                for (int j = 1; j <= 3; j++)
                {
                    var grandChild = CreateComment($"Grandchild {j} of child {i}", child.Id);
                    allComments.Add(grandChild);
                }
            }

            await AddAndSaveAsync(allComments.ToArray());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _service.DeleteAsync(parent.Id, UserId1, cts.Token);
            stopwatch.Stop();

            // Assert
            result.ShouldBeTrue();
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10000);

            var remainingComments = await _context.Comments
                .Where(c => allComments.Select(ac => ac.Id).Contains(c.Id))
                .CountAsync();
            remainingComments.ShouldBe(0);
        }

        #endregion

        #region Helper Methods

        private Comment CreateComment(string content, Guid? parentId = null, Guid? userId = null, DateTime? createdAt = null)
        {
            return new Comment
            {
                Id = Guid.NewGuid(),
                ProductId = ProductId,
                ParentCommentId = parentId,
                UserId = userId ?? UserId1,
                Content = content,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };
        }
        private async Task AddAndSaveAsync(params Comment[] comments)
        {
            _context.Comments.AddRange(comments);
            await _context.SaveChangesAsync();
        }

        #endregion
    }
}
