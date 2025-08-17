using FluentValidation;
using OtakuNest.CartService.DTOs;

namespace OtakuNest.CartService.Validators
{
    public class AddCartItemDtoValidator : AbstractValidator<AddCartItemDto>
    {
        public AddCartItemDtoValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty().WithMessage("ProductId is required.");

            RuleFor(x => x.Quantity)
                .GreaterThanOrEqualTo(1)
                .WithMessage("Quantity must be at least 1.");
        }
    }
}
