using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Blocks.BlockUser;
using InMoment.Domain.Common;
using InMoment.Domain.Privacy;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Blocks.BlockUser;

public sealed class BlockUserHandlerTests
{
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private BlockUserHandler Create()
        => new(_blocks.Object, _users.Object, _current.Object, _uow.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.Setup(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new BlockUserCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenBlockedUserIdEmpty()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var handler = Create();

        var act = () => handler.Handle(new BlockUserCommand(Guid.Empty), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("BlockedUserId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenTargetUserMissing()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        var act = () => handler.Handle(new BlockUserCommand(targetUserId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Пользователь не найден.");
    }

    [Fact]
    public async Task Handle_ShouldReturnWithoutSaving_WhenAlreadyBlocked()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var targetUser = User.Create("user@test.com", "hash", "user", "Test", "User");
        SetUserId(targetUser, targetUserId);

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);
        _blocks.Setup(x => x.ExistsAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Create();

        await handler.Handle(new BlockUserCommand(targetUserId), CancellationToken.None);

        _blocks.Verify(x => x.AddAsync(It.IsAny<BlockedUser>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCreateBlock_WhenValid()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var targetUser = User.Create("user@test.com", "hash", "user", "Test", "User");
        SetUserId(targetUser, targetUserId);

        BlockedUser? added = null;

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);
        _blocks.Setup(x => x.ExistsAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.AddAsync(It.IsAny<BlockedUser>(), It.IsAny<CancellationToken>()))
            .Callback<BlockedUser, CancellationToken>((b, _) => added = b)
            .Returns(Task.CompletedTask);

        var handler = Create();

        await handler.Handle(new BlockUserCommand(targetUserId), CancellationToken.None);

        added.Should().NotBeNull();
        added!.UserId.Should().Be(currentUserId);
        added.BlockedUserId.Should().Be(targetUserId);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static void SetUserId(User user, Guid id)
    {
        typeof(User)
            .GetProperty(nameof(User.Id))!
            .SetValue(user, id);
    }
}