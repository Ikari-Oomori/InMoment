using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Accounts.Common;
using InMoment.Application.Features.Accounts.GetMyDataSummary;
using InMoment.Domain.Common;

namespace InMoment.Application.Tests.Accounts.GetMyDataSummary;

public sealed class GetMyDataSummaryHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IAccountDataManager> _accounts = new();

    private GetMyDataSummaryHandler Create()
        => new(_current.Object, _accounts.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserUnauthorized()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new GetMyDataSummaryQuery(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");

        _accounts.Verify(
            x => x.GetSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnSummaryFromAccountManager()
    {
        var currentUserId = Guid.NewGuid();

        var summary = new AccountDataSummaryDto(
            UserId: currentUserId,
            IsActive: true,
            GroupsCount: 5,
            OwnedGroupsCount: 2,
            PhotosCount: 18,
            CommentsCount: 33,
            ReactionsCount: 41,
            FriendshipsCount: 7,
            ActiveSessionsCount: 3);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _accounts.Setup(x => x.GetSummaryAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var handler = Create();

        var result = await handler.Handle(
            new GetMyDataSummaryQuery(),
            CancellationToken.None);

        result.Should().Be(summary);
    }
}