using FluentValidation;
using InMoment.Domain.Privacy;

namespace InMoment.Application.Features.Privacy.UpdatePrivacy;

public sealed class UpdatePrivacyValidator : AbstractValidator<UpdatePrivacyCommand>
{
    public UpdatePrivacyValidator()
    {
        RuleFor(x => x.AllowFriendRequestsFrom)
            .IsInEnum();

        RuleFor(x => x.AllowGroupInvitesFrom)
            .IsInEnum();
    }
}