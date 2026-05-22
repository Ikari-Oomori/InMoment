using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Users.SkipContactsOnboarding;

public sealed class SkipContactsOnboardingHandler : IRequestHandler<SkipContactsOnboardingCommand>
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public SkipContactsOnboardingHandler(
        IUserRepository users,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _users = users;
        _uow = uow;
        _current = current;
    }

    public async Task Handle(SkipContactsOnboardingCommand request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        var user = await _users.GetByIdAsync(_current.UserId, ct)
                   ?? throw new NotFoundException("User not found.");

        user.MarkContactsStepCompleted(skipped: true);

        await _uow.SaveChangesAsync(ct);
    }
}