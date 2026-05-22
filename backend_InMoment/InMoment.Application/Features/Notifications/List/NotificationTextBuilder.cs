using InMoment.Domain.Notifications;

namespace InMoment.Application.Features.Notifications.List;

internal static class NotificationTextBuilder
{
    public static string Build(
        NotificationType type,
        string? actorDisplayName,
        string? groupName,
        int aggregationCount)
    {
        var actor = string.IsNullOrWhiteSpace(actorDisplayName)
            ? "Кто-то"
            : actorDisplayName.Trim();

        var group = string.IsNullOrWhiteSpace(groupName)
            ? "вас"
            : $"«{groupName.Trim()}»";

        var count = aggregationCount < 1 ? 1 : aggregationCount;

        return type switch
        {
            NotificationType.GroupInvitationReceived
                => $"Вас пригласили в группу {group}",

            NotificationType.ReactionOnPhoto when count == 1
                => $"{actor} отреагировал(а) на ваше фото",

            NotificationType.ReactionOnPhoto
                => $"{actor} и ещё {count - 1} отреагировали на ваше фото",

            NotificationType.CommentOnPhoto when count == 1
                => $"{actor} прокомментировал(а) ваше фото",

            NotificationType.CommentOnPhoto
                => $"{actor} и ещё {count - 1} прокомментировали ваше фото",

            NotificationType.ReplyToComment when count == 1
                => $"{actor} ответил(а) на ваш комментарий",

            NotificationType.ReplyToComment
                => $"{actor} и ещё {count - 1} ответили на ваш комментарий",

            NotificationType.CommentMention when count == 1
                => $"{actor} упомянул(а) вас в комментарии",

            NotificationType.CommentMention
                => $"{actor} и ещё {count - 1} упомянули вас в комментариях",

            NotificationType.PhotoPublishedInGroup when count == 1
                => $"{actor} опубликовал(а) новый момент в группе {group}",

            NotificationType.PhotoPublishedInGroup
                => $"{actor} и ещё {count - 1} опубликовали новые моменты в группе {group}",

            NotificationType.ReportReviewed
                => "Жалоба рассмотрена. Результат проверки обновлён.",

            NotificationType.ReportAppealSubmitted
                => "Жалоба повторно отправлена модератору.",

            NotificationType.ModeratorAnnouncement
                => "Системное уведомление InMoment.",

            NotificationType.ReportAppealReviewed
                => "Апелляция рассмотрена. Статус жалобы обновлён.",

            NotificationType.ShareReminder
                => "Вы давно не делились моментами. Пора опубликовать что-то новое.",

            NotificationType.FeedbackPrompt
                => "Поделитесь впечатлением от InMoment — что понравилось и что можно улучшить.",

            NotificationType.Anniversary
                => "Вы с нами уже целый год. Спасибо, что делитесь моментами в InMoment.",

            NotificationType.SystemMemoryReady
                => "Мы собрали для вас новое воспоминание. Откройте его в InMoment.",

            NotificationType.ProductAnnouncement
                => "Мы выпустили обновление InMoment. Загляните в приложение и посмотрите, что изменилось.",

            _ => "У вас новое уведомление"
        };
    }
}