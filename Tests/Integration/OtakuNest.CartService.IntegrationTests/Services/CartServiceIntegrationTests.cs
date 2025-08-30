using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using OtakuNest.CartService.DTOs;
using OtakuNest.CartService.Exceptions;
using OtakuNest.CartService.IntegrationTests.Fixtures;
using OtakuNest.Contracts;
using Shouldly;

namespace OtakuNest.CartService.IntegrationTests.Services
{
    public class CartServiceIntegrationTests
        : IClassFixture<DatabaseFixture>, IClassFixture<MassTransitFixture>
    {
        private readonly DatabaseFixture _dbFixture;
        private readonly MassTransitFixture _busFixture;

        private readonly CartService.Services.CartService _cartService;

        public CartServiceIntegrationTests(DatabaseFixture dbFixture, MassTransitFixture busFixture)
        {
            _dbFixture = dbFixture;
            _busFixture = busFixture;

            _cartService = new CartService.Services.CartService(
                _dbFixture.DbContext,
                _busFixture.Harness.Bus,
                _busFixture.QuantityClient
            );
        }

        #region AddItemToCartAsync Tests

        [Fact]
        public async Task AddItemToCart_ShouldAddItem_AndPublishEvent()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();
            var itemDto = CreateCartItemDto(productId, 2);

            // Act
            await _cartService.AddItemToCartAsync(userId, itemDto);

            // Assert
            var cartItem = await _dbFixture.DbContext.CartItems
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.Cart.UserId == userId);

            cartItem.ShouldNotBeNull();
            cartItem.Quantity.ShouldBe(2);

            (await _busFixture.Harness.Published.Any<CartItemAddedEvent>()).ShouldBeTrue();
        }

        [Fact]
        public async Task AddItemToCart_ShouldAddMultipleDifferentProducts()
        {
            // Arrange
            var userId = CreateGuid();
            var product1 = CreateGuid();
            var product2 = CreateGuid();

            // Act
            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(product1, 2));
            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(product2, 3));

            // Assert
            var items = await _dbFixture.DbContext.CartItems
                .AsNoTracking()
                .Where(c => c.Cart.UserId == userId)
                .ToListAsync();

            items.Count.ShouldBe(2);
            items.First(i => i.ProductId == product1).Quantity.ShouldBe(2);
            items.First(i => i.ProductId == product2).Quantity.ShouldBe(3);

            (await _busFixture.Harness.Published.Any<CartItemAddedEvent>()).ShouldBeTrue();
        }

        [Fact]
        public async Task AddItemToCart_ShouldMaintainCorrectQuantityAfterMultipleAdds()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();

            // Act
            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(productId, 1));
            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(productId, 2));
            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(productId, 3));

            // Assert
            var cartItem = await _dbFixture.DbContext.CartItems
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Cart.UserId == userId && c.ProductId == productId);

            cartItem.ShouldNotBeNull();
            cartItem.Quantity.ShouldBe(6);
        }

        [Fact]
        public async Task AddItemToCart_ShouldCreateCartIfNotExists()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();

            // Act
            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(productId, 4));

            var cart = await _dbFixture.DbContext.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            cart.ShouldNotBeNull();
            cart.Items.Count.ShouldBe(1);
            cart.Items.First().ProductId.ShouldBe(productId);
            cart.Items.First().Quantity.ShouldBe(4);
        }

        [Fact]
        public async Task AddItemToCart_ShouldIncreaseQuantity_WhenItemAlreadyExists()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();
            var firstItem = CreateCartItemDto(productId, 2);
            var secondItem = CreateCartItemDto(productId, 3);

            // Act
            await _cartService.AddItemToCartAsync(userId, firstItem);
            var firstPublished = _busFixture.Harness.Published
                .Select<CartItemAddedEvent>()
                .Where(x => x.Context.Message.ProductId == productId && x.Context.Message.UserId == userId)
                .ToList();

            await _cartService.AddItemToCartAsync(userId, secondItem);
            var secondPublished = _busFixture.Harness.Published
                .Select<CartItemAddedEvent>()
                .Where(x => x.Context.Message.ProductId == productId && x.Context.Message.UserId == userId)
                .Except(firstPublished)
                .ToList();

            // Assert
            firstPublished.Count.ShouldBe(1);
            secondPublished.Count.ShouldBe(1);
        }

        [Fact]
        public async Task AddItemToCart_ShouldThrow_WhenNotEnoughStock()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();
            var itemDto = CreateCartItemDto(productId, 20);

            // Act & Assert
            await Should.ThrowAsync<NotEnoughStockException>(() =>
                _cartService.AddItemToCartAsync(userId, itemDto)
            );
        }

        #endregion

        #region RemoveItemFromCartAsync Tests

        [Fact]
        public async Task RemoveItemFromCart_ShouldRemoveItem_AndPublishEvent()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();
            var itemDto = CreateCartItemDto(productId, 2);

            await _cartService.AddItemToCartAsync(userId, itemDto);

            // Act
            await _cartService.RemoveItemFromCartAsync(userId, productId);

            // Assert
            var cartItem = await _dbFixture.DbContext.CartItems
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.Cart.UserId == userId);

            cartItem.ShouldBeNull();

            var publishedEvent = _busFixture.Harness.Published
                .Select<CartItemRemovedEvent>()
                .FirstOrDefault();

            publishedEvent.ShouldNotBeNull();
            publishedEvent.Context.Message.ProductId.ShouldBe(productId);
            publishedEvent.Context.Message.UserId.ShouldBe(userId);
        }

        [Fact]
        public async Task RemoveItemFromCart_ShouldDoNothing_WhenCartDoesNotExist()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();

            // Act
            await _cartService.RemoveItemFromCartAsync(userId, productId);

            // Assert
            var cart = await _dbFixture.DbContext.Carts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);

            cart.ShouldBeNull();
            (await _busFixture.Harness.Published.Any<CartItemRemovedEvent>()).ShouldBeFalse();
        }

        [Fact]
        public async Task RemoveItemFromCart_ShouldDoNothing_WhenItemDoesNotExist()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();

            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(CreateGuid(), 1));

            var publishedBefore = _busFixture.Harness.Published.Select<CartItemRemovedEvent>().Count();

            // Act
            await _cartService.RemoveItemFromCartAsync(userId, productId);

            // Assert
            var cart = await _dbFixture.DbContext.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            cart.ShouldNotBeNull();
            cart.Items.Count.ShouldBe(1);

            var publishedAfter = _busFixture.Harness.Published.Select<CartItemRemovedEvent>().Count();
            (publishedAfter - publishedBefore).ShouldBe(0);
        }

        #endregion

        #region ClearCartAsync Tests

        [Fact]
        public async Task ClearCart_ShouldRemoveAllItems_AndPublishEvent()
        {
            // Arrange
            var userId = CreateGuid();
            var product1 = CreateGuid();
            var product2 = CreateGuid();

            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(product1, 2));
            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(product2, 3));

            // Act
            await _cartService.ClearCartAsync(userId);

            // Assert
            var cart = await _dbFixture.DbContext.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            cart.ShouldNotBeNull();
            cart.Items.Count.ShouldBe(0);

            var publishedEvent = _busFixture.Harness.Published
                .Select<CartClearedEvent>()
                .FirstOrDefault();

            publishedEvent.ShouldNotBeNull();
            publishedEvent.Context.Message.UserId.ShouldBe(userId);
            publishedEvent.Context.Message.ClearedAt.ShouldBeLessThan(DateTime.UtcNow.AddSeconds(1));
            publishedEvent.Context.Message.ClearedAt.ShouldBeGreaterThan(DateTime.UtcNow.AddSeconds(-1));
        }

        [Fact]
        public async Task ClearCart_ShouldDoNothing_WhenCartDoesNotExist()
        {
            // Arrange
            var userId = CreateGuid();
            var publishedBefore = _busFixture.Harness.Published.Select<CartClearedEvent>().Count();

            // Act
            await _cartService.ClearCartAsync(userId);

            // Assert
            var publishedAfter = _busFixture.Harness.Published.Select<CartClearedEvent>().Count();
            (publishedAfter - publishedBefore).ShouldBe(0);
        }

        [Fact]
        public async Task ClearCart_ShouldDoNothing_WhenCartIsAlreadyEmpty()
        {
            // Arrange
            var userId = CreateGuid();
            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(CreateGuid(), 1));
            await _cartService.ClearCartAsync(userId);

            var publishedBefore = _busFixture.Harness.Published.Select<CartClearedEvent>().Count();

            // Act
            await _cartService.ClearCartAsync(userId);

            // Assert
            var cart = await _dbFixture.DbContext.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            cart.ShouldNotBeNull();
            cart.Items.Count.ShouldBe(0);

            var publishedAfter = _busFixture.Harness.Published.Select<CartClearedEvent>().Count();
            (publishedAfter - publishedBefore).ShouldBe(0);
        }

        [Fact]
        public async Task ClearCart_ShouldNotAffectOtherUsersCarts()
        {
            // Arrange
            await _dbFixture.ResetDatabaseAsync();

            var user1 = CreateGuid();
            var user2 = CreateGuid();

            await _cartService.AddItemToCartAsync(user1, CreateCartItemDto(CreateGuid(), 2));
            await _cartService.AddItemToCartAsync(user2, CreateCartItemDto(CreateGuid(), 3));

            var publishedBefore = _busFixture.Harness.Published.Select<CartClearedEvent>().Count();

            // Act
            await _cartService.ClearCartAsync(user1);

            // Assert
            var cart1 = await _dbFixture.DbContext.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == user1);
            var cart2 = await _dbFixture.DbContext.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == user2);

            cart1!.Items.Count.ShouldBe(0);
            cart2!.Items.Count.ShouldBe(1);

            var publishedAfter = _busFixture.Harness.Published.Select<CartClearedEvent>().Count();
            (publishedAfter - publishedBefore).ShouldBe(1);

            var clearedEvent = _busFixture.Harness.Published
                .Select<CartClearedEvent>()
                .Skip(publishedBefore)
                .First();

            clearedEvent.ShouldNotBeNull();
            clearedEvent.Context.Message.UserId.ShouldBe(user1);
        }

        #endregion

        #region ChangeItemQuantityAsync Tests

        [Fact]
        public async Task ChangeItemQuantity_ShouldDoNothing_WhenDeltaIsZero()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();

            await _dbFixture.ResetDatabaseAsync();

            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(productId, 2));

            var publishedBefore = _busFixture.Harness.Published.Count();

            // Act
            await _cartService.ChangeItemQuantityAsync(userId, productId, 0);

            // Assert
            var publishedAfter = _busFixture.Harness.Published.Count();
            (publishedAfter - publishedBefore).ShouldBe(0);

            var item = await _dbFixture.DbContext.CartItems.AsNoTracking().FirstAsync();
            item.Quantity.ShouldBe(2);
        }

        [Fact]
        public async Task ChangeItemQuantity_ShouldDoNothing_WhenCartDoesNotExist()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();

            var publishedBefore = _busFixture.Harness.Published.Count();

            // Act
            await _cartService.ChangeItemQuantityAsync(userId, productId, 1);

            // Assert
            var publishedAfter = _busFixture.Harness.Published.Count();
            (publishedAfter - publishedBefore).ShouldBe(0);
        }

        [Fact]
        public async Task ChangeItemQuantity_ShouldDoNothing_WhenItemDoesNotExist()
        {
            // Arrange
            var userId = CreateGuid();
            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(CreateGuid(), 2));

            var publishedBefore = _busFixture.Harness.Published.Count();

            // Act
            await _cartService.ChangeItemQuantityAsync(userId, CreateGuid(), 1);

            // Assert
            var publishedAfter = _busFixture.Harness.Published.Count();
            (publishedAfter - publishedBefore).ShouldBe(0);
        }

        [Fact]
        public async Task ChangeItemQuantity_ShouldRemoveItem_WhenQuantityGoesToZeroOrLess()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();

            await _dbFixture.ResetDatabaseAsync();

            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(productId, 2));

            // Act
            await _cartService.ChangeItemQuantityAsync(userId, productId, -2);

            // Assert
            var cart = await _dbFixture.DbContext.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            cart!.Items.ShouldBeEmpty();

            var publishedEvent = _busFixture.Harness.Published
                .Select<CartItemRemovedEvent>()
                .FirstOrDefault(e => e.Context.Message.UserId == userId
                                  && e.Context.Message.ProductId == productId);

            publishedEvent.ShouldNotBeNull();
            publishedEvent.Context.Message.ProductId.ShouldBe(productId);
        }

        [Fact]
        public async Task ChangeItemQuantity_ShouldThrow_WhenNewQuantityExceedsStock()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();

            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(productId, 8));

            // Act & Assert
            var ex = await Should.ThrowAsync<NotEnoughStockException>(async () =>
                await _cartService.ChangeItemQuantityAsync(userId, productId, 3) 
            );

            ex.ProductId.ShouldBe(productId);
        }

        [Fact]
        public async Task ChangeItemQuantity_ShouldIncreaseQuantity_WhenStockIsEnough()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();

            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(productId, 1));

            // Act
            await _cartService.ChangeItemQuantityAsync(userId, productId, 2);

            // Assert
            var item = await _dbFixture.DbContext.CartItems.FirstOrDefaultAsync(i => i.ProductId == productId);
            item!.Quantity.ShouldBe(3);

            var publishedEvent = _busFixture.Harness.Published
                .Select<CartItemQuantityChangedEvent>()
                .FirstOrDefault();

            publishedEvent.ShouldNotBeNull();
            publishedEvent.Context.Message.NewQuantity.ShouldBe(3);
        }

        [Fact]
        public async Task ChangeItemQuantity_ShouldDecreaseQuantity_WhenDeltaIsNegative()
        {
            // Arrange
            var userId = CreateGuid();
            var productId = CreateGuid();

            await _cartService.AddItemToCartAsync(userId, CreateCartItemDto(productId, 5));

            // Act
            await _cartService.ChangeItemQuantityAsync(userId, productId, -2);

            // Assert
            var item = await _dbFixture.DbContext.CartItems.FirstOrDefaultAsync(i => i.ProductId == productId);
            item!.Quantity.ShouldBe(3);

            var publishedEvent = _busFixture.Harness.Published
                .Select<CartItemQuantityChangedEvent>()
                .FirstOrDefault();

            publishedEvent.ShouldNotBeNull();
            publishedEvent.Context.Message.NewQuantity.ShouldBe(3);
        }

        #endregion

        #region Helper Methods

        private static CartItemDto CreateCartItemDto(Guid productId, int quantity = 1) =>
            new CartItemDto
            {
                ProductId = productId,
                Quantity = quantity
            };

        private static Guid CreateGuid() => Guid.NewGuid();

        #endregion
    }
}