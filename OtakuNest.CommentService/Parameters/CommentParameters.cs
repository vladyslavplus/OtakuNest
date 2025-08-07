using OtakuNest.Common.Parameters;

namespace OtakuNest.CommentService.Parameters
{
    public class CommentParameters : QueryStringParameters
    {
        public Guid ProductId { get; set; }
        public Guid? ParentCommentId { get; set; } = null;
        public string? Content { get; set; }
    }
}
