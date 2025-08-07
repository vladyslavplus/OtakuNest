namespace OtakuNest.CommentService.Models
{
    public class CommentLike
    {
        public Guid Id { get; set; }
        public Guid CommentId { get; set; }
        public Guid UserId { get; set; }
        public DateTime LikedAt { get; set; } = DateTime.UtcNow;
        public Comment? Comment { get; set; }
    }
}
