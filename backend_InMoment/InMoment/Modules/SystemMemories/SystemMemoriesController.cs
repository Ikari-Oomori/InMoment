using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InMoment.API.Modules.SystemMemories;

[ApiController]
[Authorize]
[Route("api/system-memories")]
public sealed class SystemMemoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _storage;

    public SystemMemoriesController(
        AppDbContext db,
        ICurrentUser currentUser,
        IFileStorage storage)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SystemMemoryDto>>> List(CancellationToken ct)
    {
        EnsureAuthenticated();

        var memories = await _db.SystemMemories
            .AsNoTracking()
            .Where(x => x.UserId == _currentUser.UserId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(30)
            .ToListAsync(ct);

        var result = await MapAsync(memories, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SystemMemoryDto>> Get(Guid id, CancellationToken ct)
    {
        EnsureAuthenticated();

        var memory = await _db.SystemMemories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == _currentUser.UserId, ct);

        if (memory is null)
            throw new NotFoundException("Воспоминание не найдено.");

        var result = await MapAsync(new[] { memory }, ct);
        return Ok(result[0]);
    }

    [HttpPost("{id:guid}/viewed")]
    public async Task<IActionResult> MarkViewed(Guid id, CancellationToken ct)
    {
        EnsureAuthenticated();

        var memory = await _db.SystemMemories
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == _currentUser.UserId, ct);

        if (memory is null)
            throw new NotFoundException("Воспоминание не найдено.");

        memory.MarkViewed(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<List<SystemMemoryDto>> MapAsync(
        IReadOnlyCollection<Domain.SystemMemories.SystemMemory> memories,
        CancellationToken ct)
    {
        var sourceIds = memories
            .SelectMany(x => x.GetSourcePhotoIds())
            .Distinct()
            .ToList();

        var photos = sourceIds.Count == 0
            ? new Dictionary<Guid, Photo>()
            : await _db.Photos
                .AsNoTracking()
                .Where(x => sourceIds.Contains(x.Id) && !x.IsDeleted)
                .ToDictionaryAsync(x => x.Id, ct);

        return memories.Select(memory =>
        {
            var items = memory.GetSourcePhotoIds()
                .Select(id => photos.TryGetValue(id, out var photo) ? photo : null)
                .Where(photo => photo is not null)
                .Select(photo => new SystemMemoryMediaDto(
                    photo!.Id,
                    _storage.GetPublicUrl(photo.StorageKey),
                    photo.ContentType,
                    photo.Caption,
                    photo.CreatedAt))
                .ToList();

            string? videoUrl = null;
            if (!string.IsNullOrWhiteSpace(memory.GeneratedVideoStorageKey))
                videoUrl = _storage.GetPublicUrl(memory.GeneratedVideoStorageKey!);

            return new SystemMemoryDto(
                memory.Id,
                (int)memory.Period,
                memory.Title,
                memory.Subtitle,
                memory.PeriodStartedAtUtc,
                memory.PeriodEndedAtUtc,
                memory.CreatedAtUtc,
                memory.ViewedAtUtc,
                videoUrl,
                memory.GeneratedVideoContentType,
                items.Count,
                items);
        }).ToList();
    }

    private void EnsureAuthenticated()
    {
        if (_currentUser.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");
    }
}

public sealed record SystemMemoryDto(
    Guid Id,
    int PeriodMonths,
    string Title,
    string Subtitle,
    DateTime PeriodStartedAtUtc,
    DateTime PeriodEndedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? ViewedAtUtc,
    string? GeneratedVideoUrl,
    string? GeneratedVideoContentType,
    int ItemsCount,
    IReadOnlyList<SystemMemoryMediaDto> Items);

public sealed record SystemMemoryMediaDto(
    Guid PhotoId,
    string Url,
    string ContentType,
    string? Caption,
    DateTime CreatedAt);
