using InMoment.Application.Features.Notifications.List;
using InMoment.Domain.Notifications;
using InMoment.Application;

namespace InMoment.Tests.Application.Tests.Notifications.List;

public sealed class NotificationTargetBuilderTests
{
    [Fact]
    public void Build_ShouldReturnInvitationTarget_ForGroupInvitationReceived_WhenInvitationIdExists()
    {
        var invitationId = Guid.NewGuid();

        var result = NotificationTargetBuilder.Build(
            NotificationType.GroupInvitationReceived,
            groupId: null,
            photoId: null,
            commentId: null,
            invitationId: invitationId);

        result.targetType.Should().Be(NotificationTargetType.Invitation);
        result.targetId.Should().Be(invitationId);
        result.targetRoute.Should().Be($"/invitations/{invitationId}");
    }

    [Fact]
    public void Build_ShouldReturnPhotoTarget_ForReactionOnPhoto_WhenGroupAndPhotoExist()
    {
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        var result = NotificationTargetBuilder.Build(
            NotificationType.ReactionOnPhoto,
            groupId,
            photoId,
            commentId: null,
            invitationId: null);

        result.targetType.Should().Be(NotificationTargetType.Photo);
        result.targetId.Should().Be(photoId);
        result.targetRoute.Should().Be($"/groups/{groupId}/photos/{photoId}");
    }

    [Fact]
    public void Build_ShouldReturnPhotoTarget_ForCommentOnPhoto_WhenGroupAndPhotoExist()
    {
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        var result = NotificationTargetBuilder.Build(
            NotificationType.CommentOnPhoto,
            groupId,
            photoId,
            commentId: null,
            invitationId: null);

        result.targetType.Should().Be(NotificationTargetType.Photo);
        result.targetId.Should().Be(photoId);
        result.targetRoute.Should().Be($"/groups/{groupId}/photos/{photoId}");
    }

    [Fact]
    public void Build_ShouldReturnCommentTarget_ForReplyToComment_WhenAllIdsExist()
    {
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var commentId = Guid.NewGuid();

        var result = NotificationTargetBuilder.Build(
            NotificationType.ReplyToComment,
            groupId,
            photoId,
            commentId,
            invitationId: null);

        result.targetType.Should().Be(NotificationTargetType.Comment);
        result.targetId.Should().Be(commentId);
        result.targetRoute.Should().Be($"/groups/{groupId}/photos/{photoId}?commentId={commentId}");
    }

    [Fact]
    public void Build_ShouldReturnCommentTarget_ForCommentMention_WhenAllIdsExist()
    {
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var commentId = Guid.NewGuid();

        var result = NotificationTargetBuilder.Build(
            NotificationType.CommentMention,
            groupId,
            photoId,
            commentId,
            invitationId: null);

        result.targetType.Should().Be(NotificationTargetType.Comment);
        result.targetId.Should().Be(commentId);
        result.targetRoute.Should().Be($"/groups/{groupId}/photos/{photoId}?commentId={commentId}");
    }

    [Theory]
    [InlineData(NotificationType.GroupInvitationReceived)]
    [InlineData(NotificationType.ReactionOnPhoto)]
    [InlineData(NotificationType.CommentOnPhoto)]
    [InlineData(NotificationType.ReplyToComment)]
    [InlineData(NotificationType.CommentMention)]
    public void Build_ShouldReturnUnknown_WhenRequiredIdsMissing(NotificationType type)
    {
        var result = NotificationTargetBuilder.Build(
            type,
            groupId: null,
            photoId: null,
            commentId: null,
            invitationId: null);

        result.targetType.Should().Be(NotificationTargetType.Unknown);
        result.targetId.Should().BeNull();
        result.targetRoute.Should().BeNull();
    }
}