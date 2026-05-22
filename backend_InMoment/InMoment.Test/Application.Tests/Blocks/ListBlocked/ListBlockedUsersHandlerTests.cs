using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Blocks.ListBlocked;
using InMoment.Domain.Common;
using InMoment.Domain.Privacy;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Blocks.ListBlocked;

public sealed class ListBlockedUsersHandlerTests
{
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICurrentUser> _current = new();

    private ListBlockedUsersHandler Create()
        => new(_blocks.Object, _users.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.Setup(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new ListBlockedUsersQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNoBlocks()
    {
        var currentUserId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _blocks.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BlockedUser>());

        var handler = Create();

        var result = await handler.Handle(new ListBlockedUsersQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSkipMissingUsers_AndSortByName()
    {
        var currentUserId = Guid.NewGuid();

        var blocked1 = BlockedUser.Create(currentUserId, Guid.NewGuid());
        var blocked2 = BlockedUser.Create(currentUserId, Guid.NewGuid());
        var blocked3 = BlockedUser.Create(currentUserId, Guid.NewGuid());

        var userB = User.Create("b@test.com", "hash", "b", "Boris", "Zed");
        var userA = User.Create("a@test.com", "hash", "a", "Anna", "Alpha");

        SetUserId(userB, blocked2.BlockedUserId);
        SetUserId(userA, blocked3.BlockedUserId);

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _blocks.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { blocked1, blocked2, blocked3 });

        _users.Setup(x => x.GetByIdAsync(blocked1.BlockedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _users.Setup(x => x.GetByIdAsync(blocked2.BlockedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userB);
        _users.Setup(x => x.GetByIdAsync(blocked3.BlockedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userA);

        var handler = Create();

        var result = await handler.Handle(new ListBlockedUsersQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].UserId.Should().Be(userA.Id);
        result[1].UserId.Should().Be(userB.Id);
    }

    private static void SetUserId(User user, Guid id)
    {
        typeof(User)
            .GetProperty(nameof(User.Id))!
            .SetValue(user, id);
    }
}