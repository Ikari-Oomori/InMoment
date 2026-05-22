using FluentAssertions;
using InMoment.Domain.Common;
using InMoment.Domain.Users;

namespace InMoment.Tests.Domain.Tests.Users;

public sealed class UserTests
{
    [Fact]
    public void Create_ShouldNormalizeAndInitializeFields()
    {
        var user = User.Create(
            "  TEST@Example.COM  ",
            "hash123",
            "  user_name  ",
            "  Ivan  ",
            "  Petrov  ");

        user.Email.Should().Be("test@example.com");
        user.PasswordHash.Should().Be("hash123");
        user.UserName.Should().Be("user_name");
        user.FirstName.Should().Be("Ivan");
        user.LastName.Should().Be("Petrov");
        user.ProfilePhotoUrl.Should().BeNull();
        user.ActiveGroupId.Should().BeNull();
        user.IsActive.Should().BeTrue();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("", "hash", "user", "Ivan", "Petrov", "Email is required.")]
    [InlineData("mail@test.com", "", "user", "Ivan", "Petrov", "PasswordHash is required.")]
    [InlineData("mail@test.com", "hash", "", "Ivan", "Petrov", "UserName is required.")]
    [InlineData("mail@test.com", "hash", "user", "", "Petrov", "FirstName is required.")]
    [InlineData("mail@test.com", "hash", "user", "Ivan", "", "LastName is required.")]
    public void Create_ShouldThrowValidationException_WhenRequiredFieldMissing(
        string email,
        string passwordHash,
        string userName,
        string firstName,
        string lastName,
        string expectedMessage)
    {
        var act = () => User.Create(email, passwordHash, userName, firstName, lastName);

        act.Should().Throw<ValidationException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenEmailTooLong()
    {
        var email = new string('a', 257) + "@test.com";

        var act = () => User.Create(email, "hash", "user", "Ivan", "Petrov");

        act.Should().Throw<ValidationException>()
            .WithMessage("Email is too long.");
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenUserNameTooLong()
    {
        var userName = new string('u', 65);

        var act = () => User.Create("mail@test.com", "hash", userName, "Ivan", "Petrov");

        act.Should().Throw<ValidationException>()
            .WithMessage("UserName is too long.");
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenFirstNameTooLong()
    {
        var firstName = new string('f', 101);

        var act = () => User.Create("mail@test.com", "hash", "user", firstName, "Petrov");

        act.Should().Throw<ValidationException>()
            .WithMessage("FirstName is too long.");
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenLastNameTooLong()
    {
        var lastName = new string('l', 101);

        var act = () => User.Create("mail@test.com", "hash", "user", "Ivan", lastName);

        act.Should().Throw<ValidationException>()
            .WithMessage("LastName is too long.");
    }

    [Fact]
    public void ChangeName_ShouldTrimAndUpdateFields()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");

        user.ChangeName("  Petr  ", "  Sidorov  ");

        user.FirstName.Should().Be("Petr");
        user.LastName.Should().Be("Sidorov");
    }

    [Theory]
    [InlineData("", "Petrov", "FirstName is required.")]
    [InlineData("Ivan", "", "LastName is required.")]
    public void ChangeName_ShouldThrowValidationException_WhenRequiredFieldMissing(
        string firstName,
        string lastName,
        string expectedMessage)
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");

        var act = () => user.ChangeName(firstName, lastName);

        act.Should().Throw<ValidationException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public void ChangeName_ShouldThrowValidationException_WhenFirstNameTooLong()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");
        var firstName = new string('f', 101);

        var act = () => user.ChangeName(firstName, "Petrov");

        act.Should().Throw<ValidationException>()
            .WithMessage("FirstName is too long.");
    }

    [Fact]
    public void ChangeName_ShouldThrowValidationException_WhenLastNameTooLong()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");
        var lastName = new string('l', 101);

        var act = () => user.ChangeName("Ivan", lastName);

        act.Should().Throw<ValidationException>()
            .WithMessage("LastName is too long.");
    }

    [Fact]
    public void ChangeUserName_ShouldTrimAndUpdateValue()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");

        user.ChangeUserName("  new_user  ");

        user.UserName.Should().Be("new_user");
    }

    [Fact]
    public void ChangeUserName_ShouldThrowValidationException_WhenEmpty()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");

        var act = () => user.ChangeUserName("   ");

        act.Should().Throw<ValidationException>()
            .WithMessage("UserName is required.");
    }

    [Fact]
    public void ChangeUserName_ShouldThrowValidationException_WhenTooLong()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");
        var userName = new string('u', 65);

        var act = () => user.ChangeUserName(userName);

        act.Should().Throw<ValidationException>()
            .WithMessage("UserName is too long.");
    }

    [Fact]
    public void ChangePasswordHash_ShouldUpdatePasswordHash()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");

        user.ChangePasswordHash("new-hash");

        user.PasswordHash.Should().Be("new-hash");
    }

    [Fact]
    public void ChangePasswordHash_ShouldThrowValidationException_WhenEmpty()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");

        var act = () => user.ChangePasswordHash("  ");

        act.Should().Throw<ValidationException>()
            .WithMessage("Password hash is required.");
    }

    [Fact]
    public void SetProfilePhoto_ShouldTrimValue()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");

        user.SetProfilePhoto("  https://cdn.example.com/avatar.jpg  ");

        user.ProfilePhotoUrl.Should().Be("https://cdn.example.com/avatar.jpg");
    }

    [Fact]
    public void SetProfilePhoto_ShouldSetNull_WhenWhitespace()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");
        user.SetProfilePhoto("https://cdn.example.com/avatar.jpg");

        user.SetProfilePhoto("   ");

        user.ProfilePhotoUrl.Should().BeNull();
    }

    [Fact]
    public void SetActiveGroup_ShouldUpdateActiveGroup()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");
        var groupId = Guid.NewGuid();

        user.SetActiveGroup(groupId);

        user.ActiveGroupId.Should().Be(groupId);
    }

    [Fact]
    public void SetActiveGroup_ShouldAllowNull()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");
        user.SetActiveGroup(Guid.NewGuid());

        user.SetActiveGroup(null);

        user.ActiveGroupId.Should().BeNull();
    }

    [Fact]
    public void SetActiveGroup_ShouldThrowValidationException_WhenGuidEmpty()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");

        var act = () => user.SetActiveGroup(Guid.Empty);

        act.Should().Throw<ValidationException>()
            .WithMessage("ActiveGroupId is invalid.");
    }

    [Fact]
    public void UpdateProfile_ShouldUpdateNameAndPhoto()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");

        user.UpdateProfile("  Petr  ", "  Sidorov  ", "  https://cdn.example.com/p.jpg  ");

        user.FirstName.Should().Be("Petr");
        user.LastName.Should().Be("Sidorov");
        user.ProfilePhotoUrl.Should().Be("https://cdn.example.com/p.jpg");
    }

    [Fact]
    public void Deactivate_ShouldSetInactiveAndClearActiveGroup()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");
        user.SetActiveGroup(Guid.NewGuid());

        user.Deactivate();

        user.IsActive.Should().BeFalse();
        user.ActiveGroupId.Should().BeNull();
    }

    [Fact]
    public void Deactivate_ShouldDoNothing_WhenAlreadyInactive()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");
        user.Deactivate();

        var act = () => user.Deactivate();

        act.Should().NotThrow();
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void EnsureActive_ShouldNotThrow_WhenUserActive()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");

        var act = () => user.EnsureActive();

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureActive_ShouldThrowForbiddenException_WhenUserInactive()
    {
        var user = User.Create("mail@test.com", "hash", "user", "Ivan", "Petrov");
        user.Deactivate();

        var act = () => user.EnsureActive();

        act.Should().Throw<ForbiddenException>()
            .WithMessage("Account is deactivated.");
    }
}