using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Features.Auth.CheckUserNameAvailability;
using InMoment.Domain.Common;
using Moq;

namespace InMoment.Application.Tests.Auth.CheckUserNameAvailability;

public sealed class CheckUserNameAvailabilityHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();

    private CheckUserNameAvailabilityHandler Create()
        => new(_users.Object);

    [Fact]
    public async Task Handle_ShouldReturnAvailable_WhenUserNameDoesNotExist()
    {
        _users.Setup(x => x.UserNameExistsAsync("new_user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = Create();

        var result = await handler.Handle(
            new CheckUserNameAvailabilityQuery("new_user"),
            CancellationToken.None);

        result.UserName.Should().Be("new_user");
        result.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnNotAvailable_WhenUserNameExists()
    {
        _users.Setup(x => x.UserNameExistsAsync("taken_user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Create();

        var result = await handler.Handle(
            new CheckUserNameAvailabilityQuery("taken_user"),
            CancellationToken.None);

        result.UserName.Should().Be("taken_user");
        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldTrimUserName_BeforeChecking()
    {
        _users.Setup(x => x.UserNameExistsAsync("trimmed_user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = Create();

        var result = await handler.Handle(
            new CheckUserNameAvailabilityQuery("  trimmed_user  "),
            CancellationToken.None);

        result.UserName.Should().Be("trimmed_user");
        result.IsAvailable.Should().BeTrue();

        _users.Verify(x => x.UserNameExistsAsync("trimmed_user", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenUserNameEmpty()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new CheckUserNameAvailabilityQuery("   "),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Username is required.");

        _users.Verify(x => x.UserNameExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenUserNameTooShort()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new CheckUserNameAvailabilityQuery("a"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Username must be at least 2 characters.");

        _users.Verify(x => x.UserNameExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenUserNameTooLong()
    {
        var tooLong = new string('a', 51);
        var handler = Create();

        var act = () => handler.Handle(
            new CheckUserNameAvailabilityQuery(tooLong),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Username must be 50 characters or less.");

        _users.Verify(x => x.UserNameExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenUserNameHasInvalidCharacters()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new CheckUserNameAvailabilityQuery("bad-name!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Username may contain only letters, digits, dot and underscore.");

        _users.Verify(x => x.UserNameExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}