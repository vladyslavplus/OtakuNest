using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using OtakuNest.Common.Helpers;
using OtakuNest.Contracts;
using OtakuNest.OrderService.Data;
using OtakuNest.OrderService.DTOs;
using OtakuNest.OrderService.Models;
using OtakuNest.OrderService.Parameters;
using Shouldly;

namespace OtakuNest.OrderService.Tests.Services
{
    public class OrderServiceTests : IDisposable
    {
        private readonly OrdersDbContext _context;
        private readonly OrderService.Services.OrderService _service;
        private readonly Mock<IPublishEndpoint> _publishEndpointMock;
        private readonly Mock<IRequestClient<CheckProductPriceRequest>> _priceRequestClientMock;
        private readonly Mock<IRequestClient<CheckProductQuantityRequest>> _quantityRequestClientMock;
        private bool _disposed = false;

        private static readonly Guid UserId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private static readonly Guid OrderId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OrderId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid ProductId1 = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid ProductId2 = Guid.Parse("44444444-4444-4444-4444-444444444444");

        public OrderServiceTests()
        {
            _context = GetInMemoryDbContext();
            _publishEndpointMock = new Mock<IPublishEndpoint>();
            _priceRequestClientMock = new Mock<IRequestClient<CheckProductPriceRequest>>();
            _quantityRequestClientMock = new Mock<IRequestClient<CheckProductQuantityRequest>>();
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

        private static OrdersDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<OrdersDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new OrdersDbContext(options);

            context.Orders.AddRange(
                new Order
                {
                    Id = OrderId1,
                    UserId = UserId1,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    Status = OrderStatus.Pending,
                    ShippingAddress = "Test address 1",
                    Items = new List<OrderItem>
                    {
                        new OrderItem
                        {
                            Id = Guid.NewGuid(),
                            ProductId = ProductId1,
                            Quantity = 2,
                            UnitPrice = 50m
                        }
                    },
                    TotalPrice = 100m
                },
                new Order
                {
                    Id = OrderId2,
                    UserId = UserId2,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    Status = OrderStatus.Delivered,
                    ShippingAddress = "Test address 2",
                    Items = new List<OrderItem>
                    {
                        new OrderItem
                        {
                            Id = Guid.NewGuid(),
                            ProductId = ProductId2,
                            Quantity = 1,
                            UnitPrice = 200m
                        }
                    },
                    TotalPrice = 200m
                }
            );

            context.SaveChanges();
            return context;
        }

        private OrderService.Services.OrderService CreateService(OrdersDbContext context)
        {
            var sortHelper = new SortHelper<Order>();

            _publishEndpointMock
                .Setup(x => x.Publish<It.IsAnyType>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var priceResponse = new Mock<Response<CheckProductPriceResponse>>();
            priceResponse.SetupGet(r => r.Message).Returns(new CheckProductPriceResponse(ProductId1, 100m));
            _priceRequestClientMock
                .Setup(x => x.GetResponse<CheckProductPriceResponse>(It.IsAny<CheckProductPriceRequest>(), It.IsAny<CancellationToken>(), default))
                .ReturnsAsync(priceResponse.Object);

            var quantityResponse = new Mock<Response<CheckProductQuantityResponse>>();
            quantityResponse.SetupGet(r => r.Message).Returns(new CheckProductQuantityResponse(ProductId1, 10));
            _quantityRequestClientMock
                .Setup(x => x.GetResponse<CheckProductQuantityResponse>(It.IsAny<CheckProductQuantityRequest>(), It.IsAny<CancellationToken>(), default))
                .ReturnsAsync(quantityResponse.Object);

            return new OrderService.Services.OrderService(
                context,
                _priceRequestClientMock.Object,
                _quantityRequestClientMock.Object,
                _publishEndpointMock.Object,
                sortHelper
            );
        }

        #region GetAllOrdersAsync Tests

        [Fact]
        public async Task GetAllOrdersAsync_ShouldReturnAllOrders_WhenNoFiltersProvided()
        {
            // Arrange
            var parameters = new OrderParameters { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllOrdersAsync(parameters);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(2);
            result.TotalCount.ShouldBe(2);
        }

        [Theory]
        [InlineData("Pending", 1)]
        [InlineData("Delivered", 1)]
        [InlineData("Cancelled", 0)]
        public async Task GetAllOrdersAsync_ShouldFilterByStatus(string status, int expectedCount)
        {
            // Arrange
            var parameters = new OrderParameters { Status = status, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(expectedCount);
        }

        [Fact]
        public async Task GetAllOrdersAsync_ShouldFilterByUserId()
        {
            // Arrange
            var parameters = new OrderParameters { UserId = UserId1, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(1);
            result[0].UserId.ShouldBe(UserId1);
        }

        [Theory]
        [InlineData(50, 150, 1)]   
        [InlineData(100, 250, 2)]  
        [InlineData(300, 500, 0)]  
        public async Task GetAllOrdersAsync_ShouldFilterByPriceRange(decimal minPrice, decimal maxPrice, int expectedCount)
        {
            // Arrange
            var parameters = new OrderParameters
            {
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(expectedCount);
        }

        [Fact]
        public async Task GetAllOrdersAsync_ShouldFilterByDateRange()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-1.5);
            var toDate = DateTime.UtcNow;
            var parameters = new OrderParameters { FromDate = fromDate, ToDate = toDate, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(1);
            result[0].Id.ShouldBe(OrderId2);
        }

        [Fact]
        public async Task GetAllOrdersAsync_ShouldFilterByProductId()
        {
            // Arrange
            var parameters = new OrderParameters { ProductId = ProductId1, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(1);
            result[0].Items.Any(i => i.ProductId == ProductId1).ShouldBeTrue();
        }

        [Fact]
        public async Task GetAllOrdersAsync_ShouldApplyPaging()
        {
            // Arrange
            var parameters = new OrderParameters { PageNumber = 1, PageSize = 1 };

            // Act
            var result = await _service.GetAllOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(1);
            result.TotalCount.ShouldBe(2);
            result.PageSize.ShouldBe(1);
        }

        [Fact]
        public async Task GetAllOrdersAsync_ShouldApplySorting_ByTotalPriceDesc()
        {
            // Arrange
            var parameters = new OrderParameters { OrderBy = "TotalPrice desc", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(2);
            result[0].TotalPrice.ShouldBe(200m); 
            result[1].TotalPrice.ShouldBe(100m);
        }

        [Fact]
        public async Task GetAllOrdersAsync_ShouldFilterByMinPriceOnly()
        {
            var parameters = new OrderParameters { MinPrice = 150m, PageNumber = 1, PageSize = 10 };

            var result = await _service.GetAllOrdersAsync(parameters);

            result.Count.ShouldBe(1);
            result[0].TotalPrice.ShouldBe(200m);
        }

        [Fact]
        public async Task GetAllOrdersAsync_ShouldFilterByMaxPriceOnly()
        {
            var parameters = new OrderParameters { MaxPrice = 150m, PageNumber = 1, PageSize = 10 };

            var result = await _service.GetAllOrdersAsync(parameters);

            result.Count.ShouldBe(1);
            result[0].TotalPrice.ShouldBe(100m);
        }

        [Fact]
        public async Task GetAllOrdersAsync_ShouldFilterByFromDateOnly()
        {
            var fromDate = DateTime.UtcNow.AddDays(-1.5);
            var parameters = new OrderParameters { FromDate = fromDate, PageNumber = 1, PageSize = 10 };

            var result = await _service.GetAllOrdersAsync(parameters);

            result.Count.ShouldBe(1);
            result[0].Id.ShouldBe(OrderId2);
        }

        [Fact]
        public async Task GetAllOrdersAsync_ShouldFilterByToDateOnly()
        {
            var toDate = DateTime.UtcNow.AddDays(-1.5);
            var parameters = new OrderParameters { ToDate = toDate, PageNumber = 1, PageSize = 10 };

            var result = await _service.GetAllOrdersAsync(parameters);

            result.Count.ShouldBe(1);
            result[0].Id.ShouldBe(OrderId1);
        }

        [Fact]
        public async Task GetAllOrdersAsync_ShouldReturnEmpty_WhenFiltersDoNotMatch()
        {
            var parameters = new OrderParameters
            {
                UserId = UserId1,
                Status = "Delivered",
                PageNumber = 1,
                PageSize = 10
            };

            var result = await _service.GetAllOrdersAsync(parameters);

            result.Count.ShouldBe(0);
        }

        [Fact]
        public async Task GetAllOrdersAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync(); 

            var parameters = new OrderParameters { PageNumber = 1, PageSize = 10 };

            // Act & Assert
            await Should.ThrowAsync<TaskCanceledException>(async () =>
                await _service.GetAllOrdersAsync(parameters, cts.Token));
        }

        #endregion

        #region GetOrderByIdAsync Tests

        [Fact]
        public async Task GetOrderByIdAsync_ShouldReturnOrder_WhenOrderExists()
        {
            // Arrange
            var orderId = OrderId1;

            // Act
            var result = await _service.GetOrderByIdAsync(orderId);

            // Assert
            result.ShouldNotBeNull();
            result.Id.ShouldBe(OrderId1);
            result.Items.Count.ShouldBe(1);
            result.Items[0].ProductId.ShouldBe(ProductId1);
        }

        [Fact]
        public async Task GetOrderByIdAsync_ShouldReturnNull_WhenOrderDoesNotExist()
        {
            // Arrange
            var orderId = Guid.NewGuid(); 

            // Act
            var result = await _service.GetOrderByIdAsync(orderId);

            // Assert
            result.ShouldBeNull();
        }

        [Fact]
        public async Task GetOrderByIdAsync_ShouldMapAllItemsCorrectly()
        {
            // Arrange
            var orderId = OrderId1;

            // Act
            var result = await _service.GetOrderByIdAsync(orderId);

            // Assert
            result.ShouldNotBeNull();
            result.Items.ShouldNotBeEmpty();
            foreach (var item in result.Items)
            {
                item.ProductId.ShouldNotBe(Guid.Empty);
                item.Quantity.ShouldBeGreaterThan(0);
                item.UnitPrice.ShouldBeGreaterThan(0);
            }
        }

        [Fact]
        public async Task GetOrderByIdAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync(); 

            var orderId = OrderId1;

            // Act & Assert
            await Should.ThrowAsync<TaskCanceledException>(async () =>
                await _service.GetOrderByIdAsync(orderId, cts.Token));
        }

        #endregion

        #region GetUserOrdersAsync Tests

        [Fact]
        public async Task GetUserOrdersAsync_ShouldThrow_WhenUserIdIsNull()
        {
            // Arrange
            var parameters = new OrderParameters { PageNumber = 1, PageSize = 10 };

            // Act & Assert
            await Should.ThrowAsync<ArgumentException>(async () =>
                await _service.GetUserOrdersAsync(parameters));
        }

        [Fact]
        public async Task GetUserOrdersAsync_ShouldReturnOrders_ForGivenUserId()
        {
            // Arrange
            var parameters = new OrderParameters { UserId = UserId1, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetUserOrdersAsync(parameters);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);
            result.TotalCount.ShouldBe(1);
            result[0].UserId.ShouldBe(UserId1);
        }

        [Theory]
        [InlineData("Pending", 1)]
        [InlineData("Delivered", 0)]
        public async Task GetUserOrdersAsync_ShouldFilterByStatus(string status, int expectedCount)
        {
            // Arrange
            var parameters = new OrderParameters { UserId = UserId1, Status = status, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetUserOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(expectedCount);
        }

        [Theory]
        [InlineData(50, 150, 1)]
        [InlineData(200, 300, 0)]
        public async Task GetUserOrdersAsync_ShouldFilterByPriceRange(decimal minPrice, decimal maxPrice, int expectedCount)
        {
            // Arrange
            var parameters = new OrderParameters
            {
                UserId = UserId1,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetUserOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(expectedCount);
        }

        [Fact]
        public async Task GetUserOrdersAsync_ShouldFilterByDateRange()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-3);
            var toDate = DateTime.UtcNow.AddDays(-1);
            var parameters = new OrderParameters { UserId = UserId1, FromDate = fromDate, ToDate = toDate, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetUserOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(1);
            result[0].UserId.ShouldBe(UserId1);
        }

        [Fact]
        public async Task GetUserOrdersAsync_ShouldFilterByProductId()
        {
            // Arrange
            var parameters = new OrderParameters { UserId = UserId1, ProductId = ProductId1, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetUserOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(1);
            result[0].Items.Any(i => i.ProductId == ProductId1).ShouldBeTrue();
        }

        [Fact]
        public async Task GetUserOrdersAsync_ShouldApplyPaging()
        {
            // Arrange
            var parameters = new OrderParameters { UserId = UserId1, PageNumber = 1, PageSize = 1 };

            // Act
            var result = await _service.GetUserOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(1);
            result.TotalCount.ShouldBe(1);
            result.PageSize.ShouldBe(1);
        }

        [Fact]
        public async Task GetUserOrdersAsync_ShouldReturnEmpty_WhenNoOrdersMatchFilters()
        {
            // Arrange
            var parameters = new OrderParameters
            {
                UserId = UserId1,
                MinPrice = 500, 
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetUserOrdersAsync(parameters);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(0);
            result.TotalCount.ShouldBe(0);
        }

        [Fact]
        public async Task GetUserOrdersAsync_ShouldApplyMultipleFiltersCorrectly()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-3);
            var toDate = DateTime.UtcNow.AddDays(-1);
            var parameters = new OrderParameters
            {
                UserId = UserId1,
                Status = "Pending",
                MinPrice = 50,
                MaxPrice = 150,
                FromDate = fromDate,
                ToDate = toDate,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetUserOrdersAsync(parameters);

            // Assert
            result.Count.ShouldBe(1);
            var order = result[0];
            order.Status.ShouldBe(OrderStatus.Pending.ToString());
            order.TotalPrice.ShouldBeGreaterThanOrEqualTo(50);
            order.TotalPrice.ShouldBeLessThanOrEqualTo(150);
            order.CreatedAt.ShouldBeInRange(fromDate, toDate);
        }

        [Fact]
        public async Task GetUserOrdersAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var parameters = new OrderParameters { UserId = UserId1, PageNumber = 1, PageSize = 10 };

            // Act & Assert
            await Should.ThrowAsync<TaskCanceledException>(async () =>
                await _service.GetUserOrdersAsync(parameters, cts.Token));
        }

        #endregion

        #region CreateOrderAsync Tests

        [Fact]
        public async Task CreateOrderAsync_ShouldCreateOrderSuccessfully()
        {
            // Arrange
            var dto = BuildOrderDto(
                (ProductId1, 2, 50m),
                (ProductId2, 1, 100m)
            );

            // Act
            var result = await _service.CreateOrderAsync(UserId1, dto);

            // Assert
            result.ShouldNotBeNull();
            result.UserId.ShouldBe(UserId1);
            result.Items.Count.ShouldBe(2);
            result.TotalPrice.ShouldBe(result.Items.Sum(i => i.UnitPrice * i.Quantity));

            _publishEndpointMock.Verify(x => x.Publish(It.IsAny<ProductQuantityUpdatedEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _publishEndpointMock.Verify(x => x.Publish(It.IsAny<ClearUserCartEvent>(), It.IsAny<CancellationToken>()), Times.Once);

            var orderInDb = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == result.Id);
            orderInDb.ShouldNotBeNull();
            orderInDb.TotalPrice.ShouldBe(result.TotalPrice);
        }

        [Fact]
        public async Task CreateOrderAsync_ShouldThrow_WhenQuantityExceedsAvailable()
        {
            var dto = BuildOrderDto((ProductId1, 100, 10m));

            SetupQuantityResponse(ProductId1, 10); 

            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await _service.CreateOrderAsync(UserId1, dto));
        }

        [Fact]
        public async Task CreateOrderAsync_ShouldThrow_WhenNoItemsProvided()
        {
            var dto = new CreateOrderDto
            {
                ShippingAddress = "Test address",
                Items = new List<CreateOrderItemDto>()
            };

            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await _service.CreateOrderAsync(UserId1, dto));
        }

        [Fact]
        public async Task CreateOrderAsync_ShouldCalculateTotalPriceCorrectly_ForMultipleProducts()
        {
            var dto = BuildOrderDto(
                (ProductId1, 2, 10m),
                (ProductId2, 3, 20m)
            );

            SetupPriceResponse(ProductId1, 10m);
            SetupPriceResponse(ProductId2, 20m);

            var result = await _service.CreateOrderAsync(UserId1, dto);

            result.TotalPrice.ShouldBe(2 * 10m + 3 * 20m);
        }

        [Fact]
        public async Task CreateOrderAsync_ShouldThrow_WhenOneItemQuantityExceedsAvailable()
        {
            var dto = BuildOrderDto(
                (ProductId1, 2, 50m),
                (ProductId2, 100, 20m)
            );

            SetupQuantityResponse(ProductId1, 10);
            SetupQuantityResponse(ProductId2, 50); 

            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await _service.CreateOrderAsync(UserId1, dto));
        }

        [Fact]
        public async Task CreateOrderAsync_ShouldUseCancellationToken()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var dto = BuildOrderDto((ProductId1, 1, 50m));

            await Should.ThrowAsync<TaskCanceledException>(async () =>
                await _service.CreateOrderAsync(UserId1, dto, cts.Token));
        }

        [Fact]
        public async Task CreateOrderAsync_ShouldPublishEventsWithCorrectValues()
        {
            var dto = BuildOrderDto((ProductId1, 2, 50m));

            await _service.CreateOrderAsync(UserId1, dto);

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<ProductQuantityUpdatedEvent>(
                    e => e.ProductId == ProductId1 && e.QuantityChange == -2),
                    It.IsAny<CancellationToken>()), Times.Once);

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<ClearUserCartEvent>(e => e.UserId == UserId1), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateOrderAsync_ShouldPublishEventsInCorrectOrder()
        {
            var dto = BuildOrderDto((ProductId1, 2, 50m));

            var publishedEvents = new List<string>();
            _publishEndpointMock.Setup(x => x.Publish(It.IsAny<ProductQuantityUpdatedEvent>(), It.IsAny<CancellationToken>()))
                .Callback(() => publishedEvents.Add("ProductQuantityUpdatedEvent"))
                .Returns(Task.CompletedTask);

            _publishEndpointMock.Setup(x => x.Publish(It.IsAny<ClearUserCartEvent>(), It.IsAny<CancellationToken>()))
                .Callback(() => publishedEvents.Add("ClearUserCartEvent"))
                .Returns(Task.CompletedTask);

            await _service.CreateOrderAsync(UserId1, dto);

            publishedEvents.ShouldBe(new List<string> { "ProductQuantityUpdatedEvent", "ClearUserCartEvent" });
        }

        #endregion

        #region UpdateOrderStatusAsync Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("InvalidStatus")]
        public async Task UpdateOrderStatusAsync_ShouldReturnFalse_ForInvalidStatus(string status)
        {
            // Act
            var result = await _service.UpdateOrderStatusAsync(Guid.NewGuid(), status);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateOrderStatusAsync_ShouldReturnFalse_WhenOrderNotFound()
        {
            // Act
            var result = await _service.UpdateOrderStatusAsync(Guid.NewGuid(), "Pending");

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateOrderStatusAsync_ShouldUpdateStatusSuccessfully()
        {
            // Arrange
            var order = await AddTestOrderAsync(UserId1, OrderStatus.Pending);

            // Act
            var result = await _service.UpdateOrderStatusAsync(order.Id, "Confirmed");

            // Assert
            result.ShouldBeTrue();

            var updatedOrder = await _context.Orders.FindAsync(order.Id);
            updatedOrder!.Status.ShouldBe(OrderStatus.Confirmed);

            _publishEndpointMock.Verify(x => x.Publish(It.IsAny<OrderDeliveredEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateOrderStatusAsync_ShouldPublishOrderDeliveredEvent_WhenDelivered()
        {
            // Arrange
            var order = await AddTestOrderAsync(UserId1, OrderStatus.Paid);

            // Act
            var result = await _service.UpdateOrderStatusAsync(order.Id, "Delivered");

            // Assert
            result.ShouldBeTrue();

            var updatedOrder = await _context.Orders.FindAsync(order.Id);
            updatedOrder!.Status.ShouldBe(OrderStatus.Delivered);

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<OrderDeliveredEvent>(
                    e => e.OrderId == order.Id && e.UserId == order.UserId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateOrderStatusAsync_ShouldNotPublishEvent_IfStatusAlreadyDelivered()
        {
            // Arrange
            var order = await AddTestOrderAsync(UserId1, OrderStatus.Delivered);

            // Act
            var result = await _service.UpdateOrderStatusAsync(order.Id, "Delivered");


            // Assert
            result.ShouldBeTrue();
            var updatedOrder = await _context.Orders.FindAsync(order.Id);
            updatedOrder!.Status.ShouldBe(OrderStatus.Delivered);

            _publishEndpointMock.Verify(x => x.Publish(It.IsAny<OrderDeliveredEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(OrderStatus.Pending, "Confirmed")]
        [InlineData(OrderStatus.Confirmed, "Paid")]
        public async Task UpdateOrderStatusAsync_ShouldUpdateStatus_ForValidTransitions(OrderStatus initial, string newStatus)
        {
            // Arrange
            var order = await AddTestOrderAsync(UserId1, initial);

            // Act
            var result = await _service.UpdateOrderStatusAsync(order.Id, newStatus);

            // Assert
            result.ShouldBeTrue();
            var updatedOrder = await _context.Orders.FindAsync(order.Id);
            updatedOrder!.Status.ShouldBe(Enum.Parse<OrderStatus>(newStatus));
        }

        [Theory]
        [InlineData("delivered")]
        [InlineData("DELIVERED")]
        [InlineData("DeLiVeReD")]
        public async Task UpdateOrderStatusAsync_ShouldHandleCaseInsensitiveStatus(string status)
        {
            // Arrange
            var order = await AddTestOrderAsync(UserId1, OrderStatus.Paid);

            // Act
            var result = await _service.UpdateOrderStatusAsync(order.Id, status);

            // Assert
            result.ShouldBeTrue();
            var updatedOrder = await _context.Orders.FindAsync(order.Id);
            updatedOrder!.Status.ShouldBe(OrderStatus.Delivered);

            _publishEndpointMock.Verify(x => x.Publish(It.IsAny<OrderDeliveredEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateOrderStatusAsync_ShouldReturnFalse_ForEmptyGuid()
        {
            var result = await _service.UpdateOrderStatusAsync(Guid.Empty, "Pending");
            result.ShouldBeFalse();
        }

        #endregion

        #region DeleteOrderAsync Tests

        [Fact]
        public async Task DeleteOrderAsync_ShouldReturnFalse_WhenOrderNotFound()
        {
            // Act
            var result = await _service.DeleteOrderAsync(Guid.NewGuid());

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteOrderAsync_ShouldDeleteOrderSuccessfully_WhenStatusNotDelivered()
        {
            // Arrange
            var order = await AddTestOrderAsync(UserId1, OrderStatus.Pending, withItems: true);

            // Act
            var result = await _service.DeleteOrderAsync(order.Id);

            // Assert
            result.ShouldBeTrue();

            var deletedOrder = await _context.Orders.FindAsync(order.Id);
            deletedOrder.ShouldBeNull();

            foreach (var item in order.Items)
            {
                _publishEndpointMock.Verify(x =>
                    x.Publish(It.Is<ProductQuantityUpdatedEvent>(
                        e => e.ProductId == item.ProductId && e.QuantityChange == item.Quantity),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            }

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<OrderDeletedEvent>(
                    e => e.OrderId == order.Id && e.UserId == order.UserId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteOrderAsync_ShouldDeleteOrderSuccessfully_WhenStatusDelivered()
        {
            // Arrange
            var order = await AddTestOrderAsync(UserId1, OrderStatus.Delivered, withItems: true);

            // Act
            var result = await _service.DeleteOrderAsync(order.Id);

            // Assert
            result.ShouldBeTrue();

            var deletedOrder = await _context.Orders.FindAsync(order.Id);
            deletedOrder.ShouldBeNull();

            _publishEndpointMock.Verify(x =>
                x.Publish(It.IsAny<ProductQuantityUpdatedEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<OrderDeletedEvent>(
                    e => e.OrderId == order.Id && e.UserId == order.UserId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteOrderAsync_ShouldDeleteOrderWithoutItems_WhenStatusNotDelivered()
        {
            // Arrange
            var order = await AddTestOrderAsync(UserId1, OrderStatus.Pending, withItems: false);

            // Act
            var result = await _service.DeleteOrderAsync(order.Id);

            // Assert
            result.ShouldBeTrue();

            var deletedOrder = await _context.Orders.FindAsync(order.Id);
            deletedOrder.ShouldBeNull();

            _publishEndpointMock.Verify(x =>
                x.Publish(It.IsAny<ProductQuantityUpdatedEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<OrderDeletedEvent>(
                    e => e.OrderId == order.Id && e.UserId == order.UserId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteOrderAsync_ShouldDeleteOrderWithoutItems_WhenStatusDelivered()
        {
            // Arrange
            var order = await AddTestOrderAsync(UserId1, OrderStatus.Delivered, withItems: false);

            // Act
            var result = await _service.DeleteOrderAsync(order.Id);

            // Assert
            result.ShouldBeTrue();

            var deletedOrder = await _context.Orders.FindAsync(order.Id);
            deletedOrder.ShouldBeNull();

            _publishEndpointMock.Verify(x =>
                x.Publish(It.IsAny<ProductQuantityUpdatedEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<OrderDeletedEvent>(
                    e => e.OrderId == order.Id && e.UserId == order.UserId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteOrderAsync_ShouldPublishProductQuantityUpdated_ForEachItem()
        {
            // Arrange
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = UserId1,
                Status = OrderStatus.Pending,
                ShippingAddress = "Test address",
                CreatedAt = DateTime.UtcNow,
                Items = new List<OrderItem>
                {
                    new OrderItem { Id = Guid.NewGuid(), ProductId = ProductId1, Quantity = 2 },
                    new OrderItem { Id = Guid.NewGuid(), ProductId = ProductId2, Quantity = 3 }
                }
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.DeleteOrderAsync(order.Id);

            // Assert
            result.ShouldBeTrue();

            var deletedOrder = await _context.Orders.FindAsync(order.Id);
            deletedOrder.ShouldBeNull();

            foreach (var item in order.Items)
            {
                _publishEndpointMock.Verify(x =>
                    x.Publish(It.Is<ProductQuantityUpdatedEvent>(
                        e => e.ProductId == item.ProductId && e.QuantityChange == item.Quantity),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            }

            _publishEndpointMock.Verify(x =>
                x.Publish(It.Is<OrderDeletedEvent>(
                    e => e.OrderId == order.Id && e.UserId == order.UserId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeleteOrderAsync_ShouldReturnFalse_WhenOrderAlreadyDeleted()
        {
            // Arrange
            var order = await AddTestOrderAsync(UserId1, OrderStatus.Pending, withItems: true);

            // Act
            var firstResult = await _service.DeleteOrderAsync(order.Id);
            firstResult.ShouldBeTrue();
            
            var secondResult = await _service.DeleteOrderAsync(order.Id);

            // Assert
            secondResult.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteOrderAsync_ShouldUseCancellationToken()
        {
            // Arrange
            var order = await AddTestOrderAsync(UserId1, OrderStatus.Pending, withItems: true);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Should.ThrowAsync<TaskCanceledException>(async () =>
                await _service.DeleteOrderAsync(order.Id, cts.Token));
        }

        #endregion

        #region Helper Methods

        private CreateOrderDto BuildOrderDto(params (Guid productId, int quantity, decimal price)[] items)
        {
            var dto = new CreateOrderDto
            {
                ShippingAddress = "Test address",
                Items = items.Select(item => new CreateOrderItemDto
                {
                    ProductId = item.productId,
                    Quantity = item.quantity
                }).ToList()
            };

            foreach (var item in items)
                SetupPriceResponse(item.productId, item.price);

            foreach (var item in items)
                SetupQuantityResponse(item.productId, item.quantity * 10); 

            return dto;
        }

        private void SetupPriceResponse(Guid productId, decimal price)
        {
            var priceResponse = new Mock<Response<CheckProductPriceResponse>>();
            priceResponse.SetupGet(r => r.Message).Returns(new CheckProductPriceResponse(productId, price));

            _priceRequestClientMock.Setup(x => x.GetResponse<CheckProductPriceResponse>(
                It.Is<CheckProductPriceRequest>(r => r.ProductId == productId),
                It.IsAny<CancellationToken>(),
                default)).ReturnsAsync(priceResponse.Object);
        }

        private void SetupQuantityResponse(Guid productId, int availableQuantity)
        {
            var quantityResponse = new Mock<Response<CheckProductQuantityResponse>>();
            quantityResponse.SetupGet(r => r.Message).Returns(new CheckProductQuantityResponse(productId, availableQuantity));

            _quantityRequestClientMock.Setup(x => x.GetResponse<CheckProductQuantityResponse>(
                It.Is<CheckProductQuantityRequest>(r => r.ProductId == productId),
                It.IsAny<CancellationToken>(),
                default)).ReturnsAsync(quantityResponse.Object);
        }

        private async Task<Order> AddTestOrderAsync(Guid userId, OrderStatus status, bool withItems = false)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = status,
                ShippingAddress = "Test address",
                CreatedAt = DateTime.UtcNow,
                Items = new List<OrderItem>()
            };

            if (withItems)
            {
                order.Items.Add(new OrderItem { Id = Guid.NewGuid(), ProductId = ProductId1, Quantity = 2 });
                order.Items.Add(new OrderItem { Id = Guid.NewGuid(), ProductId = ProductId2, Quantity = 3 });
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        #endregion
    }
}
