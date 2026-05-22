namespace InMoment.Application.Abstractions.Security;

public interface ITokenService
{
    string CreateAccessToken(Guid userId, string userName);
}