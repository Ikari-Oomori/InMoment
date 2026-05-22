using MediatR;

namespace InMoment.Application.Features.Accounts.DeleteMyAccount;

public sealed record DeleteMyAccountCommand(string Confirmation) : IRequest;