using System.Diagnostics;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using OtakuNest.CartService.Data;
using OtakuNest.CartService.DTOs;
using OtakuNest.CartService.Exceptions;
using OtakuNest.CartService.Models;
using OtakuNest.Contracts;
using Shouldly;

namespace OtakuNest.CartService.Tests.Services
{
    public class CartServiceTests : IDisposable
    {
        private readonly CartDbContext _context;
        private readonly CartService.Services.CartService _service;
        private readonly Mock<IPublishEndpoint> _publishEndpointMock;
        private readonly Mock<IRequestClient<CheckProductQuantityRequest>> _quantityClientMock;
        private bool _disposed = false;

        private static readonly Guid UserId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid ProductId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid ProductId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        public CartServiceTests()
        {
            _context = GetInMemoryDbContext();
            _publishEndpointMock = new Mock<IPublishEndpoint>();
            _quantityClientMock = new Mock<IRequestClient<CheckProductQuantityRequest>>();

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

        private static CartDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<CartDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var context = new CartDbContext(options);

            var cart = new Cart
            {
                Id = Guid.NewGuid(),
                UserId = UserId1,
                CreatedAt = DateTime.UtcNow,
                Items = new List<CartItem>
                {
                    new CartItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = ProductId1,
                        Quantity = 2
                    },
                    new CartItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = ProductId2,
                        Quantity = 1
                    }
                }
            };

            context.Carts.Add(cart);
            context.SaveChanges();
            return context;
        }

        private CartService.Services.CartService CreateService(CartDbContext context)
        {
            _quantityClientMock
                .Setup(c => c.GetResponse<CheckProductQuantityResponse>(
                    It.IsAny<CheckProductQuantityRequest>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<RequestTimeout>() 
                ))
                .ReturnsAsync((CheckProductQuantityRequest request, CancellationToken token, RequestTimeout timeout) =>
                {
                    var response = new CheckProductQuantityResponse(request.ProductId, 10);
                    return Mock.Of<Response<CheckProductQuantityResponse>>(r => r.Message == response);
                });

            _publishEndpointMock
                .Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            return new CartService.Services.CartService(
                context,
                _publishEndpointMock.Object,
                _quantityClientMock.Object
            );
        }

        #region GetCartAsync Tests

        [Fact]
        public async Task GetCartAsync_ShouldReturnEmptyCart_WhenCartDoesNotExists()
        {
            // Act
            var result = await _service.GetCartAsync(Guid.NewGuid());

            // Assert
            result.ShouldNotBeNull();
            result.Items.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetCartAsync_ShouldReturnCartItems_WhenCartExists()
        {
            // Act
            var result = await _service.GetCartAsync(UserId1);

            // Assert
            result.ShouldNotBeNull();
            result.Items.Count.ShouldBe(2);
            result.Items.ShouldContain(i => i.ProductId == ProductId1 && i.Quantity == 2);
            result.Items.ShouldContain(i => i.ProductId == ProductId2 && i.Quantity == 1);
        }

        [Fact]
        public async Task GetCartAsync_ShouldReturnEmptyItems_WhenCartExistsButHasNoItems()
        {
            // Arrange
            var emptyCartUserId = Guid.NewGuid();
            _context.Carts.Add(new Cart
            {
                Id = Guid.NewGuid(),
                UserId = emptyCartUserId,
                Items = new List<CartItem>()
            });
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetCartAsync(emptyCartUserId);

            // Assert
            result.ShouldNotBeNull();
            result.Items.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetCartAsync_ShouldThrowTaskCanceled_WhenCancellationRequested()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act
            var act = async () => await _service.GetCartAsync(UserId1, cts.Token);

            // Assert
            await act.ShouldThrowAsync<TaskCanceledException>();
        }

        [Fact]
        public async Task GetCartAsync_ShouldReturnAllItems_WhenCartHasMultipleProducts()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var cart = new Cart
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Items = new List<CartItem>
                {
                    new() { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), Quantity = 5 },
                    new() { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), Quantity = 10 },
                    new() { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), Quantity = 1 }
                }
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetCartAsync(userId);

            // Assert
            result.ShouldNotBeNull();
            result.Items.Count.ShouldBe(3);
            result.Items.Sum(i => i.Quantity).ShouldBe(16);
        }

        [Fact]
        public async Task GetCartAsync_ShouldReturnItemsInSameOrder_AsStoredInDatabase()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId1 = Guid.NewGuid();
            var productId2 = Guid.NewGuid();
            var cart = new Cart
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Items = new List<CartItem>
                {
                    new() { Id = Guid.NewGuid(), ProductId = productId1, Quantity = 1 },
                    new() { Id = Guid.NewGuid(), ProductId = productId2, Quantity = 2 }
                }
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetCartAsync(userId);

            // Assert
            result.Items[0].ProductId.ShouldBe(productId1);
            result.Items[1].ProductId.ShouldBe(productId2);
        }

        [Fact]
        public async Task GetCartAsync_ShouldReturnCorrectCart_WhenMultipleCartsExist()
        {
            // Arrange
            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();

            var cart1 = new Cart
            {
                Id = Guid.NewGuid(),
                UserId = user1Id,
                Items = new List<CartItem>
                {
                    new() { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), Quantity = 2 }
                }
            };

            var cart2 = new Cart
            {
                Id = Guid.NewGuid(),
                UserId = user2Id,
                Items = new List<CartItem>
                {
                    new() { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), Quantity = 99 }
                }
            };

            _context.Carts.AddRange(cart1, cart2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetCartAsync(user1Id);

            // Assert
            result.ShouldNotBeNull();
            result.Items.Count.ShouldBe(1);
            result.Items[0].Quantity.ShouldBe(2);
            result.Items[0].Quantity.ShouldNotBe(99);
        }

        [Fact]
        public async Task GetCartAsync_ShouldHandleLargeCart_Performance()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var items = Enumerable.Range(0, 10000)
                .Select(i => new CartItem { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), Quantity = i + 1 })
                .ToList();

            var cart = new Cart { Id = Guid.NewGuid(), UserId = userId, Items = items };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            var stopwatch = Stopwatch.StartNew();

            // Act
            var result = await _service.GetCartAsync(userId);

            stopwatch.Stop();

            // Assert
            result.Items.Count.ShouldBe(10000);
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(500);
        }

        [Fact]
        public async Task GetCartAsync_ShouldBeThreadSafe_WhenCalledInParallel()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var cart = new Cart
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Items = new List<CartItem>
                {
                    new() { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), Quantity = 1 }
                }
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            // Act
            var tasks = Enumerable.Range(0, 20)
                .Select(_ => _service.GetCartAsync(userId));
            var results = await Task.WhenAll(tasks);

            // Assert
            results.ShouldAllBe(r => r.Items.Count == 1);
        }

        #endregion

        #region AddItemToCartAsync Tests

        [Fact]
        public async Task AddItemToCartAsync_ShouldCreateNewCart_WhenCartDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var itemDto = new CartItemDto { ProductId = productId, Quantity = 2 };
            SetupAvailableQuantity(productId, 10);

            // Act
            await _service.AddItemToCartAsync(userId, itemDto);

            // Assert
            var cart = await _context.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId);
            cart.ShouldNotBeNull();
            cart.Items.ShouldContain(i => i.ProductId == productId && i.Quantity == 2);

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<CartItemAddedEvent>(e =>
                    e.UserId == userId && e.ProductId == productId && e.Quantity == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddItemToCartAsync_ShouldAddNewItem_WhenCartExists()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var cart = new Cart
            {
                UserId = userId,
                Items = new List<CartItem>()
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            var itemDto = new CartItemDto { ProductId = productId, Quantity = 3 };
            SetupAvailableQuantity(productId, 10);

            // Act
            await _service.AddItemToCartAsync(userId, itemDto);

            // Assert
            var updatedCart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.UserId == userId);

            updatedCart.Items.ShouldContain(i =>
                i.ProductId == productId &&
                i.Quantity == 3 &&
                i.CartId == cart.Id); 
        }

        [Fact]
        public async Task AddItemToCartAsync_ShouldIncreaseQuantity_WhenItemAlreadyExists()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            SetupCartWithItem(userId, productId, 2);
            SetupAvailableQuantity(productId, 10);

            var itemDto = new CartItemDto { ProductId = productId, Quantity = 3 };

            // Act
            await _service.AddItemToCartAsync(userId, itemDto);

            // Assert
            var cart = await _context.Carts.Include(c => c.Items)
                .FirstAsync(c => c.UserId == userId);
            var updatedItem = await _context.CartItems
                .FirstAsync(i => i.ProductId == productId && i.Cart.UserId == userId);
            cart.Items.Count.ShouldBe(1);
            updatedItem.Quantity.ShouldBe(5);
        }

        [Fact]
        public async Task AddItemToCartAsync_ShouldThrowException_WhenStockIsNotEnough()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var itemDto = new CartItemDto { ProductId = productId, Quantity = 5 };
            SetupAvailableQuantity(productId,3);

            // Act & Assert
            await Should.ThrowAsync<NotEnoughStockException>(() =>
                _service.AddItemToCartAsync(userId, itemDto));
        }

        [Fact]
        public async Task AddItemToCartAsync_ShouldThrowException_WhenExistingQuantityExceedsStock()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            SetupCartWithItem(userId, productId, 8);
            SetupAvailableQuantity(productId,10);

            var itemDto = new CartItemDto { ProductId = productId, Quantity = 5 };

            // Act & Assert
            await Should.ThrowAsync<NotEnoughStockException>(() =>
                _service.AddItemToCartAsync(userId, itemDto));
        }

        [Fact]
        public async Task AddItemToCartAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var itemDto = new CartItemDto { ProductId = productId, Quantity = 1 };
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Should.ThrowAsync<TaskCanceledException>(() =>
                _service.AddItemToCartAsync(userId, itemDto, cts.Token));
        }

        [Fact]
        public async Task AddItemToCartAsync_ShouldBeThreadSafe_WhenCalledConcurrently()
        {
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            SetupCartWithItem(userId, productId, 0);
            SetupAvailableQuantity(productId, 100);

            var tasks = Enumerable.Range(0, 10)
                .Select(_ => _service.AddItemToCartAsync(userId, new CartItemDto { ProductId = productId, Quantity = 1 }));

            await Task.WhenAll(tasks);

            var updatedItem = await _context.CartItems
                .FirstAsync(i => i.ProductId == productId && i.Cart.UserId == userId);

            updatedItem.Quantity.ShouldBe(10);
        }

        #endregion

        #region RemoveItemFromCartAsync Tests

        [Fact]
        public async Task RemoveItemFromCartAsync_ShouldRemoveItem_WhenItemExists()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            SetupCartWithItem(userId, productId, 2);

            // Act
            await _service.RemoveItemFromCartAsync(userId, productId);

            // Assert
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.UserId == userId);

            cart.Items.ShouldNotContain(i => i.ProductId == productId);

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<CartItemRemovedEvent>(e =>
                    e.UserId == userId && e.ProductId == productId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveItemFromCartAsync_ShouldDoNothing_WhenCartDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            // Act
            await _service.RemoveItemFromCartAsync(userId, productId);

            // Assert
            (await _context.Carts.CountAsync()).ShouldBe(1);
            (await _context.CartItems.CountAsync()).ShouldBe(2);

            _publishEndpointMock.Verify(x =>
                x.Publish(It.IsAny<CartItemRemovedEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task RemoveItemFromCartAsync_ShouldDoNothing_WhenItemDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var otherProductId = Guid.NewGuid();
            SetupCartWithItem(userId, otherProductId, 1);

            // Act
            await _service.RemoveItemFromCartAsync(userId, productId);

            // Assert
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.UserId == userId);

            cart.Items.Count.ShouldBe(1);
            _publishEndpointMock.Verify(x =>
                x.Publish(It.IsAny<CartItemRemovedEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task RemoveItemFromCartAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            SetupCartWithItem(userId, productId, 1);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Should.ThrowAsync<TaskCanceledException>(() =>
                _service.RemoveItemFromCartAsync(userId, productId, cts.Token));
        }

        [Fact]
        public async Task RemoveItemFromCartAsync_ShouldLeaveEmptyCart_WhenLastItemRemoved()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            SetupCartWithItem(userId, productId, 1);

            // Act
            await _service.RemoveItemFromCartAsync(userId, productId);

            // Assert
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.UserId == userId);

            cart.Items.ShouldBeEmpty();
        }

        [Fact]
        public async Task RemoveItemFromCartAsync_ShouldDoNothing_WhenCalledTwiceForSameItem()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            SetupCartWithItem(userId, productId, 1);

            // Act
            await _service.RemoveItemFromCartAsync(userId, productId);
            await _service.RemoveItemFromCartAsync(userId, productId);

            // Assert
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.UserId == userId);

            cart.Items.ShouldBeEmpty();

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<CartItemRemovedEvent>(e =>
                    e.UserId == userId && e.ProductId == productId),
                It.IsAny<CancellationToken>()), Times.Once); 
        }

        [Fact]
        public async Task RemoveItemFromCartAsync_ShouldHandleLargeCart()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var cart = new Cart
            {
                UserId = userId,
                Items = Enumerable.Range(0, 500).Select(i => new CartItem
                {
                    ProductId = i == 250 ? productId : Guid.NewGuid(),
                    Quantity = 1
                }).ToList()
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            // Act
            await _service.RemoveItemFromCartAsync(userId, productId);

            // Assert
            var updatedCart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.UserId == userId);

            updatedCart.Items.ShouldNotContain(i => i.ProductId == productId);
        }

        #endregion

        #region ClearCartAsync Tests

        [Fact]
        public async Task ClearCartAsync_ShouldDoNothing_WhenCartDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid(); 

            // Act
            await _service.ClearCartAsync(userId);

            // Assert
            (await _context.Carts.CountAsync()).ShouldBe(1); 
            _publishEndpointMock.Verify(x =>
                x.Publish(It.IsAny<CartClearedEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ClearCartAsync_ShouldDoNothing_WhenCartIsEmpty()
        {
            // Arrange
            var userId = Guid.NewGuid();
            await SetupCartAsync(userId, 0);

            // Act
            await _service.ClearCartAsync(userId);

            // Assert
            var savedCart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.UserId == userId);

            savedCart.Items.ShouldBeEmpty();
            _publishEndpointMock.Verify(x =>
                x.Publish(It.IsAny<CartClearedEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ClearCartAsync_ShouldClearItems_AndPublishEvent_WhenCartHasItems()
        {
            // Arrange
            var userId = Guid.NewGuid();
            await SetupCartAsync(userId, 2);

            // Act
            await _service.ClearCartAsync(userId);

            // Assert
            var clearedCart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.UserId == userId);

            clearedCart.Items.ShouldBeEmpty();
            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<CartClearedEvent>(e => e.UserId == userId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ClearCartAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var userId = Guid.NewGuid();
            await SetupCartAsync(userId, 1);

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Should.ThrowAsync<TaskCanceledException>(() =>
                _service.ClearCartAsync(userId, cts.Token));
        }

        [Fact]
        public async Task ClearCartAsync_ShouldHandleLargeNumberOfItems()
        {
            // Arrange
            var userId = Guid.NewGuid();
            await SetupCartAsync(userId, 500);

            // Act
            await _service.ClearCartAsync(userId);

            // Assert
            var clearedCart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.UserId == userId);

            clearedCart.Items.ShouldBeEmpty();
            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<CartClearedEvent>(e => e.UserId == userId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ClearCartAsync_ShouldDoNothing_WhenCalledTwice()
        {
            // Arrange
            var userId = Guid.NewGuid();
            await SetupCartAsync(userId, 1);

            // Act
            await _service.ClearCartAsync(userId);
            await _service.ClearCartAsync(userId);

            // Assert
            var clearedCart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.UserId == userId);

            clearedCart.Items.ShouldBeEmpty();
            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<CartClearedEvent>(e => e.UserId == userId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ClearCartAsync_ShouldHandleCartWithNullItemsList()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var cart = new Cart
            {
                UserId = userId,
                Items = null!
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            // Act
            await _service.ClearCartAsync(userId);

            // Assert
            var savedCart = await _context.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.UserId == userId);

            savedCart.Items.ShouldBeEmpty();
            _publishEndpointMock.Verify(x =>
                x.Publish(It.IsAny<CartClearedEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ClearCartAsync_ShouldOnlyClearSpecifiedUserCart()
        {
            // Arrange
            var user1 = Guid.NewGuid();
            var user2 = Guid.NewGuid();
            await SetupCartAsync(user1, 3);
            await SetupCartAsync(user2, 2);

            // Act
            await _service.ClearCartAsync(user1);

            // Assert
            var savedCart1 = await _context.Carts.Include(c => c.Items).FirstAsync(c => c.UserId == user1);
            var savedCart2 = await _context.Carts.Include(c => c.Items).FirstAsync(c => c.UserId == user2);

            savedCart1.Items.ShouldBeEmpty();
            savedCart2.Items.Count.ShouldBe(2);
        }

        #endregion

        #region ChangeItemQuantityAsync Tests

        [Fact]
        public async Task ChangeItemQuantityAsync_ShouldReturn_WhenDeltaIsZero()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var cart = await SetupCartAsync(userId, 1);
            var item = cart.Items.First();
            item.Quantity = 1; 
            await _context.SaveChangesAsync();

            var productId = item.ProductId;

            // Act
            await _service.ChangeItemQuantityAsync(userId, productId, 0);

            // Assert
            var savedItem = await _context.CartItems.FirstAsync(i => i.ProductId == productId && i.CartId == cart.Id);
            savedItem.Quantity.ShouldBe(1);

            _publishEndpointMock.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ChangeItemQuantityAsync_ShouldReturn_WhenCartDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            // Act
            await _service.ChangeItemQuantityAsync(userId, productId, 1);

            // Assert
            _publishEndpointMock.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ChangeItemQuantityAsync_ShouldRemoveItem_WhenNewQuantityIsZeroOrLess()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var cart = await SetupCartAsync(userId, 1);
            var productId = cart.Items.First().ProductId;

            // Act
            await _service.ChangeItemQuantityAsync(userId, productId, -1);

            // Assert
            var savedCart = await _context.Carts.Include(c => c.Items).FirstAsync(c => c.UserId == userId);
            savedCart.Items.ShouldBeEmpty();
            _publishEndpointMock.Verify(x => x.Publish(It.Is<CartItemRemovedEvent>(e => e.UserId == userId && e.ProductId == productId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ChangeItemQuantityAsync_ShouldThrow_WhenIncreasingAboveAvailableQuantity()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var cart = await SetupCartAsync(userId, 1);
            var productId = cart.Items.First().ProductId;
            SetupAvailableQuantity(productId, 5);

            // Act & Assert
            await Should.ThrowAsync<NotEnoughStockException>(() =>
                _service.ChangeItemQuantityAsync(userId, productId, 10));
        }

        [Fact]
        public async Task ChangeItemQuantityAsync_ShouldIncreaseQuantity_WhenWithinAvailableStock()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var cart = await SetupCartAsync(userId, 1); 
            var item = cart.Items.First();
            item.Quantity = 1; 
            await _context.SaveChangesAsync();

            var productId = item.ProductId;
            SetupAvailableQuantity(productId, 10);

            // Act
            await _service.ChangeItemQuantityAsync(userId, productId, 3);

            // Assert
            var updatedItem = await _context.CartItems.FirstAsync(i => i.ProductId == productId && i.CartId == cart.Id);
            updatedItem.Quantity.ShouldBe(4); 

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<CartItemQuantityChangedEvent>(e => e.UserId == userId && e.ProductId == productId && e.NewQuantity == 4),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ChangeItemQuantityAsync_ShouldDecreaseQuantity_WhenDeltaIsNegative_AndNewQuantityPositive()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var cart = await SetupCartAsync(userId, 1); 
            var item = cart.Items.First();
            item.Quantity = 2;
            await _context.SaveChangesAsync();
            var productId = item.ProductId;

            // Act
            await _service.ChangeItemQuantityAsync(userId, productId, -1);

            // Assert
            var updatedItem = await _context.CartItems.FirstAsync(i => i.ProductId == productId && i.CartId == cart.Id);
            updatedItem.Quantity.ShouldBe(1);

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<CartItemQuantityChangedEvent>(e => e.UserId == userId && e.ProductId == productId && e.NewQuantity == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ChangeItemQuantityAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var cart = await SetupCartAsync(userId, 1);
            var productId = cart.Items.First().ProductId;
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Should.ThrowAsync<TaskCanceledException>(() =>
                _service.ChangeItemQuantityAsync(userId, productId, 1, cts.Token));
        }

        [Fact]
        public async Task ChangeItemQuantityAsync_ShouldReturn_WhenItemDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            await SetupCartAsync(userId, 1);
            var productId = Guid.NewGuid(); 

            // Act
            await _service.ChangeItemQuantityAsync(userId, productId, 1);

            // Assert
            var savedCart = await _context.Carts.Include(c => c.Items).FirstAsync(c => c.UserId == userId);
            savedCart.Items.Count.ShouldBe(1);
            _publishEndpointMock.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ChangeItemQuantityAsync_ShouldOnlyChangeSpecifiedItem()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var cart = await SetupCartAsync(userId, 2);
            var firstItem = cart.Items.First();
            var secondItem = cart.Items.Last();
            firstItem.Quantity = 2;
            secondItem.Quantity = 3;
            await _context.SaveChangesAsync();

            // Act
            await _service.ChangeItemQuantityAsync(userId, firstItem.ProductId, 1);

            // Assert
            var updatedItems = await _context.CartItems.Where(i => i.CartId == cart.Id).ToListAsync();
            updatedItems.First(i => i.ProductId == firstItem.ProductId).Quantity.ShouldBe(3);
            updatedItems.First(i => i.ProductId == secondItem.ProductId).Quantity.ShouldBe(3); 
        }

        #endregion

        #region Helper Methods

        private void SetupAvailableQuantity(Guid productId, int availableQuantity)
        {
            var responseMock = new Mock<Response<CheckProductQuantityResponse>>();
            responseMock.SetupGet(r => r.Message).Returns(new CheckProductQuantityResponse(productId, availableQuantity));

            _quantityClientMock
                .Setup(x => x.GetResponse<CheckProductQuantityResponse>(
                    It.IsAny<CheckProductQuantityRequest>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<RequestTimeout>()))
                .ReturnsAsync(responseMock.Object);
        }

        private void SetupCartWithItem(Guid userId, Guid productId, int quantity)
        {
            var cart = new Cart
            {
                UserId = userId,
                Items = new List<CartItem>
                {
                    new CartItem
                    {
                        ProductId = productId,
                        Quantity = quantity
                    }
                }
            };

            _context.Carts.Add(cart);
            _context.SaveChanges();
        }

        private async Task<Cart> SetupCartAsync(Guid userId, int itemCount = 0)
        {
            var cart = new Cart
            {
                UserId = userId,
                Items = Enumerable.Range(0, itemCount).Select(i => new CartItem
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 1
                }).ToList()
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
            return cart;
        }

        #endregion
    }
}
