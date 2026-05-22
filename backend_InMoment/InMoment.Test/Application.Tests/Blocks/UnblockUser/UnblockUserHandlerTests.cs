using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Blocks.UnblockUser;
using InMoment.Domain.Common;
using InMoment.Domain.Privacy;
using Moq;

namespace InMoment.Application.Tests.Blocks.UnblockUser;

public sealed class UnblockUserHandlerTests
{
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private UnblockUserHandler Create()
        => new(_blocks.Object, _current.Object, _uow.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.Setup(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new UnblockUserCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenBlockMissing()
    {
        var currentUserId = Guid.NewGuid();
        var blockedUserId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _blocks.Setup(x => x.GetAsync(currentUserId, blockedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlockedUser?)null);

        var handler = Create();

        var act = () => handler.Handle(new UnblockUserCommand(blockedUserId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Блокировка не найдена.");
    }

    [Fact]
    public async Task Handle_ShouldRemoveBlock_WhenExists()
    {
        var currentUserId = Guid.NewGuid();
        var blockedUserId = Guid.NewGuid();

        var block = BlockedUser.Create(currentUserId, blockedUserId);

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _blocks.Setup(x => x.GetAsync(currentUserId, blockedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(block);

        var handler = Create();

        await handler.Handle(new UnblockUserCommand(blockedUserId), CancellationToken.None);

        _blocks.Verify(x => x.Remove(block), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}