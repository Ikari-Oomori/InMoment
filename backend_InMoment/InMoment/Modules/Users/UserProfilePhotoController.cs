using InMoment.Application.Features.Users.SetProfilePhoto;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Users;

[ApiController]
[Authorize]
[Route("api/users/me")]
public sealed class UserProfilePhotoController : ControllerBase
{
    private readonly IMediator _mediator;
    public UserProfilePhotoController(IMediator mediator) => _mediator = mediator;

    public sealed record SetProfilePhotoRequest(string? Url);

    [HttpPost("profile-photo")]
    public async Task<IActionResult> Set([FromBody] SetProfilePhotoRequest req, CancellationToken ct)
    {
        await _mediator.Send(new SetProfilePhotoCommand(req.Url), ct);
        return NoContent();
    }
}