using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.Reactions.SetReaction;

public sealed record SetReactionCommand(Guid PhotoId, ReactionType Type) : IRequest;