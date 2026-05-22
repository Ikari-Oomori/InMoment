using FluentAssertions;
using InMoment.Domain.Contacts;
using InMoment.Infrastructure.Persistence.Repositories;
using InMoment.Test.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class ContactImportLogRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistContactImportLog()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;
        var repo = new ContactImportLogRepository(db);

        var entity = ContactImportLog.Create(
            userId: Guid.NewGuid(),
            contactsSubmitted: 15,
            matchesFound: 4);

        await repo.AddAsync(entity, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.ContactImportLogs.FirstOrDefaultAsync(x => x.Id == entity.Id);

        saved.Should().NotBeNull();
        saved!.UserId.Should().Be(entity.UserId);
        saved.ContactsSubmitted.Should().Be(15);
        saved.MatchesFound.Should().Be(4);
    }
}