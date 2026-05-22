using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Contacts;

public sealed class ContactsImportFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ContactsImportFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_Can_Import_Contacts_And_Only_Visible_Contacts_Are_Returned()
    {
        var importer = await Register(
            $"importer_{Guid.NewGuid():N}@test.com",
            $"importer_{Guid.NewGuid():N}");

        var visible = await Register(
            $"visible_{Guid.NewGuid():N}@test.com",
            $"visible_{Guid.NewGuid():N}");

        var hidden = await Register(
            $"hidden_{Guid.NewGuid():N}@test.com",
            $"hidden_{Guid.NewGuid():N}");

        var hiddenToken = await Login(hidden.email);
        SetAuth(_client, hiddenToken);

        var updateHiddenPrivacyResponse = await _client.PatchAsync("/api/privacy", Json(new
        {
            allowFriendRequestsFrom = 1,
            allowGroupInvitesFrom = 1,
            discoverableByContacts = false,
            discoverableBySearch = true
        }));

        updateHiddenPrivacyResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var importerToken = await Login(importer.email);
        SetAuth(_client, importerToken);

        var importResponse = await _client.PostAsync("/api/contacts/import", Json(new
        {
            contacts = new object[]
            {
                new
                {
                    displayName = "Visible Contact",
                    phones = Array.Empty<string>(),
                    emails = new[] { visible.email }
                },
                new
                {
                    displayName = "Hidden Contact",
                    phones = Array.Empty<string>(),
                    emails = new[] { hidden.email }
                }
            }
        }));

        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await importResponse.Content.ReadFromJsonAsync<ImportContactsResultDto>();
        result.Should().NotBeNull();

        result!.Matches.Should().ContainSingle();
        result.Invites.Should().BeEmpty();

        var match = result.Matches.Single();
        match.UserId.Should().Be(visible.userId);
        match.UserName.Should().Be(visible.userName);
        match.MatchedBy.Should().Be("email");
        match.MatchedValue.Should().Be(visible.email);
        match.SourceContactDisplayName.Should().Be("Visible Contact");
        match.AlreadyFriend.Should().BeFalse();
        match.HasIncomingRequest.Should().BeFalse();
        match.HasOutgoingRequest.Should().BeFalse();
        match.CanSendFriendRequest.Should().BeTrue();
    }

    [Fact]
    public async Task Import_ShouldIgnore_DuplicateAndDirtyEmails_AndReturnSingleMatch()
    {
        var importer = await Register(
            $"importer_{Guid.NewGuid():N}@test.com",
            $"importer_{Guid.NewGuid():N}");

        var visible = await Register(
            $"visible_{Guid.NewGuid():N}@test.com",
            $"visible_{Guid.NewGuid():N}");

        var importerToken = await Login(importer.email);
        SetAuth(_client, importerToken);

        var importResponse = await _client.PostAsync("/api/contacts/import", Json(new
        {
            contacts = new object[]
            {
                new
                {
                    displayName = "Visible 1",
                    phones = Array.Empty<string>(),
                    emails = new[] { $"  {visible.email.ToUpperInvariant()}  ", visible.email }
                },
                new
                {
                    displayName = "Visible 2",
                    phones = new[] { "+1234567890" },
                    emails = new[] { visible.email, "", "   " }
                }
            }
        }));

        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await importResponse.Content.ReadFromJsonAsync<ImportContactsResultDto>();
        result.Should().NotBeNull();

        result!.Matches.Should().ContainSingle();
        result.Invites.Should().BeEmpty();

        result.Matches[0].UserId.Should().Be(visible.userId);
        result.Matches[0].MatchedBy.Should().Be("email");
        result.Matches[0].MatchedValue.Should().Be(visible.email);
        result.Matches[0].SourceContactDisplayName.Should().Be("Visible 1");
    }

    [Fact]
    public async Task Import_ShouldReturn_InviteCandidates_ForUnknownPhoneOnlyContacts()
    {
        var importer = await Register(
            $"importer_{Guid.NewGuid():N}@test.com",
            $"importer_{Guid.NewGuid():N}");

        var importerToken = await Login(importer.email);
        SetAuth(_client, importerToken);

        var importResponse = await _client.PostAsync("/api/contacts/import", Json(new
        {
            contacts = new object[]
            {
                new
                {
                    displayName = "Phone Only 1",
                    phones = new[] { "+111111111" },
                    emails = Array.Empty<string>()
                },
                new
                {
                    displayName = "Phone Only 2",
                    phones = new[] { "+222222222" },
                    emails = Array.Empty<string>()
                }
            }
        }));

        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await importResponse.Content.ReadFromJsonAsync<ImportContactsResultDto>();
        result.Should().NotBeNull();

        result!.Matches.Should().BeEmpty();
        result.Invites.Should().HaveCount(2);

        result.Invites.Select(x => x.Phone).Should().BeEquivalentTo(new[]
        {
            "+111111111",
            "+222222222"
        });
    }

    [Fact]
    public async Task Import_ShouldNotReturn_CurrentUser_WhenOwnEmailIsSubmitted()
    {
        var importer = await Register(
            $"importer_{Guid.NewGuid():N}@test.com",
            $"importer_{Guid.NewGuid():N}");

        var importerToken = await Login(importer.email);
        SetAuth(_client, importerToken);

        var importResponse = await _client.PostAsync("/api/contacts/import", Json(new
        {
            contacts = new object[]
            {
                new
                {
                    displayName = "Self",
                    phones = Array.Empty<string>(),
                    emails = new[] { importer.email }
                }
            }
        }));

        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await importResponse.Content.ReadFromJsonAsync<ImportContactsResultDto>();
        result.Should().NotBeNull();

        result!.Matches.Should().BeEmpty();
        result.Invites.Should().BeEmpty();
    }

    [Fact]
    public async Task Contacts_Import_Endpoint_Requires_Authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsync("/api/contacts/import", Json(new
        {
            contacts = new object[]
            {
                new
                {
                    displayName = "No Auth",
                    phones = Array.Empty<string>(),
                    emails = new[] { "test@example.com" }
                }
            }
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ImportContacts_ShouldMatchUserByPhone()
    {
        var ownerEmail = $"contacts_owner_{Guid.NewGuid():N}@test.com";
        var ownerUserName = $"contactsowner_{Guid.NewGuid():N}";
        await Register(ownerEmail, ownerUserName, "Owner", "User");

        var targetEmail = $"contacts_target_{Guid.NewGuid():N}@test.com";
        var targetUserName = $"contactstarget_{Guid.NewGuid():N}";
        await Register(
            targetEmail,
            targetUserName,
            "Phone",
            "Target",
            phoneNumber: "+49 123 456 789");

        var token = await Login(ownerEmail);
        SetAuth(_client, token);

        var response = await _client.PostAsync("/api/contacts/import", Json(new
        {
            contacts = new[]
            {
                new
                {
                    displayName = "Phone Target",
                    phones = new[] { "+49 123 456 789" },
                    emails = Array.Empty<string>()
                }
            }
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ImportContactsResultDto>();
        result.Should().NotBeNull();

        result!.Matches.Should().ContainSingle();
        result.Invites.Should().BeEmpty();

        result.Matches[0].UserName.Should().Be(targetUserName);
        result.Matches[0].MatchedBy.Should().Be("phone");
        result.Matches[0].MatchedValue.Should().Be("+49123456789");
        result.Matches[0].SourceContactDisplayName.Should().Be("Phone Target");
    }

    [Fact]
    public async Task ImportContacts_ShouldMatchUserByPhone_WhenRegisteredPhoneWasSavedFromDirtyFormat()
    {
        var ownerEmail = $"contacts_dirty_owner_{Guid.NewGuid():N}@test.com";
        var ownerUserName = $"cdo_{Guid.NewGuid():N}";
        await Register(ownerEmail, ownerUserName, "Owner", "User");

        var digits = Random.Shared.Next(100_000_000, 999_999_999).ToString();
        var canonicalPhone = $"+49{digits}";
        var dirtyPhoneForRegister = $"+49 {digits[..3]} {digits[3..6]} {digits[6..]}";
        var dirtyPhoneForImport = $"+49 ({digits[..3]}) {digits[3..6]}-{digits[6..]}";

        var targetEmail = $"contacts_dirty_target_{Guid.NewGuid():N}@test.com";
        var targetUserName = $"cdt_{Guid.NewGuid():N}";
        await Register(
            targetEmail,
            targetUserName,
            "Dirty",
            "Phone",
            phoneNumber: dirtyPhoneForRegister);

        var token = await Login(ownerEmail);
        SetAuth(_client, token);

        var response = await _client.PostAsync("/api/contacts/import", Json(new
        {
            contacts = new[]
            {
            new
            {
                displayName = "Dirty Phone Contact",
                phones = new[] { dirtyPhoneForImport },
                emails = Array.Empty<string>()
            }
        }
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ImportContactsResultDto>();
        result.Should().NotBeNull();

        result!.Matches.Should().ContainSingle();
        result.Invites.Should().BeEmpty();

        result.Matches[0].UserName.Should().Be(targetUserName);
        result.Matches[0].MatchedBy.Should().Be("phone");
        result.Matches[0].MatchedValue.Should().Be(canonicalPhone);
        result.Matches[0].SourceContactDisplayName.Should().Be("Dirty Phone Contact");
    }

    [Fact]
    public async Task ImportContacts_ShouldPreferPhoneOverEmail()
    {
        var ownerEmail = $"contacts_owner2_{Guid.NewGuid():N}@test.com";
        var ownerUserName = $"contactsowner2_{Guid.NewGuid():N}";
        await Register(ownerEmail, ownerUserName, "Owner", "User");

        var targetEmail = $"contacts_target2_{Guid.NewGuid():N}@test.com";
        var targetUserName = $"contactstarget2_{Guid.NewGuid():N}";
        await Register(
            targetEmail,
            targetUserName,
            "Dual",
            "Match",
            phoneNumber: "+49 111 111 111");

        var token = await Login(ownerEmail);
        SetAuth(_client, token);

        var response = await _client.PostAsync("/api/contacts/import", Json(new
        {
            contacts = new[]
            {
                new
                {
                    displayName = "Dual Match",
                    phones = new[] { "+49 111 111 111" },
                    emails = new[] { targetEmail }
                }
            }
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ImportContactsResultDto>();
        result.Should().NotBeNull();

        result!.Matches.Should().ContainSingle();
        result.Invites.Should().BeEmpty();

        result.Matches[0].UserName.Should().Be(targetUserName);
        result.Matches[0].MatchedBy.Should().Be("phone");
        result.Matches[0].MatchedValue.Should().Be("+49111111111");
    }

    [Fact]
    public async Task ImportContacts_ShouldNotReturnUser_WhenDiscoverableByContactsIsDisabled()
    {
        var ownerEmail = $"contacts_owner3_{Guid.NewGuid():N}@test.com";
        var ownerUserName = $"contactsowner3_{Guid.NewGuid():N}";
        await Register(ownerEmail, ownerUserName, "Owner", "User");

        var targetEmail = $"contacts_target3_{Guid.NewGuid():N}@test.com";
        var targetUserName = $"contactstarget3_{Guid.NewGuid():N}";
        await Register(
            targetEmail,
            targetUserName,
            "Hidden",
            "User",
            phoneNumber: "+49 222 222 222");

        var targetToken = await Login(targetEmail);
        SetAuth(_client, targetToken);

        var privacyResponse = await _client.PatchAsync("/api/privacy", Json(new
        {
            allowFriendRequestsFrom = 1,
            allowGroupInvitesFrom = 1,
            discoverableByContacts = false,
            discoverableBySearch = true
        }));
        privacyResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ownerToken = await Login(ownerEmail);
        SetAuth(_client, ownerToken);

        var response = await _client.PostAsync("/api/contacts/import", Json(new
        {
            contacts = new[]
            {
                new
                {
                    displayName = "Hidden User",
                    phones = new[] { "+49 222 222 222" },
                    emails = Array.Empty<string>()
                }
            }
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ImportContactsResultDto>();
        result.Should().NotBeNull();

        result!.Matches.Should().BeEmpty();
        result.Invites.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportContacts_ShouldNotReturnBlockedUser()
    {
        var ownerEmail = $"contacts_owner4_{Guid.NewGuid():N}@test.com";
        var ownerUserName = $"contactsowner4_{Guid.NewGuid():N}";
        var owner = await Register(ownerEmail, ownerUserName, "Owner", "User");

        var targetEmail = $"contacts_target4_{Guid.NewGuid():N}@test.com";
        var targetUserName = $"contactstarget4_{Guid.NewGuid():N}";
        var target = await Register(
            targetEmail,
            targetUserName,
            "Blocked",
            "User",
            phoneNumber: "+49 333 333 333");

        var ownerToken = await Login(ownerEmail);
        SetAuth(_client, ownerToken);

        var blockResponse = await _client.PostAsync($"/api/blocks/{target.userId}", null);
        blockResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.PostAsync("/api/contacts/import", Json(new
        {
            contacts = new[]
            {
                new
                {
                    displayName = "Blocked User",
                    phones = new[] { "+49 333 333 333" },
                    emails = Array.Empty<string>()
                }
            }
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ImportContactsResultDto>();
        result.Should().NotBeNull();

        result!.Matches.Should().BeEmpty();
        result.Invites.Should().BeEmpty();
    }

    [Fact]
    public async Task Import_ShouldReturn_Matches_And_Invites_Together()
    {
        var importer = await Register(
            $"importer_mix_{Guid.NewGuid():N}@test.com",
            $"importer_mix_{Guid.NewGuid():N}");

        var visible = await Register(
            $"visible_mix_{Guid.NewGuid():N}@test.com",
            $"visible_mix_{Guid.NewGuid():N}",
            phoneNumber: "+49 555 000 111");

        var importerToken = await Login(importer.email);
        SetAuth(_client, importerToken);

        var importResponse = await _client.PostAsync("/api/contacts/import", Json(new
        {
            contacts = new object[]
            {
                new
                {
                    displayName = "Known Contact",
                    phones = new[] { "+49 555 000 111" },
                    emails = Array.Empty<string>()
                },
                new
                {
                    displayName = "Unknown Contact",
                    phones = new[] { "+49 999 888 777" },
                    emails = new[] { "unknown@test.com" }
                }
            }
        }));

        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await importResponse.Content.ReadFromJsonAsync<ImportContactsResultDto>();
        result.Should().NotBeNull();

        result!.Matches.Should().ContainSingle();
        result.Invites.Should().ContainSingle();

        result.Matches[0].UserId.Should().Be(visible.userId);
        result.Invites[0].DisplayName.Should().Be("Unknown Contact");
        result.Invites[0].Phone.Should().Be("+49999888777");
        result.Invites[0].Email.Should().Be("unknown@test.com");
    }

    private async Task<(string email, Guid userId, string userName)> Register(
        string email,
        string userName,
        string firstName = "Test",
        string lastName = "User",
        string? phoneNumber = null)
    {
        var response = await _client.PostAsync("/api/auth/register", Json(new
        {
            email,
            password = "Pass123!",
            firstName,
            lastName,
            userName,
            phoneNumber
        }));

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();

        return (email, auth!.userId, userName);
    }

    private async Task<string> Login(string email)
    {
        var response = await _client.PostAsync("/api/auth/login", Json(new
        {
            email,
            password = "Pass123!",
            deviceName = "tests",
            platform = "tests"
        }));

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();

        return auth!.accessToken;
    }

    private sealed record AuthResponse(
        Guid userId,
        string accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc);

    private sealed record ContactMatchDto(
        Guid UserId,
        string UserName,
        string FirstName,
        string LastName,
        string? ProfilePhotoUrl,
        string MatchedBy,
        string? MatchedValue,
        string? SourceContactDisplayName,
        bool AlreadyFriend,
        bool HasIncomingRequest,
        bool HasOutgoingRequest,
        bool CanSendFriendRequest
    );

    private sealed record ContactInviteCandidateDto(
        string? DisplayName,
        string? Phone,
        string? Email
    );

    private sealed record ImportContactsResultDto(
        IReadOnlyList<ContactMatchDto> Matches,
        IReadOnlyList<ContactInviteCandidateDto> Invites
    );
}