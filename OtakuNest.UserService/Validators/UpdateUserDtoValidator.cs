using FluentValidation;
using OtakuNest.UserService.DTOs;

namespace OtakuNest.UserService.Validators
{
    public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
    {
        public UpdateUserDtoValidator()
        {
            RuleFor(u => u.UserName)
                .MinimumLength(3).WithMessage("Username must be at least 3 characters long.")
                .MaximumLength(50).WithMessage("Username must not exceed 50 characters.")
                .When(u => !string.IsNullOrEmpty(u.UserName));

            RuleFor(u => u.Email)
                .EmailAddress().WithMessage("Invalid email format.")
                .When(u => !string.IsNullOrEmpty(u.Email));

            RuleFor(u => u.Password)
                .MinimumLength(6).WithMessage("Password must be at least 6 characters long.")
                .When(u => !string.IsNullOrEmpty(u.Password));

            RuleFor(u => u.PhoneNumber)
                .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Invalid phone number format.")
                .When(u => !string.IsNullOrEmpty(u.PhoneNumber));
        }
    }
}
