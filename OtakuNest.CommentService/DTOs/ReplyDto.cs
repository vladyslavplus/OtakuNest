namespace OtakuNest.CommentService.DTOs
{
    public class ReplyDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int LikesCount { get; set; }
        public List<ReplyDto> Replies { get; set; } = new();
    }
}
