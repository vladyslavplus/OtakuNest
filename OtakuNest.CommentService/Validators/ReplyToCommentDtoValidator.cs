using FluentValidation;
using OtakuNest.CommentService.DTOs;

namespace OtakuNest.CommentService.Validators
{
    public class ReplyToCommentDtoValidator : AbstractValidator<ReplyToCommentDto>
    {
        public ReplyToCommentDtoValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty().WithMessage("ProductId is required.");

            RuleFor(x => x.ParentCommentId)
                .NotEmpty().WithMessage("ParentCommentId is required.");

            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("Content cannot be empty.")
                .MinimumLength(3).WithMessage("Content must be at least 3 characters long.")
                .MaximumLength(1000).WithMessage("Content cannot exceed 1000 characters.");
        }
    }
}
