using DomainValidationException = InMoment.Domain.Common.ValidationException;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Users.UpdateMe;
using InMoment.Domain.Common;
using InMoment.Domain.Users;

namespace InMoment.Application.Tests.Users.UpdateMe;

public sealed class UpdateMeHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private UpdateMeHandler Create()
        => new(
            _users.Object,
            _uow.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsEmpty()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new UpdateMeCommand("anna_pet", "Anna", "Petrova"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Unauthorized.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUserNotFound()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new UpdateMeCommand("anna_pet", "Anna", "Petrova"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("User not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldUpdateOnlyNames_WhenUserNameNotChanged()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "update@test.com",
            "hash",
            "anna_pet",
            "Old",
            "Name");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        var result = await handler.Handle(
            new UpdateMeCommand("anna_pet", "Anna", "Petrova"),
            CancellationToken.None);

        user.UserName.Should().Be("anna_pet");
        user.FirstName.Should().Be("Anna");
        user.LastName.Should().Be("Petrova");
        user.PhoneNumber.Should().BeNull();

        result.Id.Should().Be(user.Id);
        result.UserName.Should().Be("anna_pet");
        result.FirstName.Should().Be("Anna");
        result.LastName.Should().Be("Petrova");
        result.PhoneNumber.Should().BeNull();

        _users.Verify(x => x.UserNameExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.PhoneNumberExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldUpdateUserName_WhenChangedAndAvailable()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "update2@test.com",
            "hash",
            "old_name",
            "Old",
            "Name");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _users.Setup(x => x.UserNameExistsAsync("new_name", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = Create();

        var result = await handler.Handle(
            new UpdateMeCommand("new_name", "Elena", "Sidorova"),
            CancellationToken.None);

        user.UserName.Should().Be("new_name");
        user.FirstName.Should().Be("Elena");
        user.LastName.Should().Be("Sidorova");

        result.UserName.Should().Be("new_name");
        result.FirstName.Should().Be("Elena");
        result.LastName.Should().Be("Sidorova");
        result.PhoneNumber.Should().BeNull();

        _users.Verify(x => x.UserNameExistsAsync("new_name", It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(x => x.PhoneNumberExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenUserNameAlreadyTaken()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "update3@test.com",
            "hash",
            "old_name",
            "Old",
            "Name");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _users.Setup(x => x.UserNameExistsAsync("taken_name", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Create();

        var act = () => handler.Handle(
            new UpdateMeCommand("taken_name", "Elena", "Sidorova"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Nickname is already used.");

        user.UserName.Should().Be("old_name");
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldUpdateOnlyProvidedNames_WhenUserNameIsNull()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "update4@test.com",
            "hash",
            "same_name",
            "Old",
            "Name");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        var result = await handler.Handle(
            new UpdateMeCommand(null, "NewFirst", null),
            CancellationToken.None);

        user.UserName.Should().Be("same_name");
        user.FirstName.Should().Be("NewFirst");
        user.LastName.Should().Be("Name");
        user.PhoneNumber.Should().BeNull();

        result.UserName.Should().Be("same_name");
        result.FirstName.Should().Be("NewFirst");
        result.LastName.Should().Be("Name");
        result.PhoneNumber.Should().BeNull();

        _users.Verify(x => x.UserNameExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.PhoneNumberExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSetPhoneNumber_WhenProvidedAndAvailable()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "update5@test.com",
            "hash",
            "phone_user",
            "Old",
            "Name");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _users.Setup(x => x.PhoneNumberExistsAsync("+49123456789", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = Create();

        var result = await handler.Handle(
            new UpdateMeCommand(null, null, null, "+49 123 456 789"),
            CancellationToken.None);

        user.PhoneNumber.Should().Be("+49123456789");
        result.PhoneNumber.Should().Be("+49123456789");

        _users.Verify(x => x.PhoneNumberExistsAsync("+49123456789", It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPhoneNumberAlreadyTaken()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "update6@test.com",
            "hash",
            "phone_taken",
            "Old",
            "Name");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _users.Setup(x => x.PhoneNumberExistsAsync("+49111111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Create();

        var act = () => handler.Handle(
            new UpdateMeCommand(null, null, null, "+49 111 111 111"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Phone number is already used.");

        user.PhoneNumber.Should().BeNull();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldClearPhoneNumber_WhenEmptyStringProvided()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "update7@test.com",
            "hash",
            "phone_clear",
            "Old",
            "Name",
            phoneNumber: "+49123456789");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        var result = await handler.Handle(
            new UpdateMeCommand(null, null, null, "   "),
            CancellationToken.None);

        user.PhoneNumber.Should().BeNull();
        result.PhoneNumber.Should().BeNull();

        _users.Verify(x => x.PhoneNumberExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotCheckUniqueness_WhenPhoneNumberDidNotChangeAfterNormalization()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "update8@test.com",
            "hash",
            "phone_same",
            "Old",
            "Name",
            phoneNumber: "+49123456789");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        var result = await handler.Handle(
            new UpdateMeCommand(null, null, null, "+49 123 456 789"),
            CancellationToken.None);

        user.PhoneNumber.Should().Be("+49123456789");
        result.PhoneNumber.Should().Be("+49123456789");

        _users.Verify(x => x.PhoneNumberExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}