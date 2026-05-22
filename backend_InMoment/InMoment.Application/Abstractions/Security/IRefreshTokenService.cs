namespace InMoment.Application.Abstractions.Security;

public interface IRefreshTokenService
{
    string CreateToken();
    string HashToken(string rawToken);
    DateTime GetExpiryUtc();
}