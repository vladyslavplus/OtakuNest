using FluentValidation;
using OtakuNest.CartService.DTOs;

namespace OtakuNest.CartService.Validators
{
    public class UpdateCartItemQuantityDtoValidator : AbstractValidator<UpdateCartItemQuantityDto>
    {
        public UpdateCartItemQuantityDtoValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty().WithMessage("ProductId is required.");

            RuleFor(x => x.Delta)
                .Must(v => v == 1 || v == -1)
                .WithMessage("Delta must be +1 or -1.");
        }
    }
}
