namespace InMoment.Application.Abstractions.Security;

public interface ICurrentUser
{
    Guid UserId { get; }
}