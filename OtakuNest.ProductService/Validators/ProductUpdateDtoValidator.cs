using FluentValidation;
using OtakuNest.ProductService.DTOs;

namespace OtakuNest.ProductService.Validators
{
    public class ProductUpdateDtoValidator : AbstractValidator<ProductUpdateDto>
    {
        public ProductUpdateDtoValidator()
        {
            RuleFor(p => p.Name)
                .MinimumLength(3).WithMessage("Name must be at least 3 characters long.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.")
                .When(p => !string.IsNullOrEmpty(p.Name));

            RuleFor(p => p.Description)
                .MinimumLength(10).WithMessage("Description must be at least 10 characters long.")
                .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
                .When(p => !string.IsNullOrEmpty(p.Description));

            RuleFor(p => p.Price)
                .GreaterThan(0).WithMessage("Price must be greater than 0.")
                .When(p => p.Price.HasValue);

            RuleFor(p => p.Quantity)
                .GreaterThanOrEqualTo(0).WithMessage("Quantity must be at least 0.")
                .When(p => p.Quantity.HasValue);

            RuleFor(p => p.ImageUrl)
                .Must(uri => Uri.TryCreate(uri!, UriKind.Absolute, out _))
                .WithMessage("Invalid image URL format.")
                .When(p => !string.IsNullOrEmpty(p.ImageUrl));

            RuleFor(p => p.Category)
                .MinimumLength(3).WithMessage("Category must be at least 3 characters long.")
                .MaximumLength(50).WithMessage("Category must not exceed 50 characters.")
                .When(p => !string.IsNullOrEmpty(p.Category));

            RuleFor(p => p.SKU)
                .MinimumLength(3).WithMessage("SKU must be at least 3 characters long.")
                .MaximumLength(50).WithMessage("SKU must not exceed 50 characters.")
                .When(p => !string.IsNullOrEmpty(p.SKU));

            RuleFor(p => p.Rating)
                .InclusiveBetween(0, 5).WithMessage("Rating must be between 0 and 5.")
                .When(p => p.Rating.HasValue);

            RuleFor(p => p.Tags)
                .MinimumLength(3).WithMessage("Tags must be at least 3 characters long.")
                .MaximumLength(200).WithMessage("Tags must not exceed 200 characters.")
                .When(p => !string.IsNullOrEmpty(p.Tags));

            RuleFor(p => p.Discount)
                .InclusiveBetween(0, 100).WithMessage("Discount must be between 0 and 100 percent.")
                .When(p => p.Discount.HasValue);
        }
    }
}