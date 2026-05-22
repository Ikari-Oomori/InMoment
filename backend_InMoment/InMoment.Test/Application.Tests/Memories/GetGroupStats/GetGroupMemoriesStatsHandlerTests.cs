using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Memories.GetGroupStats;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;

namespace InMoment.Application.Tests.Memories.GetGroupStats;

public sealed class GetGroupMemoriesStatsHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<ICurrentUser> _current = new();

    private GetGroupMemoriesStatsHandler Create()
        => new(_groups.Object, _photos.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenGroupIdEmpty()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupMemoriesStatsQuery(Guid.Empty),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("GroupId is required.");
    }

    [Fact]
    public async Task Handle_ShouldReturnZeroStats_WhenNoPostingDates()
    {
        var ownerId = Guid.NewGuid();
        var group = Group.Create("Family", ownerId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _photos.Setup(x => x.GetPostingDatesByGroupAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DateOnly>());
        _photos.Setup(x => x.CountByGroupAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupMemoriesStatsQuery(group.Id),
            CancellationToken.None);

        result.GroupId.Should().Be(group.Id);
        result.TotalPhotos.Should().Be(0);
        result.ActiveDays.Should().Be(0);
        result.CurrentStreakDays.Should().Be(0);
        result.LongestStreakDays.Should().Be(0);
        result.LastPostDateUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldCalculateStats_WhenPostingDatesExist()
    {
        var ownerId = Guid.NewGuid();
        var group = Group.Create("Family", ownerId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var postingDates = new[]
        {
            today.AddDays(-4),
            today.AddDays(-3),
            today.AddDays(-1),
            today
        };

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _photos.Setup(x => x.GetPostingDatesByGroupAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(postingDates);
        _photos.Setup(x => x.CountByGroupAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupMemoriesStatsQuery(group.Id),
            CancellationToken.None);

        result.GroupId.Should().Be(group.Id);
        result.TotalPhotos.Should().Be(12);
        result.ActiveDays.Should().Be(4);
        result.CurrentStreakDays.Should().Be(2);
        result.LongestStreakDays.Should().Be(2);
        result.LastPostDateUtc.Should().Be(today);
    }
}