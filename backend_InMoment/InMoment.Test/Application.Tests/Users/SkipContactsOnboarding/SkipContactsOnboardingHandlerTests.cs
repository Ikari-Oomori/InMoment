using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Users.SkipContactsOnboarding;
using InMoment.Domain.Common;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Users.SkipContactsOnboarding;

public sealed class SkipContactsOnboardingHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private SkipContactsOnboardingHandler Create()
        => new(
            _users.Object,
            _uow.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsEmpty()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new SkipContactsOnboardingCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Unauthorized.");

        _users.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var act = () => handler.Handle(new SkipContactsOnboardingCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("User not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldMarkContactsStepCompleted_WithSkippedTrue()
    {
        var user = User.Create(
            email: "skip@test.com",
            passwordHash: "hash",
            userName: "skip_user",
            firstName: "Skip",
            lastName: "User");

        var currentUserId = user.Id;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        await handler.Handle(new SkipContactsOnboardingCommand(), CancellationToken.None);

        user.HasCompletedContactsStep.Should().BeTrue();
        user.SkippedContactsImport.Should().BeTrue();
        user.IsOnboardingCompleted.Should().BeFalse();
        user.OnboardingCompletedAt.Should().BeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRemainIdempotent_WhenContactsStepAlreadyCompleted()
    {
        var user = User.Create(
            email: "skip2@test.com",
            passwordHash: "hash",
            userName: "skip_user_2",
            firstName: "Skip",
            lastName: "User");

        user.MarkContactsStepCompleted(skipped: false);

        var currentUserId = user.Id;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        await handler.Handle(new SkipContactsOnboardingCommand(), CancellationToken.None);

        user.HasCompletedContactsStep.Should().BeTrue();
        user.SkippedContactsImport.Should().BeTrue();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}