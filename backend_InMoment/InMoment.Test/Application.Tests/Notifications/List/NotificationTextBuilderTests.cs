using InMoment.Application.Features.Notifications.List;
using InMoment.Domain.Notifications;

namespace InMoment.Tests.Application.Tests.Notifications.List;

public sealed class NotificationTextBuilderTests
{
    [Fact]
    public void Build_ShouldUseFallbackActor_WhenActorMissing()
    {
        var result = NotificationTextBuilder.Build(
            NotificationType.ReactionOnPhoto,
            actorDisplayName: "   ",
            groupName: null,
            aggregationCount: 1);

        result.Should().Be("Кто-то отреагировал(а) на ваше фото");
    }

    [Fact]
    public void Build_ShouldUseFallbackGroup_WhenGroupMissing()
    {
        var result = NotificationTextBuilder.Build(
            NotificationType.GroupInvitationReceived,
            actorDisplayName: null,
            groupName: "   ",
            aggregationCount: 1);

        result.Should().Be("Вас пригласили в группу вас");
    }

    [Fact]
    public void Build_ShouldTrimActorAndGroupNames()
    {
        var result = NotificationTextBuilder.Build(
            NotificationType.GroupInvitationReceived,
            actorDisplayName: "  Анна  ",
            groupName: "  Семья  ",
            aggregationCount: 1);

        result.Should().Be("Вас пригласили в группу «Семья»");
    }

    [Fact]
    public void Build_ShouldNormalizeAggregationCount_ToOne_WhenLessThanOne()
    {
        var result = NotificationTextBuilder.Build(
            NotificationType.CommentOnPhoto,
            actorDisplayName: "Анна",
            groupName: null,
            aggregationCount: 0);

        result.Should().Be("Анна прокомментировал(а) ваше фото");
    }

    [Fact]
    public void Build_ShouldBuildSingleReactionText()
    {
        var result = NotificationTextBuilder.Build(
            NotificationType.ReactionOnPhoto,
            actorDisplayName: "Анна",
            groupName: null,
            aggregationCount: 1);

        result.Should().Be("Анна отреагировал(а) на ваше фото");
    }

    [Fact]
    public void Build_ShouldBuildAggregatedReactionText()
    {
        var result = NotificationTextBuilder.Build(
            NotificationType.ReactionOnPhoto,
            actorDisplayName: "Анна",
            groupName: null,
            aggregationCount: 3);

        result.Should().Be("Анна и ещё 2 отреагировали на ваше фото");
    }

    [Fact]
    public void Build_ShouldBuildSingleCommentText()
    {
        var result = NotificationTextBuilder.Build(
            NotificationType.CommentOnPhoto,
            actorDisplayName: "Анна",
            groupName: null,
            aggregationCount: 1);

        result.Should().Be("Анна прокомментировал(а) ваше фото");
    }

    [Fact]
    public void Build_ShouldBuildAggregatedCommentText()
    {
        var result = NotificationTextBuilder.Build(
            NotificationType.CommentOnPhoto,
            actorDisplayName: "Анна",
            groupName: null,
            aggregationCount: 4);

        result.Should().Be("Анна и ещё 3 прокомментировали ваше фото");
    }

    [Fact]
    public void Build_ShouldBuildSingleReplyText()
    {
        var result = NotificationTextBuilder.Build(
            NotificationType.ReplyToComment,
            actorDisplayName: "Анна",
            groupName: null,
            aggregationCount: 1);

        result.Should().Be("Анна ответил(а) на ваш комментарий");
    }

    [Fact]
    public void Build_ShouldBuildAggregatedReplyText()
    {
        var result = NotificationTextBuilder.Build(
            NotificationType.ReplyToComment,
            actorDisplayName: "Анна",
            groupName: null,
            aggregationCount: 2);

        result.Should().Be("Анна и ещё 1 ответили на ваш комментарий");
    }

    [Fact]
    public void Build_ShouldBuildSingleMentionText()
    {
        var result = NotificationTextBuilder.Build(
            NotificationType.CommentMention,
            actorDisplayName: "Анна",
            groupName: null,
            aggregationCount: 1);

        result.Should().Be("Анна упомянул(а) вас в комментарии");
    }

    [Fact]
    public void Build_ShouldBuildAggregatedMentionText()
    {
        var result = NotificationTextBuilder.Build(
            NotificationType.CommentMention,
            actorDisplayName: "Анна",
            groupName: null,
            aggregationCount: 5);

        result.Should().Be("Анна и ещё 4 упомянули вас в комментариях");
    }

    [Fact]
    public void Build_ShouldReturnDefaultText_ForUnknownType()
    {
        var result = NotificationTextBuilder.Build(
            (NotificationType)999,
            actorDisplayName: "Анна",
            groupName: "Семья",
            aggregationCount: 1);

        result.Should().Be("У вас новое уведомление");
    }
}