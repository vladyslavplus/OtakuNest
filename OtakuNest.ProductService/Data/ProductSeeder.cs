using MassTransit;
using Microsoft.EntityFrameworkCore;
using OtakuNest.Contracts;
using OtakuNest.ProductService.Models;

namespace OtakuNest.ProductService.Data
{
    public class ProductSeeder
    {
        private readonly ProductDbContext _db;
        private readonly IPublishEndpoint _publishEndpoint;

        public ProductSeeder(ProductDbContext db, IPublishEndpoint publishEndpoint)
        {
            _db = db;
            _publishEndpoint = publishEndpoint;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (await _db.Products.AnyAsync(cancellationToken))
                return;

            var products = new List<Product>
            {
                new Product
                {
                    Id = Guid.Parse("d8e3b9b9-7656-4d25-8c7a-1a2a12f8c8e1"),
                    Name = "Naruto Uzumaki Figure",
                    Description = "High quality Naruto figure with detailed design.",
                    Price = 35.99m,
                    Quantity = 50,
                    ImageUrl = "https://www.super-hobby.com.ua/zdjecia/9/1/4/69258_rd.jpg",
                    Category = "Figures",
                    SKU = "FIG-NAR-001",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.8,
                    Tags = "anime,naruto,figure,collectible",
                    Discount = 10m
                },
                new Product
                {
                    Id = Guid.Parse("177ab6b9-3ef8-4d0a-b18a-2f1cdd00a64e"),
                    Name = "Attack on Titan Poster",
                    Description = "Wall poster featuring iconic Attack on Titan artwork.",
                    Price = 12.99m,
                    Quantity = 100,
                    ImageUrl = "https://m.media-amazon.com/images/I/61t9ie31jgL._UF894,1000_QL80_.jpg",
                    Category = "Posters",
                    SKU = "POST-AOT-002",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.5,
                    Tags = "anime,attackontitan,poster,wall-art",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("2e2b3e0f-cfbc-4f52-9ed4-735c7c4f7fbc"),
                    Name = "One Piece T-Shirt",
                    Description = "Comfortable cotton T-Shirt with One Piece print.",
                    Price = 24.99m,
                    Quantity = 75,
                    ImageUrl = "https://capslab.fr/5764-large_default_2x/T-shirt-en-coton-homme-relax-fit-avec-print-One-Piece-Luffy.jpg",
                    Category = "Clothing",
                    SKU = "CLO-ONE-003",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.3,
                    Tags = "anime,onepiece,tshirt,clothing",
                    Discount = 5m
                },
                new Product
                {
                    Id = Guid.Parse("92f5eebf-bbbc-4f60-8c8e-1a74fcd36720"),
                    Name = "Demon Slayer Manga Volume 1",
                    Description = "First volume of the Demon Slayer manga series.",
                    Price = 15.99m,
                    Quantity = 40,
                    ImageUrl = "https://static.yakaboo.ua/media/catalog/product/9/7/9781974700523_0.jpg",
                    Category = "Manga",
                    SKU = "MANGA-DS-004",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.9,
                    Tags = "manga,demonslayer,book",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("a4cb2e52-1d42-4ea2-9b35-7727f99f39c5"),
                    Name = "Dragon Ball Backpack",
                    Description = "Stylish Dragon Ball themed backpack for everyday use.",
                    Price = 32.99m,
                    Quantity = 60,
                    ImageUrl = "https://cdn.media.amplience.net/s/hottopic/15471811_hi",
                    Category = "Accessories",
                    SKU = "ACC-DB-005",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.6,
                    Tags = "anime,dragonball,backpack,accessory",
                    Discount = 8m
                },
                new Product
                {
                    Id = Guid.Parse("0ec56d74-9326-4f0a-9a3a-56163b1f2fa6"),
                    Name = "Sailor Moon Badge Set",
                    Description = "Set of collectible badges from Sailor Moon series.",
                    Price = 8.99m,
                    Quantity = 120,
                    ImageUrl = "https://m.media-amazon.com/images/I/413ExYsKwAL.jpg",
                    Category = "Accessories",
                    SKU = "ACC-SM-006",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.4,
                    Tags = "anime,sailormoon,badge,collectible",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("d9786db4-39fa-4f2a-9a3a-5749a91e48de"),
                    Name = "My Hero Academia Hoodie",
                    Description = "Warm hoodie with My Hero Academia print.",
                    Price = 45.99m,
                    Quantity = 30,
                    ImageUrl = "https://i.ebayimg.com/00/s/ODAwWDgwMA==/z/mdkAAOSwPwRi0TNK/$_10.JPG?set_id=880000500F",
                    Category = "Clothing",
                    SKU = "CLO-MHA-007",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.7,
                    Tags = "anime,myheroacademia,hoodie,clothing",
                    Discount = 12m
                },
                new Product
                {
                    Id = Guid.Parse("8c9822f3-0383-4d7b-bfde-6c868db76f8c"),
                    Name = "Tokyo Ghoul Wall Scroll",
                    Description = "Large Tokyo Ghoul wall scroll poster.",
                    Price = 22.99m,
                    Quantity = 25,
                    ImageUrl = "https://www.twentyonefox.com/cdn/shop/products/Tokyo-Ghoul-Kaneki-Ken-and-Uta-Poster-Wall-Decor_600x600.jpg?v=1592283304",
                    Category = "Posters",
                    SKU = "POST-TG-008",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.2,
                    Tags = "anime,tokyoghoul,poster,wall-art",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("f1d9ae12-5831-4d88-a11a-8b8d471a0d37"),
                    Name = "One Punch Man Figure",
                    Description = "Collectible figure of One Punch Man character Saitama.",
                    Price = 40.00m,
                    Quantity = 35,
                    ImageUrl = "https://images-cdn.ubuy.co.in/6615921eb44ada586d5e96ad-6-inchs-anime-one-punch-man-saitama.jpg",
                    Category = "Figures",
                    SKU = "FIG-OPM-009",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.8,
                    Tags = "anime,onepunchman,figure,collectible",
                    Discount = 15m
                },
                new Product
                {
                    Id = Guid.Parse("c2b1f5e6-2af4-4dd8-bc3c-22432fbe3442"),
                    Name = "Naruto Headband",
                    Description = "Official Naruto shinobi headband replica.",
                    Price = 14.50m,
                    Quantity = 70,
                    ImageUrl = "https://images.halloweencostumes.eu/products/53631/1-1/naruto-anti-leaf-village-headband.jpg",
                    Category = "Accessories",
                    SKU = "ACC-NAR-010",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.5,
                    Tags = "anime,naruto,headband,accessory",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("4f7d6c2f-52d9-4bd1-9270-86708609c0f9"),
                    Name = "Black Clover Manga Volume 3",
                    Description = "Third volume of the Black Clover manga series.",
                    Price = 16.50m,
                    Quantity = 50,
                    ImageUrl = "https://m.media-amazon.com/images/I/91sXgUdkzrL.jpg",
                    Category = "Manga",
                    SKU = "MANGA-BC-011",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.7,
                    Tags = "manga,blackclover,book",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("aabb47f9-0d2a-4e63-9f2c-788f5d3eebdb"),
                    Name = "One Piece Keychain",
                    Description = "Durable metal keychain with One Piece logo.",
                    Price = 7.99m,
                    Quantity = 90,
                    ImageUrl = "https://www.thetoytemple.com/cdn/shop/files/TT---WebProduct-2048x2048-1953.2_1024x.jpg?v=1730921768",
                    Category = "Accessories",
                    SKU = "ACC-ONE-012",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.3,
                    Tags = "anime,onepiece,keychain,accessory",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("58f7a34b-3c64-44ab-86f3-536ee43109f7"),
                    Name = "Attack on Titan Soundtrack CD",
                    Description = "Original soundtrack CD for Attack on Titan anime.",
                    Price = 18.00m,
                    Quantity = 40,
                    ImageUrl = "https://materia.store/cdn/shop/products/AttackonTitan1_CD_Thumbnail.png?v=1627503923",
                    Category = "Accessories",
                    SKU = "MUS-AOT-013",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.8,
                    Tags = "anime,attackontitan,music,cd",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("cfb3d98d-c197-4ae6-9280-83f2b1a98c63"),
                    Name = "Sailor Moon Necklace",
                    Description = "Elegant necklace inspired by Sailor Moon series.",
                    Price = 29.99m,
                    Quantity = 55,
                    ImageUrl = "https://animeislandca.com/cdn/shop/files/smcrystalstarlocket.png?v=1752014093",
                    Category = "Accessories",
                    SKU = "ACC-SM-014",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.6,
                    Tags = "anime,sailormoon,necklace,jewelry",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("f6a2d3b9-44ab-4a7f-b7d4-4bdeea7b3d92"),
                    Name = "Fullmetal Alchemist Pocket Watch",
                    Description = "Replica of Edward Elric's iconic silver pocket watch.",
                    Price = 39.99m,
                    Quantity = 40,
                    ImageUrl = "https://m.media-amazon.com/images/I/617oYCgcF5L._UY900_.jpg",
                    Category = "Accessories",
                    SKU = "ACC-FMA-015",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.9,
                    Tags = "anime,fullmetalalchemist,watch,collectible",
                    Discount = 5m
                },
                new Product
                {
                    Id = Guid.Parse("2c4dbf3a-2a2d-4c71-9236-9f4e2d25a9b8"),
                    Name = "Bleach Ichigo Sword Keychain",
                    Description = "Miniature keychain of Ichigo Kurosaki's Zangetsu sword.",
                    Price = 9.99m,
                    Quantity = 100,
                    ImageUrl = "https://ae01.alicdn.com/kf/S725cfbfb5ba74a508ecdc4745e72238bO.jpg",
                    Category = "Accessories",
                    SKU = "ACC-BLE-016",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.4,
                    Tags = "anime,bleach,keychain,accessory",
                    Discount = 0m
                },
                new Product
                {
                    Id = Guid.Parse("81b1c324-0c90-4a9e-8a41-4d2f88a6a583"),
                    Name = "Jujutsu Kaisen Gojo Satoru Figure",
                    Description = "High-quality figure of Gojo Satoru from Jujutsu Kaisen.",
                    Price = 42.50m,
                    Quantity = 35,
                    ImageUrl = "https://m.media-amazon.com/images/I/51YOZq-Xq1L._UF894,1000_QL80_.jpg",
                    Category = "Figures",
                    SKU = "FIG-JJK-017",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.8,
                    Tags = "anime,jujutsukaisen,figure,collectible",
                    Discount = 10m
                },
                new Product
                {
                    Id = Guid.Parse("afce6f64-08c8-4d93-b5a8-1b6b8f8f7740"),
                    Name = "Spy x Family Anya Forger Plush",
                    Description = "Soft plush toy of Anya Forger from Spy x Family.",
                    Price = 25.00m,
                    Quantity = 60,
                    ImageUrl = "https://m.media-amazon.com/images/I/71x6PcXP4sL.jpg",
                    Category = "Accessories",
                    SKU = "TOY-SXF-018",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Rating = 4.7,
                    Tags = "anime,spyxfamily,anya,plush,toy",
                    Discount = 0m
                }
            };

            _db.Products.AddRange(products);
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var product in products)
            {
                await _publishEndpoint.Publish(new ProductCreatedEvent(
                    product.Id,
                    product.Name,
                    product.Price,
                    product.SKU,
                    product.Category,
                    product.Quantity,
                    product.IsAvailable,
                    product.Discount,
                    product.CreatedAt
                ), cancellationToken);
            }
        }
    }
}
