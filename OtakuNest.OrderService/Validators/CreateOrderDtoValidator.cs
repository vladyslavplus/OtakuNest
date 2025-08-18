using FluentValidation;
using OtakuNest.OrderService.DTOs;

namespace OtakuNest.OrderService.Validators
{
    public class CreateOrderDtoValidator : AbstractValidator<CreateOrderDto>
    {
        public CreateOrderDtoValidator()
        {
            RuleFor(x => x.ShippingAddress)
                .NotEmpty().WithMessage("Shipping address is required.")
                .MinimumLength(10).MaximumLength(500).WithMessage("Shipping address cannot exceed 500 characters and must be at least 10 characters.");

            RuleFor(x => x.Items)
                .NotEmpty().WithMessage("Order must have at least one item.")
                .ForEach(item => item.SetValidator(new CreateOrderItemDtoValidator()));
        }
    }
}
