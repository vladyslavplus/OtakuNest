using Microsoft.EntityFrameworkCore;
using OtakuNest.ProductService.Models;

namespace OtakuNest.ProductService.Data
{
    public class ProductDbContext : DbContext
    {
        public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.SKU)
                .IsUnique();

            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = Guid.Parse("c3d89b16-7a72-4d1e-a79c-90d8e9a1c47f"),
                    Name = "Naruto Figure",
                    Description = "Action figure of Naruto Uzumaki",
                    Price = 25.99m,
                    Quantity = 10,
                    ImageUrl = "https://example.com/images/naruto_figure.jpg",
                    Category = "Figures",
                    SKU = "NF-001",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.5,
                    Tags = "naruto,figure,anime",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("87c9d1eb-2f53-4a2f-a3f1-11b5c5c4348a"),
                    Name = "Sailor Moon Poster",
                    Description = "Wall poster of Sailor Moon",
                    Price = 10.50m,
                    Quantity = 20,
                    ImageUrl = "https://example.com/images/sailor_moon_poster.jpg",
                    Category = "Posters",
                    SKU = "SM-002",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.0,
                    Tags = "sailormoon,poster,anime",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("5a97d027-46bf-46ee-95c0-064c5a6a0e32"),
                    Name = "One Piece Mug",
                    Description = "Mug with One Piece theme",
                    Price = 12.00m,
                    Quantity = 15,
                    ImageUrl = "https://example.com/images/one_piece_mug.jpg",
                    Category = "Mugs",
                    SKU = "OP-003",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.2,
                    Tags = "onepiece,mug,anime",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("a52399cd-6511-4ed9-b559-cf0e412eb4f4"),
                    Name = "Attack on Titan Hoodie",
                    Description = "Hoodie with AOT logo",
                    Price = 40.00m,
                    Quantity = 8,
                    ImageUrl = "https://example.com/images/aot_hoodie.jpg",
                    Category = "Clothing",
                    SKU = "AOT-004",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.7,
                    Tags = "attackontitan,hoodie,anime",
                    Discount = 5.0m
                },
                new Product
                {
                    Id = Guid.Parse("f43ecddb-5cc8-4f43-bca1-58f0977eae91"),
                    Name = "Dragon Ball Keychain",
                    Description = "Keychain with Dragon Ball emblem",
                    Price = 5.99m,
                    Quantity = 30,
                    ImageUrl = "https://example.com/images/dragon_ball_keychain.jpg",
                    Category = "Accessories",
                    SKU = "DB-005",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.1,
                    Tags = "dragonball,keychain,anime",
                    Discount = 0m
                }
            );
        }
    }
}
