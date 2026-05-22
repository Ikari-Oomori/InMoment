using InMoment.Domain.Common;

namespace InMoment.Domain.SystemAnnouncements;

public sealed class SystemAnnouncement : Entity<Guid>
{
    public Guid CreatedByUserId { get; private set; }
    public string Text { get; private set; } = default!;
    public string? MediaUrl { get; private set; }
    public string? MediaContentType { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private SystemAnnouncement() { }

    public static SystemAnnouncement Create(
        Guid createdByUserId,
        string text,
        string? mediaUrl,
        string? mediaContentType)
    {
        if (createdByUserId == Guid.Empty)
            throw new ValidationException("Пользователь не авторизован.");

        var announcement = new SystemAnnouncement
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        announcement.SetContent(text, mediaUrl, mediaContentType);
        return announcement;
    }

    public bool CanEdit(DateTime nowUtc)
        => nowUtc <= CreatedAtUtc.AddHours(3);

    public void Update(
        string text,
        string? mediaUrl,
        string? mediaContentType,
        DateTime nowUtc)
    {
        if (!CanEdit(nowUtc))
            throw new ValidationException("Редактирование доступно только в течение 3 часов после отправки.");

        SetContent(text, mediaUrl, mediaContentType);
        UpdatedAtUtc = nowUtc;
    }

    private void SetContent(string text, string? mediaUrl, string? mediaContentType)
    {
        var normalizedText = (text ?? string.Empty).Trim();

        if (normalizedText.Length == 0)
            throw new ValidationException("Текст уведомления обязателен.");

        if (normalizedText.Length > 2000)
            throw new ValidationException("Текст уведомления слишком длинный.");

        Text = normalizedText;
        MediaUrl = string.IsNullOrWhiteSpace(mediaUrl) ? null : mediaUrl.Trim();
        MediaContentType = string.IsNullOrWhiteSpace(mediaContentType)
            ? null
            : mediaContentType.Trim();

        if (MediaUrl is null && MediaContentType is not null)
            throw new ValidationException("Для медиа необходимо указать ссылку.");
    }
}