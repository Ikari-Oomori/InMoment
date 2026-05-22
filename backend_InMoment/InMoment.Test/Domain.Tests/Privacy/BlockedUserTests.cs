using FluentAssertions;
using InMoment.Domain.Common;
using InMoment.Domain.Privacy;

namespace InMoment.Domain.Tests.Privacy;

public sealed class BlockedUserTests
{
    [Fact]
    public void Create_ShouldThrowValidationException_WhenUserIdEmpty()
    {
        var blockedUserId = Guid.NewGuid();

        var act = () => BlockedUser.Create(Guid.Empty, blockedUserId);

        act.Should().Throw<ValidationException>()
            .WithMessage("UserId is required.");
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenBlockedUserIdEmpty()
    {
        var userId = Guid.NewGuid();

        var act = () => BlockedUser.Create(userId, Guid.Empty);

        act.Should().Throw<ValidationException>()
            .WithMessage("BlockedUserId is required.");
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenBlockingSelf()
    {
        var userId = Guid.NewGuid();

        var act = () => BlockedUser.Create(userId, userId);

        act.Should().Throw<ValidationException>()
            .WithMessage("You cannot block yourself.");
    }

    [Fact]
    public void Create_ShouldCreateBlockedUser_WhenValid()
    {
        var userId = Guid.NewGuid();
        var blockedUserId = Guid.NewGuid();

        var result = BlockedUser.Create(userId, blockedUserId);

        result.Id.Should().NotBe(Guid.Empty);
        result.UserId.Should().Be(userId);
        result.BlockedUserId.Should().Be(blockedUserId);
        result.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}