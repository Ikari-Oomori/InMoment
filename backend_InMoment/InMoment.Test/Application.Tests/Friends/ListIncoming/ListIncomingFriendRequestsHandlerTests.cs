using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Friends.ListIncoming;
using InMoment.Domain.Common;
using InMoment.Domain.Friends;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Friends.ListIncoming;

public sealed class ListIncomingFriendRequestsHandlerTests
{
    private readonly Mock<IFriendRequestRepository> _requests = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICurrentUser> _current = new();

    private ListIncomingFriendRequestsHandler Create()
        => new(_requests.Object, _users.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new ListIncomingFriendRequestsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");

        _requests.Verify(x => x.GetIncomingPendingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNoIncomingRequests()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _requests.Setup(x => x.GetIncomingPendingAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FriendRequest>());

        var handler = Create();

        var result = await handler.Handle(new ListIncomingFriendRequestsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSkipMissingUsers_AndMapDtos()
    {
        var currentUserId = Guid.NewGuid();
        var senderAId = Guid.NewGuid();
        var senderBId = Guid.NewGuid();
        var missingSenderId = Guid.NewGuid();

        var requestA = FriendRequest.Create(senderAId, currentUserId);
        var requestB = FriendRequest.Create(senderBId, currentUserId);
        var requestMissing = FriendRequest.Create(missingSenderId, currentUserId);

        SetCreatedAtUtc(requestA, new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAtUtc(requestB, new DateTime(2026, 4, 11, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAtUtc(requestMissing, new DateTime(2026, 4, 12, 10, 0, 0, DateTimeKind.Utc));

        var userA = User.Create("a@test.com", "hash", "alpha_user", "Alpha", "One");
        SetEntityId(userA, senderAId);

        var userB = User.Create("b@test.com", "hash", "beta_user", "Beta", "Two");
        userB.SetProfilePhoto("https://cdn.example.com/profiles/beta.jpg");
        SetEntityId(userB, senderBId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _requests.Setup(x => x.GetIncomingPendingAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { requestA, requestMissing, requestB });

        _users.Setup(x => x.GetByIdAsync(senderAId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userA);

        _users.Setup(x => x.GetByIdAsync(senderBId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userB);

        _users.Setup(x => x.GetByIdAsync(missingSenderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        var result = await handler.Handle(new ListIncomingFriendRequestsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);

        result[0].RequestId.Should().Be(requestA.Id);
        result[0].UserId.Should().Be(senderAId);
        result[0].UserName.Should().Be("alpha_user");
        result[0].FirstName.Should().Be("Alpha");
        result[0].LastName.Should().Be("One");
        result[0].ProfilePhotoUrl.Should().BeNull();
        result[0].Status.Should().Be(FriendRequestStatus.Pending);
        result[0].CreatedAtUtc.Should().Be(requestA.CreatedAtUtc);

        result[1].RequestId.Should().Be(requestB.Id);
        result[1].UserId.Should().Be(senderBId);
        result[1].UserName.Should().Be("beta_user");
        result[1].FirstName.Should().Be("Beta");
        result[1].LastName.Should().Be("Two");
        result[1].ProfilePhotoUrl.Should().Be("https://cdn.example.com/profiles/beta.jpg");
        result[1].Status.Should().Be(FriendRequestStatus.Pending);
        result[1].CreatedAtUtc.Should().Be(requestB.CreatedAtUtc);
    }

    private static void SetEntityId(User user, Guid id)
    {
        typeof(InMoment.Domain.Common.Entity<Guid>)
            .GetProperty("Id")!
            .SetValue(user, id);
    }

    private static void SetCreatedAtUtc(FriendRequest request, DateTime value)
    {
        typeof(FriendRequest)
            .GetProperty(nameof(FriendRequest.CreatedAtUtc))!
            .SetValue(request, value);
    }
}