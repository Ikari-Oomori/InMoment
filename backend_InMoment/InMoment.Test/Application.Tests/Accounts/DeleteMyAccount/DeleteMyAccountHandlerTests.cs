using FluentAssertions;
using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Accounts.DeleteMyAccount;
using InMoment.Domain.Common;
using Moq;

namespace InMoment.Application.Tests.Accounts.DeleteMyAccount;

public sealed class DeleteMyAccountHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IAccountDataManager> _accounts = new();

    private DeleteMyAccountHandler Create()
        => new(_current.Object, _accounts.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserUnauthorized()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new DeleteMyAccountCommand("DELETE"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");

        _accounts.Verify(
            x => x.DeactivateAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenConfirmationInvalid()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        var handler = Create();

        var act = () => handler.Handle(
            new DeleteMyAccountCommand(" delete "),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Для удаления аккаунта подтвердите действие строкой DELETE.");

        _accounts.Verify(
            x => x.DeactivateAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldTrimConfirmation_AndDeactivateAccount()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        var handler = Create();

        await handler.Handle(
            new DeleteMyAccountCommand("  DELETE  "),
            CancellationToken.None);

        _accounts.Verify(
            x => x.DeactivateAccountAsync(currentUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}