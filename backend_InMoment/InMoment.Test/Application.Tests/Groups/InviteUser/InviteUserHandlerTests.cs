using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.InviteUser;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Notifications;
using InMoment.Domain.Privacy;
using InMoment.Domain.Users;

namespace InMoment.Application.Tests.Groups.InviteUser;

public sealed class InviteUserHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IInvitationRepository> _invitations = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<INotificationRealtime> _notificationRealtime = new();
    private readonly Mock<IPrivacySettingsRepository> _privacy = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<IFriendshipRepository> _friendships = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<INotificationPushDeliveryService> _pushDelivery = new();

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenGroupIdEmpty()
    {
        var handler = CreateHandler();
        var command = new InviteUserCommand(Guid.Empty, "target");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("GroupId is required.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldThrowValidationException_WhenLoginEmpty(string login)
    {
        var handler = CreateHandler();
        var command = new InviteUserCommand(Guid.NewGuid(), login);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Login is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = CreateHandler();
        var command = new InviteUserCommand(groupId, "target");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserCannotManageGroup()
    {
        var ownerId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("Test group", ownerId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = CreateHandler();
        var command = new InviteUserCommand(group.Id, "target");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not a member of this group.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenInvitedUserNotFound_ByUserName()
    {
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("Test group", currentUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _users.Setup(x => x.GetByUserNameAsync("target_user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = CreateHandler();
        var command = new InviteUserCommand(group.Id, "target_user");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("User not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenInvitedUserInactive()
    {
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("Test group", currentUserId);
        var invitedUser = User.Create(
            email: "target@example.com",
            passwordHash: "hash",
            userName: "target",
            firstName: "Target",
            lastName: "User");

        invitedUser.Deactivate();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _users.Setup(x => x.GetByUserNameAsync("target", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitedUser);

        var handler = CreateHandler();
        var command = new InviteUserCommand(group.Id, "target");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("User not found.");
    }

    [Fact]
    public async Task Handle_ShouldUseLowerCasedEmail_WhenLoginLooksLikeEmail()
    {
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("Test group", currentUserId);
        var invitedUser = User.Create(
            email: "target@example.com",
            passwordHash: "hash",
            userName: "target_user",
            firstName: "Target",
            lastName: "User");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _users.Setup(x => x.GetByEmailAsync("target@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitedUser);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _groups.Setup(x => x.IsMemberAsync(group.Id, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privacy.Setup(x => x.GetByUserIdAsync(invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InMoment.Domain.Friends.Friendship?)null);

        _invitations.Setup(x => x.HasPendingAsync(group.Id, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                invitedUser.Id,
                NotificationType.GroupInvitationReceived,
                currentUserId,
                group.Id,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        GroupInvitation? addedInvitation = null;
        _invitations.Setup(x => x.AddAsync(It.IsAny<GroupInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<GroupInvitation, CancellationToken>((x, _) => addedInvitation = x)
            .Returns(Task.CompletedTask);

        Notification? addedNotification = null;
        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((x, _) => addedNotification = x)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.GetUnreadCountAsync(invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var command = new InviteUserCommand(group.Id, "  TARGET@EXAMPLE.COM  ");

        var result = await handler.Handle(command, CancellationToken.None);

        result.InvitationId.Should().NotBe(Guid.Empty);

        addedInvitation.Should().NotBeNull();
        addedInvitation!.GroupId.Should().Be(group.Id);
        addedInvitation.InvitedUserId.Should().Be(invitedUser.Id);
        addedInvitation.InvitedByUserId.Should().Be(currentUserId);

        addedNotification.Should().NotBeNull();
        addedNotification!.UserId.Should().Be(invitedUser.Id);
        addedNotification.Type.Should().Be(NotificationType.GroupInvitationReceived);
        addedNotification.ActorUserId.Should().Be(currentUserId);
        addedNotification.GroupId.Should().Be(group.Id);
        addedNotification.InvitationId.Should().Be(addedInvitation.Id);

        _users.Verify(x => x.GetByEmailAsync("target@example.com", It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(x => x.GetByUserNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(invitedUser.Id, 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenInvitingSelf()
    {
        var currentUser = User.Create(
            email: "me@example.com",
            passwordHash: "hash",
            userName: "me",
            firstName: "Me",
            lastName: "User");

        var group = Group.Create("Test group", currentUser.Id);

        _current.SetupGet(x => x.UserId).Returns(currentUser.Id);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _users.Setup(x => x.GetByUserNameAsync("me", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentUser);

        var handler = CreateHandler();
        var command = new InviteUserCommand(group.Id, "me");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("You cannot invite yourself.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUsersAreBlocked()
    {
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("Test group", currentUserId);
        var invitedUser = User.Create(
            email: "target@example.com",
            passwordHash: "hash",
            userName: "target",
            firstName: "Target",
            lastName: "User");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _users.Setup(x => x.GetByUserNameAsync("target", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitedUser);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new InviteUserCommand(group.Id, "target");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Взаимодействие с этим пользователем недоступно.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenUserAlreadyMember()
    {
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("Test group", currentUserId);
        var invitedUser = User.Create(
            email: "target@example.com",
            passwordHash: "hash",
            userName: "target",
            firstName: "Target",
            lastName: "User");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _users.Setup(x => x.GetByUserNameAsync("target", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitedUser);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _groups.Setup(x => x.IsMemberAsync(group.Id, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new InviteUserCommand(group.Id, "target");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("User is already a member of this group.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenPrivacyDoesNotAllowGroupInvites()
    {
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("Test group", currentUserId);
        var invitedUser = User.Create(
            email: "target@example.com",
            passwordHash: "hash",
            userName: "target",
            firstName: "Target",
            lastName: "User");

        var privacy = PrivacySettings.CreateDefault(invitedUser.Id);
        privacy.Update(
            allowFriendRequestsFrom: PrivacyAudience.Everyone,
            allowGroupInvitesFrom: PrivacyAudience.Nobody,
            discoverableByContacts: true,
            discoverableBySearch: true);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _users.Setup(x => x.GetByUserNameAsync("target", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitedUser);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _groups.Setup(x => x.IsMemberAsync(group.Id, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privacy.Setup(x => x.GetByUserIdAsync(invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privacy);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InMoment.Domain.Friends.Friendship?)null);

        var handler = CreateHandler();
        var command = new InviteUserCommand(group.Id, "target");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не принимает приглашения в группы.");
    }

    [Fact]
    public async Task Handle_ShouldAllowInvite_WhenPrivacyIsFriendsOnly_AndUsersAreFriends()
    {
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("Test group", currentUserId);
        var invitedUser = User.Create(
            email: "target@example.com",
            passwordHash: "hash",
            userName: "target",
            firstName: "Target",
            lastName: "User");

        var privacy = PrivacySettings.CreateDefault(invitedUser.Id);
        privacy.Update(
            allowFriendRequestsFrom: PrivacyAudience.Everyone,
            allowGroupInvitesFrom: PrivacyAudience.FriendsOnly,
            discoverableByContacts: true,
            discoverableBySearch: true);

        var friendship = InMoment.Domain.Friends.Friendship.Create(currentUserId, invitedUser.Id);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _users.Setup(x => x.GetByUserNameAsync("target", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitedUser);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _groups.Setup(x => x.IsMemberAsync(group.Id, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privacy.Setup(x => x.GetByUserIdAsync(invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privacy);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(friendship);

        _invitations.Setup(x => x.HasPendingAsync(group.Id, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                invitedUser.Id,
                NotificationType.GroupInvitationReceived,
                currentUserId,
                group.Id,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        GroupInvitation? addedInvitation = null;
        _invitations.Setup(x => x.AddAsync(It.IsAny<GroupInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<GroupInvitation, CancellationToken>((x, _) => addedInvitation = x)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.GetUnreadCountAsync(invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var handler = CreateHandler();
        var command = new InviteUserCommand(group.Id, "target");

        var result = await handler.Handle(command, CancellationToken.None);

        result.InvitationId.Should().Be(addedInvitation!.Id);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPendingInvitationAlreadyExists()
    {
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("Test group", currentUserId);
        var invitedUser = User.Create(
            email: "target@example.com",
            passwordHash: "hash",
            userName: "target",
            firstName: "Target",
            lastName: "User");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _users.Setup(x => x.GetByUserNameAsync("target", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitedUser);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _groups.Setup(x => x.IsMemberAsync(group.Id, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privacy.Setup(x => x.GetByUserIdAsync(invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InMoment.Domain.Friends.Friendship?)null);

        _invitations.Setup(x => x.HasPendingAsync(group.Id, invitedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new InviteUserCommand(group.Id, "target");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Pending invitation already exists.");
    }

    private InviteUserHandler CreateHandler()
    => new(
        _groups.Object,
        _users.Object,
        _invitations.Object,
        _notifications.Object,
        _notificationRealtime.Object,
        _pushDelivery.Object,
        _privacy.Object,
        _blocks.Object,
        _friendships.Object,
        _uow.Object,
        _current.Object);
}