using FluentValidation;
using OtakuNest.OrderService.DTOs;
using OtakuNest.OrderService.Models;

namespace OtakuNest.OrderService.Validators
{
    public class OrderStatusUpdateDtoValidator : AbstractValidator<OrderStatusUpdateDto>
    {
        public OrderStatusUpdateDtoValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status is required.")
                .Must(BeAValidStatus)
                .WithMessage(x => $"Invalid order status. Possible values: {string.Join(", ", Enum.GetNames(typeof(OrderStatus)))}");
        }

        private static bool BeAValidStatus(string status)
        {
            return Enum.TryParse<OrderStatus>(status, ignoreCase: true, out _);
        }
    }
}
