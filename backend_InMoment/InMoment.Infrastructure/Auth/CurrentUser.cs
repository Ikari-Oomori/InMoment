using System.Security.Claims;
using InMoment.Application.Abstractions.Security;
using Microsoft.AspNetCore.Http;

namespace InMoment.Infrastructure.Auth;

public sealed class CurrentUser : ICurrentUser
{
    public Guid UserId { get; }

    public CurrentUser(IHttpContextAccessor accessor)
    {
        var id = accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        UserId = Guid.TryParse(id, out var guid) ? guid : Guid.Empty;
    }
}