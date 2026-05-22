class NotificationItem {
  final String id;
  final int type;
  final String? actorUserId;
  final String? actorDisplayName;
  final String? actorUserName;
  final String? actorProfilePhotoUrl;
  final String? groupId;
  final String? groupName;
  final String? groupAvatarUrl;
  final String? photoId;
  final String? photoUrl;
  final String? thumbnailUrl;
  final String? photoCaption;
  final String? commentId;
  final String? invitationId;
  final String? systemMemoryId;
  final String? systemAnnouncementId;
  final String? announcementText;
  final String? announcementMediaUrl;
  final String? announcementMediaContentType;
  final bool isRead;
  final int aggregationCount;
  final String previewText;
  final int targetType;
  final String? targetId;
  final String? targetRoute;
  final bool isClickable;
  final String createdAtHumanized;
  final DateTime createdAt;
  final DateTime? readAt;

  const NotificationItem({
    required this.id,
    required this.type,
    required this.actorUserId,
    required this.actorDisplayName,
    required this.actorUserName,
    required this.actorProfilePhotoUrl,
    required this.groupId,
    required this.groupName,
    required this.groupAvatarUrl,
    required this.photoId,
    required this.photoUrl,
    required this.thumbnailUrl,
    required this.photoCaption,
    required this.commentId,
    required this.invitationId,
    required this.systemMemoryId,
    required this.systemAnnouncementId,
    required this.announcementText,
    required this.announcementMediaUrl,
    required this.announcementMediaContentType,
    required this.isRead,
    required this.aggregationCount,
    required this.previewText,
    required this.targetType,
    required this.targetId,
    required this.targetRoute,
    required this.isClickable,
    required this.createdAtHumanized,
    required this.createdAt,
    required this.readAt,
  });

  factory NotificationItem.fromJson(Map<String, dynamic> json) {
    return NotificationItem(
      id: (json['id'] ?? '').toString(),
      type: (json['type'] as num?)?.toInt() ?? 0,
      actorUserId: json['actorUserId']?.toString(),
      actorDisplayName: json['actorDisplayName']?.toString(),
      actorUserName: json['actorUserName']?.toString(),
      actorProfilePhotoUrl: json['actorProfilePhotoUrl']?.toString(),
      groupId: json['groupId']?.toString(),
      groupName: json['groupName']?.toString(),
      groupAvatarUrl: json['groupAvatarUrl']?.toString(),
      photoId: json['photoId']?.toString(),
      photoUrl: json['photoUrl']?.toString(),
      thumbnailUrl: json['thumbnailUrl']?.toString(),
      photoCaption: json['photoCaption']?.toString(),
      commentId: json['commentId']?.toString(),
      invitationId: json['invitationId']?.toString(),
      systemMemoryId: json['systemMemoryId']?.toString(),
      systemAnnouncementId: json['systemAnnouncementId']?.toString(),
      announcementText: json['announcementText']?.toString(),
      announcementMediaUrl: json['announcementMediaUrl']?.toString(),
      announcementMediaContentType: json['announcementMediaContentType']?.toString(),
      isRead: json['isRead'] as bool? ?? false,
      aggregationCount: (json['aggregationCount'] as num?)?.toInt() ?? 1,
      previewText: (json['previewText'] ?? '').toString(),
      targetType: (json['targetType'] as num?)?.toInt() ?? 0,
      targetId: json['targetId']?.toString(),
      targetRoute: json['targetRoute']?.toString(),
      isClickable: json['isClickable'] as bool? ?? false,
      createdAtHumanized: (json['createdAtHumanized'] ?? '').toString(),
      createdAt: DateTime.parse(
        (json['createdAt'] ?? DateTime.now().toIso8601String()).toString(),
      ),
      readAt: json['readAt'] != null
          ? DateTime.parse(json['readAt'].toString())
          : null,
    );
  }

  NotificationItem copyWith({
    bool? isRead,
    DateTime? readAt,
  }) {
    return NotificationItem(
      id: id,
      type: type,
      actorUserId: actorUserId,
      actorDisplayName: actorDisplayName,
      actorUserName: actorUserName,
      actorProfilePhotoUrl: actorProfilePhotoUrl,
      groupId: groupId,
      groupName: groupName,
      groupAvatarUrl: groupAvatarUrl,
      photoId: photoId,
      photoUrl: photoUrl,
      thumbnailUrl: thumbnailUrl,
      photoCaption: photoCaption,
      commentId: commentId,
      invitationId: invitationId,
      systemMemoryId: systemMemoryId,
      systemAnnouncementId: systemAnnouncementId,
      announcementText: announcementText,
      announcementMediaUrl: announcementMediaUrl,
      announcementMediaContentType: announcementMediaContentType,
      isRead: isRead ?? this.isRead,
      aggregationCount: aggregationCount,
      previewText: previewText,
      targetType: targetType,
      targetId: targetId,
      targetRoute: targetRoute,
      isClickable: isClickable,
      createdAtHumanized: createdAtHumanized,
      createdAt: createdAt,
      readAt: readAt ?? this.readAt,
    );
  }
}

class NotificationsPageResult {
  final List<NotificationItem> items;
  final String? nextCursor;
  final int unreadCount;

  const NotificationsPageResult({
    required this.items,
    required this.nextCursor,
    required this.unreadCount,
  });

  factory NotificationsPageResult.fromJson(Map<String, dynamic> json) {
    final rawItems = json['items'];

    return NotificationsPageResult(
      items: rawItems is List
          ? rawItems
              .whereType<Map<String, dynamic>>()
              .map(NotificationItem.fromJson)
              .toList()
          : const [],
      nextCursor: json['nextCursor']?.toString(),
      unreadCount: (json['unreadCount'] as num?)?.toInt() ?? 0,
    );
  }
}

class UnreadNotificationsCount {
  final int count;

  const UnreadNotificationsCount({
    required this.count,
  });

  factory UnreadNotificationsCount.fromJson(Map<String, dynamic> json) {
    return UnreadNotificationsCount(
      count: (json['count'] as num?)?.toInt() ?? 0,
    );
  }
}