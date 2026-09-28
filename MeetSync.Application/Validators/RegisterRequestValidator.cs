using FluentValidation;
using MeetSync.Application.DTOs.Auth;

namespace MeetSync.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .Length(2, 50).WithMessage("Display name must be between 2 and 50 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");
    }
}
