using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Contacts.Common;
using InMoment.Application.Features.Contacts.Import;
using InMoment.Domain.Common;
using InMoment.Domain.Contacts;
using InMoment.Domain.Friends;
using InMoment.Domain.Privacy;
using Moq;

namespace InMoment.Application.Tests.Contacts.Import;

public sealed class ImportContactsHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IFriendshipRepository> _friendships = new();
    private readonly Mock<IFriendRequestRepository> _requests = new();
    private readonly Mock<IContactImportLogRepository> _logs = new();
    private readonly Mock<IPrivacySettingsRepository> _privacy = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserNotAuthorized()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(Array.Empty<ContactImportItemDto>());

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");

        _logs.Verify(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyAndWriteLog_WhenContactsAreEmpty()
    {
        var currentUserId = Guid.NewGuid();
        ContactImportLog? addedLog = null;

        var currentUser = SetupCurrentUser(currentUserId);
        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Callback<ContactImportLog, CancellationToken>((log, _) => addedLog = log)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(Array.Empty<ContactImportItemDto>());

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().BeEmpty();

        _users.Verify(
            x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        addedLog.Should().NotBeNull();
        addedLog!.UserId.Should().Be(currentUserId);
        addedLog.ContactsSubmitted.Should().Be(0);
        addedLog.MatchesFound.Should().Be(0);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNormalizeAndDeduplicateEmails_BeforeSearching()
    {
        var currentUserId = Guid.NewGuid();
        IReadOnlyCollection<string>? capturedEmails = null;

        var currentUser = SetupCurrentUser(currentUserId);
        _users.Setup(x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<string>, CancellationToken>((emails, _) => capturedEmails = emails)
            .ReturnsAsync(Array.Empty<Domain.Users.User>());
        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var contacts = new[]
        {
            CreateContact(emails: new[] { " TEST@Example.com ", "test@example.com", "SECOND@example.com" }),
            CreateContact(emails: new[] { "second@example.com", "   ", "" })
        };

        var handler = CreateHandler();
        var command = new ImportContactsCommand(contacts);

        _ = await handler.Handle(command, CancellationToken.None);

        capturedEmails.Should().NotBeNull();
        capturedEmails!.Should().BeEquivalentTo(new[]
        {
            "test@example.com",
            "second@example.com"
        });
    }

    [Fact]
    public async Task Handle_ShouldNotQueryUsers_WhenNoValidEmailsRemainAfterNormalization()
    {
        var currentUserId = Guid.NewGuid();
        ContactImportLog? addedLog = null;

        var currentUser = SetupCurrentUser(currentUserId);
        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Callback<ContactImportLog, CancellationToken>((log, _) => addedLog = log)
            .Returns(Task.CompletedTask);

        var contacts = new[]
        {
            CreateContact(phones: new[] { "+100" }, emails: new[] { "", "   ", "\t" }),
            CreateContact(phones: new[] { "+200" }, emails: Array.Empty<string>())
        };

        var handler = CreateHandler();
        var command = new ImportContactsCommand(contacts);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().BeEmpty();

        _users.Verify(
            x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        addedLog.Should().NotBeNull();
        addedLog!.ContactsSubmitted.Should().Be(5);
        addedLog.MatchesFound.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFilterOutCurrentUser_AndDuplicateMatchedUsers()
    {
        var currentUserId = Guid.NewGuid();
        var currentUser = CreateUser(currentUserId, "me");
        var matchedUser = CreateUser(Guid.NewGuid(), "anna");

        SetupCurrentUser(currentUserId);
        _users.Setup(x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                currentUser,
                matchedUser,
                matchedUser
            });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _privacy.Setup(x => x.GetByUserIdAsync(matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);
        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);
        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(new[]
        {
            CreateContact(displayName: "Anna From Contacts", emails: new[] { "me@test.com", "anna@test.com" })
        });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().HaveCount(1);
        result.Matches[0].UserId.Should().Be(matchedUser.Id);
        result.Matches[0].MatchedValue.Should().Be("anna@test.com");
        result.Matches[0].SourceContactDisplayName.Should().Be("Anna From Contacts");
        result.Matches[0].CanSendFriendRequest.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSkipInactiveUsers()
    {
        var currentUserId = Guid.NewGuid();
        var inactiveUser = CreateUser(Guid.NewGuid(), "inactive");
        inactiveUser.Deactivate();

        var currentUser = SetupCurrentUser(currentUserId);
        _users.Setup(x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { inactiveUser });
        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(new[]
        {
            CreateContact(emails: new[] { "inactive@test.com" })
        });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().BeEmpty();

        _blocks.Verify(
            x => x.ExistsEitherDirectionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSkipBlockedUsers()
    {
        var currentUserId = Guid.NewGuid();
        var blockedUser = CreateUser(Guid.NewGuid(), "blocked");

        var currentUser = SetupCurrentUser(currentUserId);
        _users.Setup(x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { blockedUser });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, blockedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        ContactImportLog? addedLog = null;
        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Callback<ContactImportLog, CancellationToken>((log, _) => addedLog = log)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(new[]
        {
            CreateContact(emails: new[] { "blocked@test.com" })
        });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().BeEmpty();

        addedLog.Should().NotBeNull();
        addedLog!.MatchesFound.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldSkipUsersNotDiscoverableByContacts()
    {
        var currentUserId = Guid.NewGuid();
        var hiddenUser = CreateUser(Guid.NewGuid(), "hidden");

        var privacy = PrivacySettings.CreateDefault(hiddenUser.Id);
        privacy.Update(
            allowFriendRequestsFrom: PrivacyAudience.Everyone,
            allowGroupInvitesFrom: PrivacyAudience.Everyone,
            discoverableByContacts: false,
            discoverableBySearch: true);

        var currentUser = SetupCurrentUser(currentUserId);
        _users.Setup(x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { hiddenUser });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, hiddenUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _privacy.Setup(x => x.GetByUserIdAsync(hiddenUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privacy);
        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(new[]
        {
            CreateContact(emails: new[] { "hidden@test.com" })
        });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldIgnorePhoneOnlyContacts_AndReturnEmpty()
    {
        var currentUserId = Guid.NewGuid();
        ContactImportLog? addedLog = null;

        var currentUser = SetupCurrentUser(currentUserId);
        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Callback<ContactImportLog, CancellationToken>((log, _) => addedLog = log)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(new[]
        {
            CreateContact(
                displayName: "Phone Only",
                phones: new[] { "+1234567890" },
                emails: Array.Empty<string>())
        });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().BeEmpty();

        _users.Verify(
            x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        addedLog.Should().NotBeNull();
        addedLog!.ContactsSubmitted.Should().Be(1);
        addedLog.MatchesFound.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldMapAlreadyFriend_AndIncomingOutgoingRequests()
    {
        var currentUserId = Guid.NewGuid();

        var friendUser = CreateUser(Guid.NewGuid(), "friend", "Anna", "Friend");
        var incomingUser = CreateUser(Guid.NewGuid(), "incoming", "Boris", "Incoming");
        var outgoingUser = CreateUser(Guid.NewGuid(), "outgoing", "Clara", "Outgoing");

        var currentUser = SetupCurrentUser(currentUserId);
        _users.Setup(x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { friendUser, incomingUser, outgoingUser });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, friendUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, incomingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, outgoingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privacy.Setup(x => x.GetByUserIdAsync(friendUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);
        _privacy.Setup(x => x.GetByUserIdAsync(incomingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);
        _privacy.Setup(x => x.GetByUserIdAsync(outgoingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, friendUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Friendship.Create(currentUserId, friendUser.Id));
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, incomingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, outgoingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, friendUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);
        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, incomingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FriendRequest.Create(incomingUser.Id, currentUserId));
        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, outgoingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FriendRequest.Create(currentUserId, outgoingUser.Id));

        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(new[]
        {
            CreateContact(emails: new[] { "friend@test.com", "incoming@test.com", "outgoing@test.com" })
        });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().HaveCount(3);

        result.Matches.Should().ContainSingle(x =>
            x.UserId == friendUser.Id &&
            x.AlreadyFriend &&
            !x.HasIncomingRequest &&
            !x.HasOutgoingRequest &&
            !x.CanSendFriendRequest &&
            x.MatchedBy == "email" &&
            x.MatchedValue == "friend@test.com");

        result.Matches.Should().ContainSingle(x =>
            x.UserId == incomingUser.Id &&
            !x.AlreadyFriend &&
            x.HasIncomingRequest &&
            !x.HasOutgoingRequest &&
            !x.CanSendFriendRequest &&
            x.MatchedBy == "email" &&
            x.MatchedValue == "incoming@test.com");

        result.Matches.Should().ContainSingle(x =>
            x.UserId == outgoingUser.Id &&
            !x.AlreadyFriend &&
            !x.HasIncomingRequest &&
            x.HasOutgoingRequest &&
            !x.CanSendFriendRequest &&
            x.MatchedBy == "email" &&
            x.MatchedValue == "outgoing@test.com");
    }

    [Fact]
    public async Task Handle_ShouldSetCanSendFriendRequestFalse_WhenPrivacyDisallowsRequests()
    {
        var currentUserId = Guid.NewGuid();
        var targetUser = CreateUser(Guid.NewGuid(), "hiddenrequests", "Hidden", "Requests");

        var privacy = PrivacySettings.CreateDefault(targetUser.Id);
        privacy.Update(
            allowFriendRequestsFrom: PrivacyAudience.Nobody,
            allowGroupInvitesFrom: PrivacyAudience.Everyone,
            discoverableByContacts: true,
            discoverableBySearch: true);

        var currentUser = SetupCurrentUser(currentUserId);
        _users.Setup(x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { targetUser });
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, targetUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _privacy.Setup(x => x.GetByUserIdAsync(targetUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privacy);
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, targetUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);
        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, targetUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);
        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new ImportContactsCommand(new[]
            {
                CreateContact(displayName: "Hidden Requests", emails: new[] { "hiddenrequests@test.com" })
            }),
            CancellationToken.None);

        result.Matches.Should().ContainSingle();
        result.Matches[0].CanSendFriendRequest.Should().BeFalse();
        result.Matches[0].SourceContactDisplayName.Should().Be("Hidden Requests");
        result.Matches[0].MatchedValue.Should().Be("hiddenrequests@test.com");
    }

    [Fact]
    public async Task Handle_ShouldSort_FriendsFirst_ThenIncoming_ThenCanSend_ThenByName()
    {
        var currentUserId = Guid.NewGuid();

        var zedFriend = CreateUser(Guid.NewGuid(), "zedfriend", "Zed", "Friend");
        var bobIncoming = CreateUser(Guid.NewGuid(), "bobincoming", "Bob", "Incoming");
        var alexPlain = CreateUser(Guid.NewGuid(), "alexplain", "Alex", "Plain");
        var carlClosed = CreateUser(Guid.NewGuid(), "carlclosed", "Carl", "Closed");

        var closedPrivacy = PrivacySettings.CreateDefault(carlClosed.Id);
        closedPrivacy.Update(
            allowFriendRequestsFrom: PrivacyAudience.Nobody,
            allowGroupInvitesFrom: PrivacyAudience.Everyone,
            discoverableByContacts: true,
            discoverableBySearch: true);

        var currentUser = SetupCurrentUser(currentUserId);
        _users.Setup(x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { alexPlain, zedFriend, bobIncoming, carlClosed });

        foreach (var user in new[] { alexPlain, zedFriend, bobIncoming, carlClosed })
        {
            _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        _privacy.Setup(x => x.GetByUserIdAsync(zedFriend.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);
        _privacy.Setup(x => x.GetByUserIdAsync(bobIncoming.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);
        _privacy.Setup(x => x.GetByUserIdAsync(alexPlain.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);
        _privacy.Setup(x => x.GetByUserIdAsync(carlClosed.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(closedPrivacy);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, zedFriend.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Friendship.Create(currentUserId, zedFriend.Id));
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, bobIncoming.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, alexPlain.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, carlClosed.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, zedFriend.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);
        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, bobIncoming.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FriendRequest.Create(bobIncoming.Id, currentUserId));
        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, alexPlain.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);
        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, carlClosed.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);

        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(new[]
        {
            CreateContact(emails: new[] { "a@test.com", "b@test.com", "c@test.com", "d@test.com" })
        });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Select(x => x.UserId).Should().ContainInOrder(
            zedFriend.Id,
            bobIncoming.Id,
            alexPlain.Id,
            carlClosed.Id);
    }

    [Fact]
    public async Task Handle_ShouldMatchUserByPhone_WhenPhoneExists()
    {
        var currentUserId = Guid.NewGuid();
        var matchedUser = CreateUser(
            Guid.NewGuid(),
            "annaphone",
            firstName: "Anna",
            lastName: "Phone",
            phoneNumber: "+49123456789");

        var currentUser = SetupCurrentUser(currentUserId);

        _users.Setup(x => x.GetByPhoneNumberAsync("+49123456789", It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchedUser);

        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privacy.Setup(x => x.GetByUserIdAsync(matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(new[]
        {
        CreateContact(
            displayName: "Anna From Phonebook",
            phones: new[] { "+49 123 456 789" },
            emails: Array.Empty<string>())
    });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().ContainSingle();

        result.Matches[0].UserId.Should().Be(matchedUser.Id);
        result.Matches[0].MatchedBy.Should().Be("phone");
        result.Matches[0].MatchedValue.Should().Be("+49123456789");
        result.Matches[0].SourceContactDisplayName.Should().Be("Anna From Phonebook");

        _users.Verify(
            x => x.GetByPhoneNumberAsync("+49123456789", It.IsAny<CancellationToken>()),
            Times.Once);

        _users.Verify(
            x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldPreferPhoneMatch_WhenUserMatchedByPhoneAndEmail()
    {
        var currentUserId = Guid.NewGuid();
        var matchedUser = CreateUser(
            Guid.NewGuid(),
            "dualmatch",
            firstName: "Dual",
            lastName: "Match",
            phoneNumber: "+49111111111");

        var currentUser = SetupCurrentUser(currentUserId);

        _users.Setup(x => x.GetByPhoneNumberAsync("+49111111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchedUser);

        _users.Setup(x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { matchedUser });

        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privacy.Setup(x => x.GetByUserIdAsync(matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(new[]
        {
        CreateContact(
            displayName: "Dual Contact",
            phones: new[] { "+49 111 111 111" },
            emails: new[] { "dualmatch@test.com" })
    });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().ContainSingle();

        result.Matches[0].UserId.Should().Be(matchedUser.Id);
        result.Matches[0].MatchedBy.Should().Be("phone");
        result.Matches[0].MatchedValue.Should().Be("+49111111111");
        result.Matches[0].SourceContactDisplayName.Should().Be("Dual Contact");
    }

    [Fact]
    public async Task Handle_ShouldFallbackToEmail_WhenPhoneDidNotMatch()
    {
        var currentUserId = Guid.NewGuid();
        var matchedUser = CreateUser(
            Guid.NewGuid(),
            "emailfallback",
            firstName: "Email",
            lastName: "Fallback");

        var currentUser = SetupCurrentUser(currentUserId);

        _users.Setup(x => x.GetByPhoneNumberAsync("+49222222222", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Users.User?)null);

        _users.Setup(x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { matchedUser });

        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privacy.Setup(x => x.GetByUserIdAsync(matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(new[]
        {
        CreateContact(
            displayName: "Email Fallback",
            phones: new[] { "+49 222 222 222" },
            emails: new[] { "emailfallback@test.com" })
    });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().ContainSingle();

        result.Matches[0].UserId.Should().Be(matchedUser.Id);
        result.Matches[0].MatchedBy.Should().Be("email");
        result.Matches[0].MatchedValue.Should().Be("emailfallback@test.com");
        result.Matches[0].SourceContactDisplayName.Should().Be("Email Fallback");
    }

    [Fact]
    public async Task Handle_ShouldWriteLog_WithSubmittedCountAndMatchesFound()
    {
        var currentUserId = Guid.NewGuid();
        var matchedUser = CreateUser(Guid.NewGuid(), "anna");

        ContactImportLog? addedLog = null;

        var currentUser = SetupCurrentUser(currentUserId);
        _users.Setup(x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { matchedUser });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _privacy.Setup(x => x.GetByUserIdAsync(matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);
        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);

        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Callback<ContactImportLog, CancellationToken>((log, _) => addedLog = log)
            .Returns(Task.CompletedTask);

        var contacts = new[]
        {
            CreateContact(
                phones: new[] { "+100", "+200" },
                emails: new[] { "anna@test.com", "other@test.com" }),
            CreateContact(
                phones: new[] { "+300" },
                emails: new[] { "third@test.com" })
        };

        var handler = CreateHandler();
        var command = new ImportContactsCommand(contacts);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().HaveCount(1);

        addedLog.Should().NotBeNull();
        addedLog!.UserId.Should().Be(currentUserId);
        addedLog.ContactsSubmitted.Should().Be(6);
        addedLog.MatchesFound.Should().Be(1);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldMarkContactsStepCompleted_WhenImportSucceeded()
    {
        var currentUserId = Guid.NewGuid();
        var currentUser = SetupCurrentUser(currentUserId);
        var matchedUser = CreateUser(Guid.NewGuid(), "anna");

        _users.Setup(x => x.GetByEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { matchedUser });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privacy.Setup(x => x.GetByUserIdAsync(matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, matchedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);

        _logs.Setup(x => x.AddAsync(It.IsAny<ContactImportLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new ImportContactsCommand(new[]
        {
        CreateContact(displayName: "Anna From Contacts", emails: new[] { "anna@test.com" })
    });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Matches.Should().ContainSingle();
        currentUser.HasCompletedContactsStep.Should().BeTrue();
        currentUser.SkippedContactsImport.Should().BeFalse();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    private ImportContactsHandler CreateHandler()
        => new(
            _users.Object,
            _friendships.Object,
            _requests.Object,
            _logs.Object,
            _privacy.Object,
            _blocks.Object,
            _current.Object,
            _uow.Object);

    private static ContactImportItemDto CreateContact(
        string? displayName = null,
        IReadOnlyList<string>? phones = null,
        IReadOnlyList<string>? emails = null)
        => new(
            DisplayName: displayName,
            Phones: phones ?? Array.Empty<string>(),
            Emails: emails ?? Array.Empty<string>());

    private static Domain.Users.User CreateUser(
    Guid id,
    string userName,
    string firstName = "Test",
    string lastName = "User",
    string? phoneNumber = null)
    {
        var user = Domain.Users.User.Create(
            email: $"{userName}@test.com",
            passwordHash: "hash",
            userName: userName,
            firstName: firstName,
            lastName: lastName,
            phoneNumber: phoneNumber);

        typeof(Domain.Users.User)
            .GetProperty(nameof(Domain.Users.User.Id))!
            .SetValue(user, id);

        return user;
    }

    private Domain.Users.User SetupCurrentUser(Guid currentUserId, string userName = "current_user")
    {
        var user = CreateUser(currentUserId, userName);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        return user;
    }
}