namespace OtakuNest.CommentService.DTOs
{
    public class ReplyToCommentDto
    {
        public Guid ProductId { get; set; }
        public Guid ParentCommentId { get; set; }
        public string Content { get; set; } = null!;
    }
}
