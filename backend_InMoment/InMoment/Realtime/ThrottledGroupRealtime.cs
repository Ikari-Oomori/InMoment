using System.Collections.Concurrent;
using System.Threading.Channels;
using InMoment.Application.Abstractions.Realtime;

namespace InMoment.API.Realtime;
public sealed class ThrottledGroupRealtime : IGroupRealtime, IDisposable
{
    private readonly IGroupRealtime _inner;

    private readonly TimeSpan _debounceWindow = TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan _maxDelay = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<Guid, PendingGroupEvent> _pending = new();
    private readonly Channel<Guid> _flushQueue = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    public ThrottledGroupRealtime(IGroupRealtime inner)
    {
        _inner = inner;
        _worker = Task.Run(WorkerLoop);
    }

    public Task NotifyFeedChangedAsync(Guid groupId, string reason, Guid? photoId, CancellationToken ct)
    {
        var p = _pending.AddOrUpdate(
            groupId,
            _ => PendingGroupEvent.New(reason, photoId),
            (_, existing) => existing.Merge(reason, photoId)
        );

        var now = DateTimeOffset.UtcNow;
        var shouldFlush =
            (now - p.FirstSeenUtc) >= _maxDelay ||
            (now - p.LastUpdatedUtc) >= _debounceWindow;

        if (shouldFlush)
        {
            _flushQueue.Writer.TryWrite(groupId);
        }

        return Task.CompletedTask;
    }

    private async Task WorkerLoop()
    {
        try
        {
            while (await _flushQueue.Reader.WaitToReadAsync(_cts.Token))
            {
                while (_flushQueue.Reader.TryRead(out var groupId))
                {
                    if (!_pending.TryRemove(groupId, out var pending))
                        continue;

                    var (reason, photoId) = pending.SelectFinal();

                    await _inner.NotifyFeedChangedAsync(groupId, reason, photoId, _cts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _worker.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cts.Dispose();
    }

    private sealed class PendingGroupEvent
    {
        public bool PhotoPublished { get; private set; }
        public bool PhotoDeleted { get; private set; }
        public bool CommentChanged { get; private set; }
        public bool ReactionChanged { get; private set; }

        public Guid? LastPhotoId { get; private set; }

        public DateTimeOffset FirstSeenUtc { get; private set; }
        public DateTimeOffset LastUpdatedUtc { get; private set; }

        public static PendingGroupEvent New(string reason, Guid? photoId)
        {
            var p = new PendingGroupEvent
            {
                FirstSeenUtc = DateTimeOffset.UtcNow,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };
            p.Apply(reason, photoId);
            return p;
        }

        public PendingGroupEvent Merge(string reason, Guid? photoId)
        {
            Apply(reason, photoId);
            LastUpdatedUtc = DateTimeOffset.UtcNow;
            return this;
        }

        private void Apply(string reason, Guid? photoId)
        {
            switch (reason)
            {
                case "photo_published": PhotoPublished = true; break;
                case "photo_deleted": PhotoDeleted = true; break;
                case "comment_changed": CommentChanged = true; break;
                case "reaction_changed": ReactionChanged = true; break;
                default: break; 
            }

            if (photoId.HasValue)
                LastPhotoId = photoId;
        }

        public (string reason, Guid? photoId) SelectFinal()
        {
            if (PhotoDeleted) return ("photo_deleted", LastPhotoId);
            if (PhotoPublished) return ("photo_published", LastPhotoId);
            if (CommentChanged) return ("comment_changed", LastPhotoId);
            if (ReactionChanged) return ("reaction_changed", LastPhotoId);

            return ("feed_changed", LastPhotoId);
        }
    }
}