using FluentAssertions;
using InMoment.Domain.Groups;
using InMoment.Domain.Media;
using InMoment.Domain.Users;
using InMoment.Infrastructure.Queries;
using InMoment.Test.Common.Persistence;

namespace InMoment.Infrastructure.Tests.Queries;

public sealed class NotificationPreviewReaderTests
{
    [Fact]
    public async Task GetBundleAsync_ShouldReturnOnlyDistinctExistingAndNotDeletedEntities()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var actor = User.Create("actor@test.com", "hash", "actor_user", "Anna", "Petrova");
        actor.SetProfilePhoto("https://cdn.example.com/profiles/anna.jpg");

        var secondActor = User.Create("second@test.com", "hash", "second_user", "Maria", "Sidorova");

        var group = Group.Create("Family", actor.Id);

        var photo = Photo.Create(group.Id, actor.Id, "groups/family/photos/1.jpg", "image/jpeg", 100);
        var deletedPhoto = Photo.Create(group.Id, actor.Id, "groups/family/photos/deleted.jpg", "image/jpeg", 100);
        deletedPhoto.MarkDeleted(actor.Id, actor.Id);

        db.Users.AddRange(actor, secondActor);
        db.Groups.Add(group);
        db.Photos.AddRange(photo, deletedPhoto);

        await db.SaveChangesAsync();

        var reader = new NotificationPreviewReader(db);

        var result = await reader.GetBundleAsync(
            actorUserIds: new[] { actor.Id, actor.Id, secondActor.Id, Guid.Empty, Guid.NewGuid() },
            groupIds: new[] { group.Id, group.Id, Guid.Empty, Guid.NewGuid() },
            photoIds: new[] { photo.Id, photo.Id, deletedPhoto.Id, Guid.Empty, Guid.NewGuid() },
            CancellationToken.None);

        result.Actors.Should().HaveCount(2);
        result.Groups.Should().HaveCount(1);
        result.Photos.Should().HaveCount(1);

        result.Actors[actor.Id].DisplayName.Should().Be("Anna Petrova");
        result.Actors[actor.Id].ProfilePhotoUrl.Should().Be("https://cdn.example.com/profiles/anna.jpg");

        result.Actors[secondActor.Id].DisplayName.Should().Be("Maria Sidorova");
        result.Actors[secondActor.Id].ProfilePhotoUrl.Should().BeNull();

        result.Groups[group.Id].Name.Should().Be("Family");

        result.Photos[photo.Id].StorageKey.Should().Be("groups/family/photos/1.jpg");
        result.Photos.ContainsKey(deletedPhoto.Id).Should().BeFalse();
    }

    [Fact]
    public async Task GetBundleAsync_ShouldReturnEmptyDictionaries_WhenNothingMatches()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var reader = new NotificationPreviewReader(testDb.DbContext);

        var result = await reader.GetBundleAsync(
            actorUserIds: new[] { Guid.Empty, Guid.NewGuid() },
            groupIds: new[] { Guid.Empty, Guid.NewGuid() },
            photoIds: new[] { Guid.Empty, Guid.NewGuid() },
            CancellationToken.None);

        result.Actors.Should().BeEmpty();
        result.Groups.Should().BeEmpty();
        result.Photos.Should().BeEmpty();
    }
}