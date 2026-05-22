using MediatR;

namespace InMoment.Application.Features.Media.Reactions.RemoveReaction;

public sealed record RemoveReactionCommand(Guid PhotoId) : IRequest;