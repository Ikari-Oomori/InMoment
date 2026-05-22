using FluentValidation;

namespace InMoment.Application.Features.Auth.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(72);

        RuleFor(x => x.DeviceName)
            .MaximumLength(200);

        RuleFor(x => x.Platform)
            .MaximumLength(100);

        RuleFor(x => x.IpAddress)
            .MaximumLength(100);

        RuleFor(x => x.UserAgent)
            .MaximumLength(1000);
    }
}