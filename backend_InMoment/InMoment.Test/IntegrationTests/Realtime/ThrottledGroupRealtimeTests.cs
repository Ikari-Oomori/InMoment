using FluentAssertions;
using InMoment.API.Realtime;
using InMoment.Application.Abstractions.Realtime;
using Moq;

namespace InMoment.Tests.IntegrationTests.Realtime;

public sealed class ThrottledGroupRealtimeTests
{
    [Fact]
    public async Task NotifyFeedChangedAsync_ShouldFlushAfterMaxDelay_AndPreferPhotoDeleted()
    {
        var groupId = Guid.NewGuid();
        var firstPhotoId = Guid.NewGuid();
        var finalPhotoId = Guid.NewGuid();

        var delivered = new TaskCompletionSource<(Guid GroupId, string Reason, Guid? PhotoId)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var inner = new Mock<IGroupRealtime>();
        inner.Setup(x => x.NotifyFeedChangedAsync(groupId, "photo_deleted", finalPhotoId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => delivered.TrySetResult((groupId, "photo_deleted", finalPhotoId)));

        using var throttled = new ThrottledGroupRealtime(inner.Object);

        await throttled.NotifyFeedChangedAsync(groupId, "reaction_changed", firstPhotoId, CancellationToken.None);

        await Task.Delay(2100);

        await throttled.NotifyFeedChangedAsync(groupId, "photo_deleted", finalPhotoId, CancellationToken.None);

        var completed = await Task.WhenAny(delivered.Task, Task.Delay(2000));
        completed.Should().Be(delivered.Task);

        var result = await delivered.Task;
        result.GroupId.Should().Be(groupId);
        result.Reason.Should().Be("photo_deleted");
        result.PhotoId.Should().Be(finalPhotoId);

        inner.Verify(
            x => x.NotifyFeedChangedAsync(groupId, "photo_deleted", finalPhotoId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyFeedChangedAsync_ShouldUseFeedChanged_ForUnknownReason()
    {
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        var delivered = new TaskCompletionSource<(Guid GroupId, string Reason, Guid? PhotoId)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var inner = new Mock<IGroupRealtime>();
        inner.Setup(x => x.NotifyFeedChangedAsync(groupId, "feed_changed", photoId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => delivered.TrySetResult((groupId, "feed_changed", photoId)));

        using var throttled = new ThrottledGroupRealtime(inner.Object);

        await throttled.NotifyFeedChangedAsync(groupId, "unknown_reason", photoId, CancellationToken.None);

        await Task.Delay(2100);

        await throttled.NotifyFeedChangedAsync(groupId, "unknown_reason", photoId, CancellationToken.None);

        var completed = await Task.WhenAny(delivered.Task, Task.Delay(2000));
        completed.Should().Be(delivered.Task);

        var result = await delivered.Task;
        result.GroupId.Should().Be(groupId);
        result.Reason.Should().Be("feed_changed");
        result.PhotoId.Should().Be(photoId);

        inner.Verify(
            x => x.NotifyFeedChangedAsync(groupId, "feed_changed", photoId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        var inner = new Mock<IGroupRealtime>();

        var throttled = new ThrottledGroupRealtime(inner.Object);

        var act = () => throttled.Dispose();

        act.Should().NotThrow();
    }
}