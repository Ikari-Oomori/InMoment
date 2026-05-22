using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Users.CompleteOnboarding;
using InMoment.Domain.Common;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Users.CompleteOnboarding;

public sealed class CompleteOnboardingHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private CompleteOnboardingHandler Create()
        => new(
            _users.Object,
            _uow.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsEmpty()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new CompleteOnboardingCommand(), CancellationToken.None);

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

        var act = () => handler.Handle(new CompleteOnboardingCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("User not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenContactsStepIsNotCompleted()
    {
        var user = User.Create(
            email: "complete@test.com",
            passwordHash: "hash",
            userName: "complete_user",
            firstName: "Complete",
            lastName: "User");

        var currentUserId = user.Id;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        var act = () => handler.Handle(new CompleteOnboardingCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Contacts step must be completed before onboarding can be finished.");

        user.IsOnboardingCompleted.Should().BeFalse();
        user.OnboardingCompletedAt.Should().BeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCompleteOnboarding_WhenContactsStepAlreadyCompleted()
    {
        var user = User.Create(
            email: "complete2@test.com",
            passwordHash: "hash",
            userName: "complete_user_2",
            firstName: "Complete",
            lastName: "User");

        user.MarkContactsStepCompleted(skipped: true);

        var currentUserId = user.Id;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        await handler.Handle(new CompleteOnboardingCommand(), CancellationToken.None);

        user.IsOnboardingCompleted.Should().BeTrue();
        user.OnboardingCompletedAt.Should().NotBeNull();
        user.HasCompletedContactsStep.Should().BeTrue();
        user.SkippedContactsImport.Should().BeTrue();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenOnboardingAlreadyCompleted()
    {
        var user = User.Create(
            email: "complete3@test.com",
            passwordHash: "hash",
            userName: "complete_user_3",
            firstName: "Complete",
            lastName: "User");

        user.MarkContactsStepCompleted(skipped: false);
        user.CompleteOnboarding();

        var completedAt = user.OnboardingCompletedAt;
        var currentUserId = user.Id;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        await handler.Handle(new CompleteOnboardingCommand(), CancellationToken.None);

        user.IsOnboardingCompleted.Should().BeTrue();
        user.OnboardingCompletedAt.Should().Be(completedAt);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}