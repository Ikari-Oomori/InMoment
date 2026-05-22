using FluentAssertions;
using InMoment.Domain.Privacy;
using InMoment.Infrastructure.Persistence.Repositories;
using InMoment.Test.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class PrivacySettingsRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistPrivacySettings()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;
        var repo = new PrivacySettingsRepository(db);

        var entity = PrivacySettings.CreateDefault(Guid.NewGuid());

        await repo.AddAsync(entity, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.PrivacySettings.FirstOrDefaultAsync(x => x.Id == entity.Id);

        saved.Should().NotBeNull();
        saved!.UserId.Should().Be(entity.UserId);
        saved.AllowFriendRequestsFrom.Should().Be(entity.AllowFriendRequestsFrom);
        saved.AllowGroupInvitesFrom.Should().Be(entity.AllowGroupInvitesFrom);
        saved.DiscoverableByContacts.Should().Be(entity.DiscoverableByContacts);
        saved.DiscoverableBySearch.Should().Be(entity.DiscoverableBySearch);
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnSettings_WhenExists()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var entity = PrivacySettings.CreateDefault(Guid.NewGuid());
        entity.Update(
            PrivacyAudience.Nobody,
            PrivacyAudience.FriendsOnly,
            discoverableByContacts: false,
            discoverableBySearch: true);

        db.PrivacySettings.Add(entity);
        await db.SaveChangesAsync();

        var repo = new PrivacySettingsRepository(db);

        var result = await repo.GetByUserIdAsync(entity.UserId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(entity.UserId);
        result.AllowFriendRequestsFrom.Should().Be(PrivacyAudience.Nobody);
        result.AllowGroupInvitesFrom.Should().Be(PrivacyAudience.FriendsOnly);
        result.DiscoverableByContacts.Should().BeFalse();
        result.DiscoverableBySearch.Should().BeTrue();
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnNull_WhenMissing()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var repo = new PrivacySettingsRepository(testDb.DbContext);

        var result = await repo.GetByUserIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }
}