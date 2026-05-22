using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Memories.GetPersonalStats;
using InMoment.Domain.Common;

namespace InMoment.Application.Tests.Memories.GetPersonalStats;

public sealed class GetPersonalMemoriesStatsHandlerTests
{
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<ICurrentUser> _current = new();

    private GetPersonalMemoriesStatsHandler Create()
        => new(_photos.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new GetPersonalMemoriesStatsQuery(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public async Task Handle_ShouldReturnZeroStats_WhenNoPostingDates()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetPostingDatesByUserAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DateOnly>());
        _photos.Setup(x => x.CountByUserAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = Create();

        var result = await handler.Handle(
            new GetPersonalMemoriesStatsQuery(),
            CancellationToken.None);

        result.TotalPhotos.Should().Be(0);
        result.ActiveDays.Should().Be(0);
        result.CurrentStreakDays.Should().Be(0);
        result.LongestStreakDays.Should().Be(0);
        result.LastPostDateUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldCalculateStats_WhenPostingDatesExist()
    {
        var currentUserId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var postingDates = new[]
        {
            today.AddDays(-5),
            today.AddDays(-4),
            today.AddDays(-3),
            today.AddDays(-1),
            today
        };

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetPostingDatesByUserAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(postingDates);
        _photos.Setup(x => x.CountByUserAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(21);

        var handler = Create();

        var result = await handler.Handle(
            new GetPersonalMemoriesStatsQuery(),
            CancellationToken.None);

        result.TotalPhotos.Should().Be(21);
        result.ActiveDays.Should().Be(5);
        result.CurrentStreakDays.Should().Be(2);
        result.LongestStreakDays.Should().Be(3);
        result.LastPostDateUtc.Should().Be(today);
    }
}