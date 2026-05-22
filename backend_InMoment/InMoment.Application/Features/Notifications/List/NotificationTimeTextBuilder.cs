namespace InMoment.Application.Features.Notifications.List;

internal static class NotificationTimeTextBuilder
{
    public static string BuildRu(DateTime createdAtUtc, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var diff = now - createdAtUtc;

        if (diff.TotalSeconds < 0)
            diff = TimeSpan.Zero;

        if (diff.TotalSeconds < 15)
            return "только что";

        if (diff.TotalMinutes < 1)
            return "меньше минуты назад";

        var minutes = (int)Math.Floor(diff.TotalMinutes);
        if (minutes < 60)
            return $"{minutes} {PluralizeMinutes(minutes)} назад";

        var hours = (int)Math.Floor(diff.TotalHours);
        if (hours < 24)
            return $"{hours} {PluralizeHours(hours)} назад";

        var days = (int)Math.Floor(diff.TotalDays);

        if (days == 1)
            return "вчера";

        if (days < 7)
            return $"{days} {PluralizeDays(days)} назад";

        var local = createdAtUtc.ToLocalTime();
        return local.ToString("dd.MM.yyyy HH:mm");
    }

    private static string PluralizeMinutes(int value)
    {
        var n = Math.Abs(value) % 100;
        var n1 = n % 10;

        if (n > 10 && n < 20) return "минут";
        if (n1 > 1 && n1 < 5) return "минуты";
        if (n1 == 1) return "минуту";
        return "минут";
    }

    private static string PluralizeHours(int value)
    {
        var n = Math.Abs(value) % 100;
        var n1 = n % 10;

        if (n > 10 && n < 20) return "часов";
        if (n1 > 1 && n1 < 5) return "часа";
        if (n1 == 1) return "час";
        return "часов";
    }

    private static string PluralizeDays(int value)
    {
        var n = Math.Abs(value) % 100;
        var n1 = n % 10;

        if (n > 10 && n < 20) return "дней";
        if (n1 > 1 && n1 < 5) return "дня";
        if (n1 == 1) return "день";
        return "дней";
    }
}