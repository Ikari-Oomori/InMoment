using FluentValidation;

namespace InMoment.Application.Features.Auth.ForgotPassword;

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(200);

        RuleFor(x => x.RequestedByIp)
            .MaximumLength(100);

        RuleFor(x => x.RequestedByUserAgent)
            .MaximumLength(1000);
    }
}