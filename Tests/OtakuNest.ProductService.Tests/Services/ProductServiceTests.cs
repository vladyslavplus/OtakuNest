using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using OtakuNest.Common.Helpers;
using OtakuNest.ProductService.Data;
using OtakuNest.ProductService.DTOs;
using OtakuNest.ProductService.Models;
using OtakuNest.ProductService.Parameters;
using Shouldly;

namespace OtakuNest.ProductService.Tests.Services
{
    public class ProductServiceTests : IDisposable
    {
        private readonly ProductDbContext _context;
        private readonly ProductService.Services.ProductService _service;
        private readonly List<Guid> _productIds;
        private bool _disposed = false;

        public ProductServiceTests()
        {
            _context = GetInMemoryDbContext();
            _service = CreateService(_context);
            _productIds = _context.Products.Select(p => p.Id).ToList();
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

        private static ProductService.Services.ProductService CreateService(ProductDbContext context)
        {
            var sortHelper = new SortHelper<Product>();
            var publishEndpointMock = new Mock<IPublishEndpoint>();

            return new ProductService.Services.ProductService(
                context,
                publishEndpointMock.Object,
                sortHelper
            );
        }

        private static ProductDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var context = new ProductDbContext(options);

            var narutoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var onePieceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var aotId = Guid.Parse("33333333-3333-3333-3333-333333333333");

            context.Products.AddRange(
                new Product
                {
                    Id = narutoId,
                    Name = "Naruto Figure",
                    Category = "Figures",
                    Price = 100,
                    IsAvailable = true,
                    Rating = 5,
                    Description = "High quality Naruto figure",
                    Quantity = 10,
                    ImageUrl = "https://example.com/naruto.jpg",
                    SKU = "FIG-NAR-TEST",
                    Tags = "anime,figure,naruto",
                    Discount = 0m
                },
                new Product
                {
                    Id = onePieceId,
                    Name = "One Piece Poster",
                    Category = "Posters",
                    Price = 20,
                    IsAvailable = true,
                    Rating = 4,
                    Description = "Beautiful One Piece poster",
                    Quantity = 5,
                    ImageUrl = "https://example.com/poster.jpg",
                    SKU = "POST-ONEPIECE-TEST",
                    Tags = "anime,poster,onepiece",
                    Discount = 0m
                },
                new Product
                {
                    Id = aotId,
                    Name = "Attack on Titan T-Shirt",
                    Category = "Clothes",
                    Price = 50,
                    IsAvailable = false,
                    Rating = 3,
                    Description = "Comfortable AoT t-shirt",
                    Quantity = 0,
                    ImageUrl = "https://example.com/aot-shirt.jpg",
                    SKU = "CLO-AOT-TEST",
                    Tags = "anime,clothing,aot",
                    Discount = 0m
                }
            );
            context.SaveChanges();
            return context;
        }

        #region Filtering Tests

        [Fact]
        public async Task GetAllAsync_Should_Filter_By_Name_Partial_Match()
        {
            // Arrange
            var parameters = new ProductParameters
            {
                Name = "One Piece",
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);
            result.Single().Name.ShouldBe("One Piece Poster");
        }

        [Fact]
        public async Task GetAllAsync_Should_Filter_By_Category()
        {
            // Arrange
            var parameters = new ProductParameters
            {
                Category = "Figures",
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);
            result.Single().Category.ShouldBe("Figures");
        }

        [Fact]
        public async Task GetAllAsync_Should_Filter_By_SKU_Exact_Match()
        {
            // Arrange
            var parameters = new ProductParameters
            {
                SKU = "CLO-AOT-TEST",
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);
            result.Single().SKU.ShouldBe("CLO-AOT-TEST");
        }

        [Theory]
        [InlineData(30, 2)] 
        [InlineData(60, 1)] 
        [InlineData(150, 0)] 
        public async Task GetAllAsync_Should_Filter_By_MinPrice(decimal minPrice, int expectedCount)
        {
            // Arrange
            var parameters = new ProductParameters
            {
                MinPrice = minPrice,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(expectedCount);
            result.All(p => p.Price >= minPrice).ShouldBeTrue();
        }

        [Theory]
        [InlineData(30, 1)]
        [InlineData(60, 2)]
        [InlineData(150, 3)] 
        public async Task GetAllAsync_Should_Filter_By_MaxPrice(decimal maxPrice, int expectedCount)
        {
            // Arrange
            var parameters = new ProductParameters
            {
                MaxPrice = maxPrice,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(expectedCount);
            result.All(p => p.Price <= maxPrice).ShouldBeTrue();
        }

        [Theory]
        [InlineData(true, 2)]  
        [InlineData(false, 1)] 
        public async Task GetAllAsync_Should_Filter_By_Availability(bool isAvailable, int expectedCount)
        {
            // Arrange
            var parameters = new ProductParameters
            {
                IsAvailable = isAvailable,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(expectedCount);
            result.All(p => p.IsAvailable == isAvailable).ShouldBeTrue();
        }

        [Theory]
        [InlineData(4, 2)]
        [InlineData(5, 1)] 
        [InlineData(6, 0)] 
        public async Task GetAllAsync_Should_Filter_By_MinRating(int minRating, int expectedCount)
        {
            // Arrange
            var parameters = new ProductParameters
            {
                MinRating = minRating,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(expectedCount);
            result.All(p => p.Rating >= minRating).ShouldBeTrue();
        }

        [Fact]
        public async Task GetAllAsync_Should_Apply_Multiple_Filters()
        {
            // Arrange
            var parameters = new ProductParameters
            {
                Category = "Figures",
                MinPrice = 50,
                IsAvailable = true,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);

            var product = result.Single();
            product.Category.ShouldBe("Figures");
            product.Price.ShouldBeGreaterThanOrEqualTo(50);
            product.IsAvailable.ShouldBeTrue();
        }

        #endregion

        #region Sorting Tests

        [Theory]
        [InlineData("Name", "Attack on Titan T-Shirt")] 
        [InlineData("Name desc", "One Piece Poster")]   
        [InlineData("Price", "One Piece Poster")]       
        [InlineData("Price desc", "Naruto Figure")]     
        public async Task GetAllAsync_Should_Sort_Products_Correctly(string orderBy, string expectedFirstProductName)
        {
            // Arrange
            var parameters = new ProductParameters
            {
                OrderBy = orderBy,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBeGreaterThan(0);
            result[0].Name.ShouldBe(expectedFirstProductName);
        }

        #endregion

        #region Pagination Tests

        [Theory]
        [InlineData(1, 2, 2)] 
        [InlineData(2, 2, 1)] 
        [InlineData(3, 2, 0)] 
        public async Task GetAllAsync_Should_Handle_Pagination_Correctly(int pageNumber, int pageSize, int expectedCount)
        {
            // Arrange
            var parameters = new ProductParameters
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(expectedCount);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task GetAllAsync_Should_Return_All_When_No_Filters_Applied()
        {
            // Arrange
            var parameters = new ProductParameters();

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(3);
        }

        [Fact]
        public async Task GetAllAsync_Should_Return_Empty_When_No_Products_Match_Filter()
        {
            // Arrange
            var parameters = new ProductParameters
            {
                Name = "NonExistentProduct"
            };

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetAllAsync_Should_Handle_Zero_Page_Size()
        {
            // Arrange
            var parameters = new ProductParameters
            {
                PageNumber = 1,
                PageSize = 0
            };

            // Act
            var result = await _service.GetAllAsync(parameters, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
        }

        #endregion

        #region GetById Tests

        [Fact]
        public async Task GetByIdAsync_Should_Return_Product_When_Exists()
        {
            // Arrange
            var existingId = _productIds[0];

            // Act
            var result = await _service.GetByIdAsync(existingId, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Id.ShouldBe(existingId);
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Null_When_Product_Does_Not_Exist()
        {
            // Arrange
            var nonExistingId = Guid.NewGuid();

            // Act
            var result = await _service.GetByIdAsync(nonExistingId, CancellationToken.None);

            // Assert
            result.ShouldBeNull();
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Null_For_Empty_Guid()
        {
            // Act
            var result = await _service.GetByIdAsync(Guid.Empty, CancellationToken.None);

            // Assert
            result.ShouldBeNull();
        }

        #endregion

        #region CreateAsync Tests 

        [Fact]
        public async Task CreateAsync_Should_Create_Product_And_Return_ProductDto()
        {
            // Arrange
            var createDto = new ProductCreateDto
            {
                Name = "Dragon Ball Figure",
                Description = "High quality Dragon Ball figure",
                Price = 75.99m,
                Quantity = 5,
                ImageUrl = "https://example.com/dragonball.jpg",
                Category = "Figures",
                SKU = "FIG-DB-001",
                IsAvailable = true,
                Rating = 4,
                Tags = "anime,figure,dragonball",
                Discount = 10m
            };

            // Act
            var result = await _service.CreateAsync(createDto, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Id.ShouldNotBe(Guid.Empty);
            result.Name.ShouldBe(createDto.Name);
            result.Description.ShouldBe(createDto.Description);
            result.Price.ShouldBe(createDto.Price);
            result.Quantity.ShouldBe(createDto.Quantity);
            result.ImageUrl.ShouldBe(createDto.ImageUrl);
            result.Category.ShouldBe(createDto.Category);
            result.SKU.ShouldBe(createDto.SKU);
            result.IsAvailable.ShouldBe(createDto.IsAvailable);
            result.Rating.ShouldBe(createDto.Rating);
            result.Tags.ShouldBe(createDto.Tags);
            result.Discount.ShouldBe(createDto.Discount);
        }

        [Fact]
        public async Task CreateAsync_Should_Save_Product_To_Database()
        {
            // Arrange
            var createDto = new ProductCreateDto
            {
                Name = "Test Product",
                Description = "Test Description",
                Price = 50m,
                Quantity = 3,
                ImageUrl = "https://example.com/test.jpg",
                Category = "Test",
                SKU = "TEST-001",
                IsAvailable = true,
                Rating = 5,
                Tags = "test",
                Discount = 0m
            };

            var initialCount = await _context.Products.CountAsync();

            // Act
            var result = await _service.CreateAsync(createDto, CancellationToken.None);

            // Assert
            var finalCount = await _context.Products.CountAsync();
            finalCount.ShouldBe(initialCount + 1);

            var savedProduct = await _context.Products.FindAsync(result.Id);
            savedProduct.ShouldNotBeNull();
            savedProduct.Name.ShouldBe(createDto.Name);
            savedProduct.CreatedAt.ShouldBeGreaterThan(DateTime.MinValue);
            savedProduct.UpdatedAt.ShouldBeGreaterThan(DateTime.MinValue);
        }

        [Fact]
        public async Task CreateAsync_Should_Set_CreatedAt_And_UpdatedAt()
        {
            // Arrange
            var beforeCreate = DateTime.UtcNow;
            var createDto = new ProductCreateDto
            {
                Name = "Time Test Product",
                Description = "Testing timestamps",
                Price = 25m,
                Quantity = 1,
                ImageUrl = "https://example.com/time-test.jpg",
                Category = "Test",
                SKU = "TIME-001",
                IsAvailable = true,
                Rating = 3,
                Tags = "test,time",
                Discount = 0m
            };

            // Act
            var result = await _service.CreateAsync(createDto, CancellationToken.None);
            var afterCreate = DateTime.UtcNow;

            // Assert
            var savedProduct = await _context.Products.FindAsync(result.Id);
            savedProduct.ShouldNotBeNull();
            savedProduct.CreatedAt.ShouldBeGreaterThanOrEqualTo(beforeCreate);
            savedProduct.CreatedAt.ShouldBeLessThanOrEqualTo(afterCreate);
            savedProduct.UpdatedAt.ShouldBeGreaterThanOrEqualTo(beforeCreate);
            savedProduct.UpdatedAt.ShouldBeLessThanOrEqualTo(afterCreate);
        }

        [Fact]
        public async Task CreateAsync_Should_Generate_Unique_Guid()
        {
            // Arrange
            var createDto1 = new ProductCreateDto
            {
                Name = "Product 1",
                Description = "First product",
                Price = 10m,
                Quantity = 1,
                ImageUrl = "https://example.com/product1.jpg",
                Category = "Test",
                SKU = "UNIQUE-001",
                IsAvailable = true,
                Rating = 5,
                Tags = "test",
                Discount = 0m
            };

            var createDto2 = new ProductCreateDto
            {
                Name = "Product 2",
                Description = "Second product",
                Price = 20m,
                Quantity = 2,
                ImageUrl = "https://example.com/product2.jpg",
                Category = "Test",
                SKU = "UNIQUE-002",
                IsAvailable = true,
                Rating = 4,
                Tags = "test",
                Discount = 0m
            };

            // Act
            var result1 = await _service.CreateAsync(createDto1, CancellationToken.None);
            var result2 = await _service.CreateAsync(createDto2, CancellationToken.None);

            // Assert
            result1.Id.ShouldNotBe(Guid.Empty);
            result2.Id.ShouldNotBe(Guid.Empty);
            result1.Id.ShouldNotBe(result2.Id);
        }

        [Fact]
        public async Task CreateAsync_Should_Handle_Valid_Minimal_Data()
        {
            // Arrange
            var createDto = new ProductCreateDto
            {
                Name = "Minimal Product",
                Description = "Valid description",
                Price = 15m,
                Quantity = 1,
                ImageUrl = "https://example.com/minimal.jpg",
                Category = "Test",
                SKU = "MIN-001",
                IsAvailable = true,
                Rating = 3,
                Tags = "minimal,test",
                Discount = 0m
            };

            // Act
            var result = await _service.CreateAsync(createDto, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Id.ShouldNotBe(Guid.Empty);
            result.Name.ShouldBe("Minimal Product");
            result.Description.ShouldBe("Valid description");
            result.ImageUrl.ShouldBe("https://example.com/minimal.jpg");
            result.Tags.ShouldBe("minimal,test");
        }

        [Fact]
        public async Task CreateAsync_Should_Handle_Maximum_Field_Lengths()
        {
            // Arrange
            var longName = new string('A', 100);
            var longDescription = new string('B', 1000);
            var longCategory = new string('C', 50);
            var longSKU = new string('D', 50);
            var longTags = new string('T', 200);

            var createDto = new ProductCreateDto
            {
                Name = longName,
                Description = longDescription,
                Price = 99.99m,
                Quantity = 999,
                ImageUrl = "https://example.com/long-field-test.jpg",
                Category = longCategory,
                SKU = longSKU,
                IsAvailable = true,
                Rating = 5,
                Tags = longTags,
                Discount = 50m
            };

            // Act
            var result = await _service.CreateAsync(createDto, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Name.ShouldBe(longName);
            result.Description.ShouldBe(longDescription);
            result.Category.ShouldBe(longCategory);
            result.SKU.ShouldBe(longSKU);
            result.Tags.ShouldBe(longTags);
        }

        [Theory]
        [InlineData(0.1)]
        [InlineData(99.99)]
        [InlineData(1000.50)]
        public async Task CreateAsync_Should_Handle_Valid_Prices(decimal price)
        {
            // Arrange
            var createDto = new ProductCreateDto
            {
                Name = $"Price Test Product {price}",
                Description = "Testing valid prices",
                Price = price,
                Quantity = 1,
                ImageUrl = "https://example.com/price-test.jpg",
                Category = "Test",
                SKU = $"PRICE-{price:0.00}".Replace(".", ""),
                IsAvailable = true,
                Rating = 4,
                Tags = "test,price",
                Discount = 0m
            };

            // Act
            var result = await _service.CreateAsync(createDto, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Price.ShouldBe(price);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public async Task CreateAsync_Should_Handle_Valid_Ratings(double rating)
        {
            // Arrange
            var createDto = new ProductCreateDto
            {
                Name = $"Rating Test Product {rating}",
                Description = "Testing valid ratings",
                Price = 25m,
                Quantity = 1,
                ImageUrl = "https://example.com/rating-test.jpg",
                Category = "Test",
                SKU = $"RATING-{rating}",
                IsAvailable = true,
                Rating = rating,
                Tags = "test,rating",
                Discount = 0m
            };

            // Act
            var result = await _service.CreateAsync(createDto, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Rating.ShouldBe(rating);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(25.5)]
        [InlineData(50)]
        [InlineData(99.99)]
        [InlineData(100)]
        public async Task CreateAsync_Should_Handle_Valid_Discounts(decimal discount)
        {
            // Arrange
            var createDto = new ProductCreateDto
            {
                Name = $"Discount Test Product {discount}",
                Description = "Testing valid discounts",
                Price = 100m,
                Quantity = 1,
                ImageUrl = "https://example.com/discount-test.jpg",
                Category = "Test",
                SKU = $"DISC-{discount:0.00}".Replace(".", ""),
                IsAvailable = true,
                Rating = 3,
                Tags = "test,discount",
                Discount = discount
            };

            // Act
            var result = await _service.CreateAsync(createDto, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Discount.ShouldBe(discount);
        }

        [Fact]
        public async Task CreateAsync_Should_Handle_Decimal_Precision()
        {
            // Arrange
            var createDto = new ProductCreateDto
            {
                Name = "Precision Test Product",
                Description = "Testing decimal precision",
                Price = 99.999m,
                Quantity = 1,
                ImageUrl = "https://example.com/precision.jpg",
                Category = "Test",
                SKU = "PREC-001",
                IsAvailable = true,
                Rating = 5,
                Tags = "test,precision",
                Discount = 15.555m
            };

            // Act
            var result = await _service.CreateAsync(createDto, CancellationToken.None);

            // Assert
            result.ShouldNotBeNull();
            result.Price.ShouldBe(99.999m);
            result.Discount.ShouldBe(15.555m);
        }

        [Fact]
        public async Task CreateAsync_Should_Handle_Cancellation_Token()
        {
            // Arrange
            var createDto = new ProductCreateDto
            {
                Name = "Cancellation Test Product",
                Description = "Testing cancellation",
                Price = 30m,
                Quantity = 1,
                ImageUrl = "https://example.com/cancel.jpg",
                Category = "Test",
                SKU = "CANCEL-001",
                IsAvailable = true,
                Rating = 3,
                Tags = "test",
                Discount = 0m
            };

            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await _service.CreateAsync(createDto, cts.Token);
            result.ShouldNotBeNull();
        }

        #endregion

        #region UpdateAsync Tests 

        [Fact]
        public async Task UpdateAsync_Should_Update_All_Fields_When_All_Provided()
        {
            // Arrange
            var existingProductId = _productIds[0];
            var updateDto = new ProductUpdateDto
            {
                Name = "Updated Naruto Figure",
                Description = "Updated description",
                Price = 120.50m,
                Quantity = 15,
                ImageUrl = "https://example.com/updated-naruto.jpg",
                Category = "Updated Figures",
                SKU = "FIG-NAR-UPDATED",
                IsAvailable = false,
                Rating = 4.5,
                Tags = "anime,updated,figure",
                Discount = 15m
            };

            // Act
            var result = await _service.UpdateAsync(existingProductId, updateDto, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var updatedProduct = await _context.Products.FindAsync(existingProductId);
            updatedProduct.ShouldNotBeNull();
            updatedProduct.Name.ShouldBe(updateDto.Name);
            updatedProduct.Description.ShouldBe(updateDto.Description);
            updatedProduct.Price.ShouldBe(updateDto.Price.Value);
            updatedProduct.Quantity.ShouldBe(updateDto.Quantity.Value);
            updatedProduct.ImageUrl.ShouldBe(updateDto.ImageUrl);
            updatedProduct.Category.ShouldBe(updateDto.Category);
            updatedProduct.SKU.ShouldBe(updateDto.SKU);
            updatedProduct.IsAvailable.ShouldBe(updateDto.IsAvailable.Value);
            updatedProduct.Rating.ShouldBe(updateDto.Rating.Value);
            updatedProduct.Tags.ShouldBe(updateDto.Tags);
            updatedProduct.Discount.ShouldBe(updateDto.Discount.Value);
        }

        [Fact]
        public async Task UpdateAsync_Should_Update_Only_Provided_Fields()
        {
            // Arrange
            var existingProductId = _productIds[0];
            var originalProduct = await _context.Products.FindAsync(existingProductId);
            var originalPrice = originalProduct!.Price;

            var updateDto = new ProductUpdateDto
            {
                Name = "Updated Name Only",
                Description = null, 
                Price = null,
                Quantity = 25,
                ImageUrl = null, 
                Category = null, 
                SKU = null, 
                IsAvailable = null, 
                Rating = null, 
                Tags = null, 
                Discount = null 
            };

            // Act
            var result = await _service.UpdateAsync(existingProductId, updateDto, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var updatedProduct = await _context.Products.FindAsync(existingProductId);
            updatedProduct.ShouldNotBeNull();
            updatedProduct.Name.ShouldBe("Updated Name Only"); 
            updatedProduct.Quantity.ShouldBe(25);
            updatedProduct.Price.ShouldBe(originalPrice); 
            updatedProduct.Description.ShouldBe(originalProduct.Description); 
        }

        [Fact]
        public async Task UpdateAsync_Should_Update_UpdatedAt_Timestamp()
        {
            // Arrange
            var existingProductId = _productIds[0];
            var originalProduct = await _context.Products.FindAsync(existingProductId);
            var originalUpdatedAt = originalProduct!.UpdatedAt;

            var beforeUpdate = DateTime.UtcNow;
            await Task.Delay(10); 

            var updateDto = new ProductUpdateDto
            {
                Name = "Time Update Test"
            };

            // Act
            var result = await _service.UpdateAsync(existingProductId, updateDto, CancellationToken.None);
            var afterUpdate = DateTime.UtcNow;

            // Assert
            result.ShouldBeTrue();

            var updatedProduct = await _context.Products.FindAsync(existingProductId);
            updatedProduct.ShouldNotBeNull();
            updatedProduct.UpdatedAt.ShouldBeGreaterThan(originalUpdatedAt);
            updatedProduct.UpdatedAt.ShouldBeGreaterThanOrEqualTo(beforeUpdate);
            updatedProduct.UpdatedAt.ShouldBeLessThanOrEqualTo(afterUpdate);
        }

        [Fact]
        public async Task UpdateAsync_Should_Return_False_When_Product_Not_Found()
        {
            // Arrange
            var nonExistingId = Guid.NewGuid();
            var updateDto = new ProductUpdateDto
            {
                Name = "This should not work"
            };

            // Act
            var result = await _service.UpdateAsync(nonExistingId, updateDto, CancellationToken.None);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_Should_Return_False_For_Empty_Guid()
        {
            // Arrange
            var updateDto = new ProductUpdateDto
            {
                Name = "Empty Guid Test"
            };

            // Act
            var result = await _service.UpdateAsync(Guid.Empty, updateDto, CancellationToken.None);

            // Assert
            result.ShouldBeFalse();
        }


        [Theory]
        [InlineData("Name", "Updated Product Name")]
        [InlineData("Description", "Updated description text")]
        [InlineData("ImageUrl", "https://example.com/updated-image.jpg")]
        [InlineData("Category", "Updated Category")]
        [InlineData("SKU", "UPDATED-SKU-001")]
        [InlineData("Tags", "updated,tags,test")]
        public async Task UpdateAsync_Should_Update_Individual_String_Fields(string fieldName, string newValue)
        {
            // Arrange
            var existingProductId = _productIds[1]; 
            var updateDto = new ProductUpdateDto();

            switch (fieldName)
            {
                case "Name":
                    updateDto.Name = newValue;
                    break;
                case "Description":
                    updateDto.Description = newValue;
                    break;
                case "ImageUrl":
                    updateDto.ImageUrl = newValue;
                    break;
                case "Category":
                    updateDto.Category = newValue;
                    break;
                case "SKU":
                    updateDto.SKU = newValue;
                    break;
                case "Tags":
                    updateDto.Tags = newValue;
                    break;
            }

            // Act
            var result = await _service.UpdateAsync(existingProductId, updateDto, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var updatedProduct = await _context.Products.FindAsync(existingProductId);
            updatedProduct.ShouldNotBeNull();

            switch (fieldName)
            {
                case "Name":
                    updatedProduct.Name.ShouldBe(newValue);
                    break;
                case "Description":
                    updatedProduct.Description.ShouldBe(newValue);
                    break;
                case "ImageUrl":
                    updatedProduct.ImageUrl.ShouldBe(newValue);
                    break;
                case "Category":
                    updatedProduct.Category.ShouldBe(newValue);
                    break;
                case "SKU":
                    updatedProduct.SKU.ShouldBe(newValue);
                    break;
                case "Tags":
                    updatedProduct.Tags.ShouldBe(newValue);
                    break;
            }
        }

        [Theory]
        [InlineData("Price", 199.99)]
        [InlineData("Quantity", 50)]
        [InlineData("Rating", 4.8)]
        [InlineData("Discount", 25.5)]
        public async Task UpdateAsync_Should_Update_Individual_Numeric_Fields(string fieldName, decimal newValue)
        {
            // Arrange
            var existingProductId = _productIds[2]; 
            var updateDto = new ProductUpdateDto();

            switch (fieldName)
            {
                case "Price":
                    updateDto.Price = newValue;
                    break;
                case "Quantity":
                    updateDto.Quantity = (int)newValue;
                    break;
                case "Rating":
                    updateDto.Rating = (double)newValue;
                    break;
                case "Discount":
                    updateDto.Discount = newValue;
                    break;
            }

            // Act
            var result = await _service.UpdateAsync(existingProductId, updateDto, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var updatedProduct = await _context.Products.FindAsync(existingProductId);
            updatedProduct.ShouldNotBeNull();

            switch (fieldName)
            {
                case "Price":
                    updatedProduct.Price.ShouldBe(newValue);
                    break;
                case "Quantity":
                    updatedProduct.Quantity.ShouldBe((int)newValue);
                    break;
                case "Rating":
                    updatedProduct.Rating.ShouldBe((double)newValue);
                    break;
                case "Discount":
                    updatedProduct.Discount.ShouldBe(newValue);
                    break;
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UpdateAsync_Should_Update_IsAvailable_Field(bool newAvailability)
        {
            // Arrange
            var existingProductId = _productIds[0];
            var updateDto = new ProductUpdateDto
            {
                IsAvailable = newAvailability
            };

            // Act
            var result = await _service.UpdateAsync(existingProductId, updateDto, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var updatedProduct = await _context.Products.FindAsync(existingProductId);
            updatedProduct.ShouldNotBeNull();
            updatedProduct.IsAvailable.ShouldBe(newAvailability);
        }
        
        [Fact]
        public async Task UpdateAsync_Should_Not_Change_CreatedAt()
        {
            // Arrange
            var existingProductId = _productIds[0];
            var originalProduct = await _context.Products.FindAsync(existingProductId);
            var originalCreatedAt = originalProduct!.CreatedAt;

            var updateDto = new ProductUpdateDto
            {
                Name = "CreatedAt Test Update"
            };

            // Act
            var result = await _service.UpdateAsync(existingProductId, updateDto, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var updatedProduct = await _context.Products.FindAsync(existingProductId);
            updatedProduct.ShouldNotBeNull();
            updatedProduct.CreatedAt.ShouldBe(originalCreatedAt);
        }

        [Fact]
        public async Task UpdateAsync_Should_Handle_Empty_Update_Dto()
        {
            // Arrange
            var existingProductId = _productIds[0];
            var originalProduct = await _context.Products.FindAsync(existingProductId);
            var originalUpdatedAt = originalProduct!.UpdatedAt;
            var updateDto = new ProductUpdateDto(); 

            // Act
            var result = await _service.UpdateAsync(existingProductId, updateDto, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var updatedProduct = await _context.Products.FindAsync(existingProductId);
            updatedProduct.ShouldNotBeNull();

            updatedProduct.Name.ShouldBe(originalProduct!.Name);
            updatedProduct.Description.ShouldBe(originalProduct.Description);
            updatedProduct.Price.ShouldBe(originalProduct.Price);
            updatedProduct.Quantity.ShouldBe(originalProduct.Quantity);
            updatedProduct.UpdatedAt.ShouldBeGreaterThan(originalUpdatedAt);
        }

        [Fact]
        public async Task UpdateAsync_Should_Handle_Cancellation_Token()
        {
            // Arrange
            var existingProductId = _productIds[0];
            var updateDto = new ProductUpdateDto
            {
                Name = "Cancellation Test Update"
            };

            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await _service.UpdateAsync(existingProductId, updateDto, cts.Token);
            result.ShouldBeTrue();
        }

        [Fact]
        public async Task UpdateAsync_Should_Handle_Decimal_Precision()
        {
            // Arrange
            var existingProductId = _productIds[0];
            var updateDto = new ProductUpdateDto
            {
                Price = 99.999m,
                Discount = 15.555m
            };

            // Act
            var result = await _service.UpdateAsync(existingProductId, updateDto, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var updatedProduct = await _context.Products.FindAsync(existingProductId);
            updatedProduct.ShouldNotBeNull();
            updatedProduct.Price.ShouldBe(99.999m);
            updatedProduct.Discount.ShouldBe(15.555m);
        }
        #endregion

        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_Should_Delete_Product_And_Return_True_When_Product_Exists()
        {
            // Arrange
            var existingId = _productIds[0];
            var initialCount = await _context.Products.CountAsync();

            // Act
            var result = await _service.DeleteAsync(existingId, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var finalCount = await _context.Products.CountAsync();
            finalCount.ShouldBe(initialCount - 1);

            var deletedProduct = await _context.Products.FindAsync(existingId);
            deletedProduct.ShouldBeNull();
        }

        [Fact]
        public async Task DeleteAsync_Should_Return_False_When_Product_Does_Not_Exist()
        {
            // Arrange
            var nonExistingId = Guid.NewGuid();
            var initialCount = await _context.Products.CountAsync();

            // Act
            var result = await _service.DeleteAsync(nonExistingId, CancellationToken.None);

            // Assert
            result.ShouldBeFalse();

            var finalCount = await _context.Products.CountAsync();
            finalCount.ShouldBe(initialCount);
        }

        [Fact]
        public async Task DeleteAsync_Should_Return_False_For_Empty_Guid()
        {
            // Arrange
            var initialCount = await _context.Products.CountAsync();

            // Act
            var result = await _service.DeleteAsync(Guid.Empty, CancellationToken.None);

            // Assert
            result.ShouldBeFalse();

            var finalCount = await _context.Products.CountAsync();
            finalCount.ShouldBe(initialCount);
        }

        [Fact]
        public async Task DeleteAsync_Should_Remove_Product_From_Database()
        {
            // Arrange
            var productIdToDelete = _productIds[1];
            var productToDelete = await _context.Products.FindAsync(productIdToDelete);
            productToDelete.ShouldNotBeNull();

            // Act
            var result = await _service.DeleteAsync(productIdToDelete, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var deletedProduct = await _context.Products.FindAsync(productIdToDelete);
            deletedProduct.ShouldBeNull();

            var remainingProducts = await _context.Products.ToListAsync();
            remainingProducts.Count.ShouldBe(2);
            remainingProducts.ShouldNotContain(p => p.Id == productIdToDelete);
        }

        [Fact]
        public async Task DeleteAsync_Should_Not_Affect_Other_Products()
        {
            // Arrange
            var productIdToDelete = _productIds[0];
            var otherProductIds = _productIds.Skip(1).ToList();

            // Act
            var result = await _service.DeleteAsync(productIdToDelete, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            foreach (var otherId in otherProductIds)
            {
                var otherProduct = await _context.Products.FindAsync(otherId);
                otherProduct.ShouldNotBeNull();
            }
        }

        [Fact]
        public async Task DeleteAsync_Should_Handle_Multiple_Deletions()
        {
            // Arrange
            var firstProductId = _productIds[0];
            var secondProductId = _productIds[1];
            var initialCount = await _context.Products.CountAsync();

            // Act
            var firstResult = await _service.DeleteAsync(firstProductId, CancellationToken.None);
            var secondResult = await _service.DeleteAsync(secondProductId, CancellationToken.None);

            // Assert
            firstResult.ShouldBeTrue();
            secondResult.ShouldBeTrue();

            var finalCount = await _context.Products.CountAsync();
            finalCount.ShouldBe(initialCount - 2);

            var firstDeletedProduct = await _context.Products.FindAsync(firstProductId);
            var secondDeletedProduct = await _context.Products.FindAsync(secondProductId);
            firstDeletedProduct.ShouldBeNull();
            secondDeletedProduct.ShouldBeNull();
        }

        [Fact]
        public async Task DeleteAsync_Should_Handle_Attempting_To_Delete_Same_Product_Twice()
        {
            // Arrange
            var productId = _productIds[0];

            // Act
            var firstDeleteResult = await _service.DeleteAsync(productId, CancellationToken.None);
            var secondDeleteResult = await _service.DeleteAsync(productId, CancellationToken.None);

            // Assert
            firstDeleteResult.ShouldBeTrue();
            secondDeleteResult.ShouldBeFalse();

            var deletedProduct = await _context.Products.FindAsync(productId);
            deletedProduct.ShouldBeNull();
        }

        [Fact]
        public async Task DeleteAsync_Should_Handle_Cancellation_Token()
        {
            // Arrange
            var existingId = _productIds[2];
            using var cts = new CancellationTokenSource();

            // Act & Assert
            var result = await _service.DeleteAsync(existingId, cts.Token);
            result.ShouldBeTrue();

            var deletedProduct = await _context.Products.FindAsync(existingId);
            deletedProduct.ShouldBeNull();
        }

        [Fact]
        public async Task DeleteAsync_Should_Delete_Product_With_All_Properties()
        {
            // Arrange
            var productId = _productIds[0];
            var productToDelete = await _context.Products.FindAsync(productId);

            productToDelete.ShouldNotBeNull();
            productToDelete.Name.ShouldBe("Naruto Figure");
            productToDelete.Category.ShouldBe("Figures");
            productToDelete.Price.ShouldBe(100);

            // Act
            var result = await _service.DeleteAsync(productId, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var deletedProduct = await _context.Products.FindAsync(productId);
            deletedProduct.ShouldBeNull();
        }

        [Theory]
        [InlineData(0)] 
        [InlineData(1)]
        [InlineData(2)] 
        public async Task DeleteAsync_Should_Successfully_Delete_Any_Valid_Product(int productIndex)
        {
            // Arrange
            var productId = _productIds[productIndex];
            var initialCount = await _context.Products.CountAsync();

            // Act
            var result = await _service.DeleteAsync(productId, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var finalCount = await _context.Products.CountAsync();
            finalCount.ShouldBe(initialCount - 1);

            var deletedProduct = await _context.Products.FindAsync(productId);
            deletedProduct.ShouldBeNull();
        }

        [Fact]
        public async Task DeleteAsync_Should_Maintain_Database_Consistency_After_Deletion()
        {
            // Arrange
            var productIdToDelete = _productIds[1];
            var remainingProductIds = _productIds.Where(id => id != productIdToDelete).ToList();

            // Act
            var result = await _service.DeleteAsync(productIdToDelete, CancellationToken.None);

            // Assert
            result.ShouldBeTrue();

            var allProducts = await _context.Products.ToListAsync();
            allProducts.Count.ShouldBe(2);

            foreach (var remainingId in remainingProductIds)
            {
                var product = allProducts.FirstOrDefault(p => p.Id == remainingId);
                product.ShouldNotBeNull();
            }

            var deletedProduct = allProducts.FirstOrDefault(p => p.Id == productIdToDelete);
            deletedProduct.ShouldBeNull();
        }

        #endregion
    }
}