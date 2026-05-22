using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Contacts.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Contacts;
using InMoment.Domain.Privacy;
using MediatR;

namespace InMoment.Application.Features.Contacts.Import;

public sealed class ImportContactsHandler
    : IRequestHandler<ImportContactsCommand, ImportContactsResultDto>
{
    private readonly IUserRepository _users;
    private readonly IFriendshipRepository _friendships;
    private readonly IFriendRequestRepository _requests;
    private readonly IContactImportLogRepository _logs;
    private readonly IPrivacySettingsRepository _privacy;
    private readonly IBlockedUserRepository _blocks;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public ImportContactsHandler(
        IUserRepository users,
        IFriendshipRepository friendships,
        IFriendRequestRepository requests,
        IContactImportLogRepository logs,
        IPrivacySettingsRepository privacy,
        IBlockedUserRepository blocks,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _users = users;
        _friendships = friendships;
        _requests = requests;
        _logs = logs;
        _privacy = privacy;
        _blocks = blocks;
        _current = current;
        _uow = uow;
    }

    public async Task<ImportContactsResultDto> Handle(ImportContactsCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var user = await _users.GetByIdAsync(_current.UserId, ct)
                   ?? throw new NotFoundException("User not found.");

        var contacts = cmd.Contacts ?? Array.Empty<ContactImportItemDto>();

        var normalizedPhones = contacts
            .SelectMany(x => x.Phones ?? Array.Empty<string>())
            .Select(PhoneNumberNormalizer.Normalize)
            .Where(x => x is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var normalizedEmails = contacts
            .SelectMany(x => x.Emails ?? Array.Empty<string>())
            .Select(NormalizeEmail)
            .Where(x => x is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var phoneToSourceContact = BuildPhoneToSourceContactMap(contacts);
        var emailToSourceContact = BuildEmailToSourceContactMap(contacts);

        var candidates = new Dictionary<Guid, MatchedContactCandidate>();

        if (normalizedPhones.Count > 0)
        {
            foreach (var phone in normalizedPhones)
            {
                var matchedUser = await _users.GetByPhoneNumberAsync(phone, ct);
                if (matchedUser is null || !matchedUser.IsActive)
                    continue;

                if (candidates.ContainsKey(matchedUser.Id))
                    continue;

                phoneToSourceContact.TryGetValue(phone, out var sourceContact);

                candidates[matchedUser.Id] = new MatchedContactCandidate(
                    matchedUser,
                    "phone",
                    phone,
                    NormalizeDisplayName(sourceContact?.DisplayName));
            }
        }

        if (normalizedEmails.Count > 0)
        {
            var matchedUsers = await _users.GetByEmailsAsync(normalizedEmails, ct);

            var filteredUsers = matchedUsers
                .Where(x => x.IsActive)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToList();

            foreach (var matchedUser in filteredUsers)
            {
                if (candidates.ContainsKey(matchedUser.Id))
                    continue;

                var matchedEmail = NormalizeEmail(matchedUser.Email);
                emailToSourceContact.TryGetValue(matchedEmail ?? string.Empty, out var sourceContact);

                candidates[matchedUser.Id] = new MatchedContactCandidate(
                    matchedUser,
                    "email",
                    matchedEmail,
                    NormalizeDisplayName(sourceContact?.DisplayName));
            }
        }

        var resolvedPhones = new HashSet<string>(StringComparer.Ordinal);
        var resolvedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates.Values)
        {
            if (candidate.MatchedBy == "phone" && !string.IsNullOrWhiteSpace(candidate.MatchedValue))
                resolvedPhones.Add(candidate.MatchedValue);

            if (candidate.MatchedBy == "email" && !string.IsNullOrWhiteSpace(candidate.MatchedValue))
                resolvedEmails.Add(candidate.MatchedValue);
        }

        var matches = new List<ContactMatchDto>(candidates.Count);

        foreach (var candidate in candidates.Values)
        {
            var matchedUser = candidate.User;

            if (matchedUser.Id == _current.UserId)
                continue;

            if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, matchedUser.Id, ct))
                continue;

            var privacy = await _privacy.GetByUserIdAsync(matchedUser.Id, ct);
            if (privacy is not null && !privacy.DiscoverableByContacts)
                continue;

            var friendship = await _friendships.GetByUsersAsync(_current.UserId, matchedUser.Id, ct);
            var pending = await _requests.GetPendingBetweenUsersAsync(_current.UserId, matchedUser.Id, ct);

            var hasIncoming = pending is not null && pending.ToUserId == _current.UserId;
            var hasOutgoing = pending is not null && pending.FromUserId == _current.UserId;
            var alreadyFriend = friendship is not null;

            var canSendFriendRequest =
                !alreadyFriend &&
                !hasIncoming &&
                !hasOutgoing &&
                CanReceiveFriendRequest(privacy);

            matches.Add(new ContactMatchDto(
                UserId: matchedUser.Id,
                UserName: matchedUser.UserName,
                FirstName: matchedUser.FirstName,
                LastName: matchedUser.LastName,
                ProfilePhotoUrl: matchedUser.ProfilePhotoUrl,
                MatchedBy: candidate.MatchedBy,
                MatchedValue: candidate.MatchedValue,
                SourceContactDisplayName: candidate.SourceContactDisplayName,
                AlreadyFriend: alreadyFriend,
                HasIncomingRequest: hasIncoming,
                HasOutgoingRequest: hasOutgoing,
                CanSendFriendRequest: canSendFriendRequest));
        }

        var invites = BuildInviteCandidates(contacts, resolvedPhones, resolvedEmails);

        var submittedCount = contacts.Sum(x =>
            (x.Emails?.Count ?? 0) + (x.Phones?.Count ?? 0));

        var log = ContactImportLog.Create(
            _current.UserId,
            contactsSubmitted: submittedCount,
            matchesFound: matches.Count);

        user.MarkContactsStepCompleted(skipped: false);

        await _logs.AddAsync(log, ct);
        await _uow.SaveChangesAsync(ct);

        var orderedMatches = matches
            .OrderByDescending(x => x.AlreadyFriend)
            .ThenByDescending(x => x.HasIncomingRequest)
            .ThenByDescending(x => x.CanSendFriendRequest)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .ThenBy(x => x.UserName)
            .ToList();

        return new ImportContactsResultDto(
            Matches: orderedMatches,
            Invites: invites);
    }

    private static IReadOnlyList<ContactInviteCandidateDto> BuildInviteCandidates(
        IReadOnlyList<ContactImportItemDto> contacts,
        ISet<string> resolvedPhones,
        ISet<string> resolvedEmails)
    {
        var invites = new List<ContactInviteCandidateDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var contact in contacts)
        {
            var displayName = NormalizeDisplayName(contact.DisplayName);

            var phones = (contact.Phones ?? Array.Empty<string>())
                .Select(PhoneNumberNormalizer.Normalize)
                .Where(x => x is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var emails = (contact.Emails ?? Array.Empty<string>())
                .Select(NormalizeEmail)
                .Where(x => x is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var hasResolvedPhone = phones.Any(resolvedPhones.Contains);
            var hasResolvedEmail = emails.Any(resolvedEmails.Contains);

            if (hasResolvedPhone || hasResolvedEmail)
                continue;

            var invitePhone = phones.FirstOrDefault();
            var inviteEmail = emails.FirstOrDefault();

            if (invitePhone is null && inviteEmail is null)
                continue;

            var dedupKey = $"{invitePhone ?? string.Empty}|{inviteEmail ?? string.Empty}";
            if (!seen.Add(dedupKey))
                continue;

            invites.Add(new ContactInviteCandidateDto(
                DisplayName: displayName,
                Phone: invitePhone,
                Email: inviteEmail));
        }

        return invites;
    }

    private static Dictionary<string, ContactImportItemDto> BuildPhoneToSourceContactMap(
        IReadOnlyList<ContactImportItemDto> contacts)
    {
        var map = new Dictionary<string, ContactImportItemDto>(StringComparer.Ordinal);

        foreach (var contact in contacts)
        {
            foreach (var phone in contact.Phones ?? Array.Empty<string>())
            {
                var normalized = PhoneNumberNormalizer.Normalize(phone);
                if (normalized is null)
                    continue;

                if (!map.ContainsKey(normalized))
                    map[normalized] = contact;
            }
        }

        return map;
    }

    private static Dictionary<string, ContactImportItemDto> BuildEmailToSourceContactMap(
        IReadOnlyList<ContactImportItemDto> contacts)
    {
        var map = new Dictionary<string, ContactImportItemDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var contact in contacts)
        {
            foreach (var email in contact.Emails ?? Array.Empty<string>())
            {
                var normalized = NormalizeEmail(email);
                if (normalized is null)
                    continue;

                if (!map.ContainsKey(normalized))
                    map[normalized] = contact;
            }
        }

        return map;
    }

    private static bool CanReceiveFriendRequest(PrivacySettings? settings)
    {
        if (settings is null)
            return true;

        return settings.AllowFriendRequestsFrom == PrivacyAudience.Everyone;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim().ToLowerInvariant();
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizeDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        var normalized = displayName.Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private sealed record MatchedContactCandidate(
        Domain.Users.User User,
        string MatchedBy,
        string? MatchedValue,
        string? SourceContactDisplayName);
}