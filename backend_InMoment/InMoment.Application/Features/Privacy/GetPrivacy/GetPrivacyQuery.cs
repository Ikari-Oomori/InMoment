using InMoment.Application.Features.Privacy.Common;
using MediatR;

namespace InMoment.Application.Features.Privacy.GetPrivacy;

public sealed record GetPrivacyQuery : IRequest<PrivacySettingsDto>;