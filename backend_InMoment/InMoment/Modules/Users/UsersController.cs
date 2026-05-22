using InMoment.Application.Features.Users.CompleteOnboarding;
using InMoment.Application.Features.Users.GetMe;
using InMoment.Application.Features.Users.SkipContactsOnboarding;
using InMoment.Application.Features.Users.UpdateMe;
using InMoment.Application.Features.Users.GetPublicProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Users;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    public UsersController(IMediator mediator) => _mediator = mediator;

    [HttpGet("me")]
    public async Task<ActionResult<MeDto>> GetMe(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMeQuery(), ct));

    [HttpGet("{userId:guid}/public-profile")]
    public async Task<ActionResult<PublicUserProfileDto>> GetPublicProfile(Guid userId, CancellationToken ct)
    => Ok(await _mediator.Send(new GetPublicProfileQuery(userId), ct));

    public sealed record UpdateMeRequest(
        string? UserName,
        string? FirstName,
        string? LastName,
        string? PhoneNumber);

    [HttpPatch("me")]
    public async Task<ActionResult<UpdatedMeDto>> UpdateMe([FromBody] UpdateMeRequest req, CancellationToken ct)
        => Ok(await _mediator.Send(
            new UpdateMeCommand(req.UserName, req.FirstName, req.LastName, req.PhoneNumber), ct));

    [HttpPost("me/onboarding/skip-contacts")]
    public async Task<IActionResult> SkipContactsOnboarding(CancellationToken ct)
    {
        await _mediator.Send(new SkipContactsOnboardingCommand(), ct);
        return NoContent();
    }

    [HttpPost("me/onboarding/complete")]
    public async Task<IActionResult> CompleteOnboarding(CancellationToken ct)
    {
        await _mediator.Send(new CompleteOnboardingCommand(), ct);
        return NoContent();
    }
}