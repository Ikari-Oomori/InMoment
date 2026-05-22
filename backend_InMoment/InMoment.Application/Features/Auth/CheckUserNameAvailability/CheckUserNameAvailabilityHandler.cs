using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Auth.CheckUserNameAvailability;

public sealed class CheckUserNameAvailabilityHandler
    : IRequestHandler<CheckUserNameAvailabilityQuery, UserNameAvailabilityDto>
{
    private readonly IUserRepository _users;

    public CheckUserNameAvailabilityHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<UserNameAvailabilityDto> Handle(CheckUserNameAvailabilityQuery query, CancellationToken ct)
    {
        var userName = NormalizeAndValidate(query.UserName);

        var exists = await _users.UserNameExistsAsync(userName, ct);

        return new UserNameAvailabilityDto(
            userName,
            !exists
        );
    }

    private static string NormalizeAndValidate(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ValidationException("Username is required.");

        var normalized = userName.Trim();

        if (normalized.Length < 2)
            throw new ValidationException("Username must be at least 2 characters.");

        if (normalized.Length > 50)
            throw new ValidationException("Username must be 50 characters or less.");

        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[A-Za-z0-9._]+$"))
            throw new ValidationException("Username may contain only letters, digits, dot and underscore.");

        return normalized;
    }
}