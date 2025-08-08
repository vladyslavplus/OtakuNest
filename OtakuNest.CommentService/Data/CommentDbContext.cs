using Microsoft.EntityFrameworkCore;
using OtakuNest.CommentService.Models;

namespace OtakuNest.CommentService.Data
{
    public class CommentDbContext : DbContext
    {
        public CommentDbContext(DbContextOptions<CommentDbContext> options) : base(options) { }

        public DbSet<Comment> Comments { get; set; }
        public DbSet<CommentLike> CommentLikes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Comment>()
                .HasIndex(c => c.ParentCommentId);

            modelBuilder.Entity<Comment>()
                .HasIndex(c => c.ProductId);

            modelBuilder.Entity<Comment>()
                .HasIndex(c => c.UserId);


            modelBuilder.Entity<Comment>()
                .HasOne(c => c.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CommentLike>()
                .HasIndex(cl => new { cl.CommentId, cl.UserId })
                .IsUnique();
        }
    }
}
