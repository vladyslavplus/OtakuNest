using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OtakuNest.UserService.Models;

namespace OtakuNest.UserService.Data
{
    public class UserDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            var adminRoleId = Guid.NewGuid();
            var userRoleId = Guid.NewGuid();

            builder.Entity<IdentityRole<Guid>>().HasData(
                new IdentityRole<Guid>
                {
                    Id = adminRoleId,
                    Name = "Admin",
                    NormalizedName = "ADMIN"
                },
                new IdentityRole<Guid>
                {
                    Id = userRoleId,
                    Name = "User",
                    NormalizedName = "USER"
                }
            );
        }
    }
}
