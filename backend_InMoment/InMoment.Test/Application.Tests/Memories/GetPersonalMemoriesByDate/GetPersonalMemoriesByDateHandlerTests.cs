using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Memories.GetPersonalMemoriesByDate;
using InMoment.Domain.Common;
using InMoment.Domain.Media;

namespace InMoment.Application.Tests.Memories.GetPersonalMemoriesByDate;

public sealed class GetPersonalMemoriesByDateHandlerTests
{
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Mock<ICurrentUser> _current = new();

    private GetPersonalMemoriesByDateHandler Create()
        => new(_photos.Object, _storage.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new GetPersonalMemoriesByDateQuery(new DateOnly(2026, 4, 1)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public async Task Handle_ShouldReturnPhotosOrderedByCreatedAtDesc()
    {
        var currentUserId = Guid.NewGuid();
        var groupAId = Guid.NewGuid();
        var groupBId = Guid.NewGuid();
        var date = new DateOnly(2026, 4, 1);

        var first = Photo.Create(groupAId, currentUserId, "photos/a.jpg", "image/jpeg", 100);
        await Task.Delay(5);
        var second = Photo.Create(groupBId, currentUserId, "photos/b.jpg", "image/jpeg", 100);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _photos.Setup(x => x.GetByUserAndDateRangeAsync(
                currentUserId,
                date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });

        _storage.Setup(x => x.GetPublicUrl("photos/a.jpg")).Returns("https://cdn.example.com/photos/a.jpg");
        _storage.Setup(x => x.GetPublicUrl("photos/b.jpg")).Returns("https://cdn.example.com/photos/b.jpg");

        var handler = Create();

        var result = await handler.Handle(
            new GetPersonalMemoriesByDateQuery(date),
            CancellationToken.None);

        result.Date.Should().Be(date);
        result.Items.Should().HaveCount(2);

        result.Items[0].PhotoId.Should().Be(second.Id);
        result.Items[0].GroupId.Should().Be(groupBId);
        result.Items[0].PhotoUrl.Should().Be("https://cdn.example.com/photos/b.jpg");

        result.Items[1].PhotoId.Should().Be(first.Id);
        result.Items[1].GroupId.Should().Be(groupAId);
        result.Items[1].PhotoUrl.Should().Be("https://cdn.example.com/photos/a.jpg");
    }
}