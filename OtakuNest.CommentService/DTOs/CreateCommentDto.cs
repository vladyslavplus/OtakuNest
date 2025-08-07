namespace OtakuNest.CommentService.DTOs
{
    public class CreateCommentDto
    {
        public Guid ProductId { get; set; }
        public string Content { get; set; } = null!;
    }
}
