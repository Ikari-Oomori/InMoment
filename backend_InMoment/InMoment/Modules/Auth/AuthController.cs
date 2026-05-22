using InMoment.Application.Features.Auth.ChangePassword;
using InMoment.Application.Features.Auth.CheckUserNameAvailability;
using InMoment.Application.Features.Auth.ForgotPassword;
using InMoment.Application.Features.Auth.Login;
using InMoment.Application.Features.Auth.Logout;
using InMoment.Application.Features.Auth.LogoutAll;
using InMoment.Application.Features.Auth.Refresh;
using InMoment.Application.Features.Auth.Register;
using InMoment.Application.Features.Auth.ResetPassword;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [AllowAnonymous]
    [HttpGet("username-availability")]
    public async Task<ActionResult<UserNameAvailabilityDto>> CheckUserNameAvailability(
        [FromQuery] string userName,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CheckUserNameAvailabilityQuery(userName),
            ct);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<RegisterResult>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new RegisterCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            request.UserName,
            request.PhoneNumber), ct);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(
            request.Email,
            request.Password,
            request.DeviceName,
            request.Platform,
            ResolveClientIpAddress(),
            Request.Headers.UserAgent.ToString()), ct);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshTokenResult>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new RefreshTokenCommand(request.RefreshToken), ct);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken ct)
    {
        await _mediator.Send(new LogoutCommand(request.RefreshToken), ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await _mediator.Send(new LogoutAllCommand(), ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        await _mediator.Send(new ChangePasswordCommand(
            request.CurrentPassword,
            request.NewPassword,
            request.CurrentRefreshToken), ct);

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct)
    {
        await _mediator.Send(new ForgotPasswordCommand(
            request.Email,
            ResolveClientIpAddress(),
            Request.Headers.UserAgent.ToString()), ct);

        return Ok(new
        {
            message = "Если пользователь с таким email существует, инструкции по восстановлению уже отправлены."
        });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct)
    {
        await _mediator.Send(new ResetPasswordCommand(
            request.Token,
            request.NewPassword), ct);

        return NoContent();
    }

    private string? ResolveClientIpAddress()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var first = forwardedFor
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(first))
                return first;
        }

        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
            return realIp.Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}