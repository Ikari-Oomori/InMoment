using MediatR;

namespace InMoment.Application.Features.Accounts.PermanentlyDeleteMyAccount;

public sealed record PermanentlyDeleteMyAccountCommand(string Confirmation) : IRequest;