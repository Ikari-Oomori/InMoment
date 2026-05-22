class DiscussionReactionSummary {
  final int type;
  final int count;

  const DiscussionReactionSummary({
    required this.type,
    required this.count,
  });

  factory DiscussionReactionSummary.fromJson(Map<String, dynamic> json) {
    return DiscussionReactionSummary(
      type: (json['type'] as num?)?.toInt() ?? 0,
      count: (json['count'] as num?)?.toInt() ?? 0,
    );
  }
}

class DiscussionItem {
  final String photoId;
  final String photoUrl;
  final String? photoContentType;
  final DateTime photoCreatedAt;
  final String? photoCaption;
  final String photoAuthorUserId;
  final String photoAuthorUserName;
  final String? photoAuthorProfilePhotoUrl;

  final List<DiscussionReactionSummary> reactions;
  final int reactionsCount;
  final int myReaction;

  final int commentsCount;
  final String? latestCommentText;
  final String? latestCommentUserId;
  final String? latestCommentUserName;
  final String? latestCommentUserProfilePhotoUrl;
  final DateTime? latestCommentCreatedAt;
  final DateTime lastActivityAt;

  DiscussionItem({
    required this.photoId,
    required this.photoUrl,
    required this.photoContentType,
    required this.photoCreatedAt,
    required this.photoCaption,
    required this.photoAuthorUserId,
    required this.photoAuthorUserName,
    required this.photoAuthorProfilePhotoUrl,
    required this.reactions,
    required this.reactionsCount,
    required this.myReaction,
    required this.commentsCount,
    required this.latestCommentText,
    required this.latestCommentUserId,
    required this.latestCommentUserName,
    required this.latestCommentUserProfilePhotoUrl,
    required this.latestCommentCreatedAt,
    required this.lastActivityAt,
  });

  factory DiscussionItem.fromJson(Map<String, dynamic> json) {
    final reactionsRaw = json['reactions'];

    final rawContentType =
        json['photoContentType'] ??
        json['contentType'] ??
        json['photoMimeType'] ??
        json['mimeType'];

    return DiscussionItem(
      photoId: json['photoId'] as String,
      photoUrl: json['photoUrl'] as String,
      photoContentType: rawContentType is String && rawContentType.trim().isNotEmpty
          ? rawContentType.trim()
          : null,
      photoCreatedAt: DateTime.parse(json['photoCreatedAt'] as String),
      photoCaption: json['photoCaption'] as String?,
      photoAuthorUserId: json['photoAuthorUserId'] as String,
      photoAuthorUserName: json['photoAuthorUserName'] as String,
      photoAuthorProfilePhotoUrl: json['photoAuthorProfilePhotoUrl'] as String?,
      reactions: reactionsRaw is List
          ? reactionsRaw
              .whereType<Map<String, dynamic>>()
              .map(DiscussionReactionSummary.fromJson)
              .toList()
          : const [],
      reactionsCount: (json['reactionsCount'] as num?)?.toInt() ?? 0,
      myReaction: (json['myReaction'] as num?)?.toInt() ?? 0,
      commentsCount: json['commentsCount'] as int? ?? 0,
      latestCommentText: json['latestCommentText'] as String?,
      latestCommentUserId: json['latestCommentUserId'] as String?,
      latestCommentUserName: json['latestCommentUserName'] as String?,
      latestCommentUserProfilePhotoUrl:
          json['latestCommentUserProfilePhotoUrl'] as String?,
      latestCommentCreatedAt: json['latestCommentCreatedAt'] != null
          ? DateTime.parse(json['latestCommentCreatedAt'] as String)
          : null,
      lastActivityAt: DateTime.parse(json['lastActivityAt'] as String),
    );
  }

  DiscussionItem copyWith({
    String? photoContentType,
    List<DiscussionReactionSummary>? reactions,
    int? reactionsCount,
    int? myReaction,
  }) {
    return DiscussionItem(
      photoId: photoId,
      photoUrl: photoUrl,
      photoContentType: photoContentType ?? this.photoContentType,
      photoCreatedAt: photoCreatedAt,
      photoCaption: photoCaption,
      photoAuthorUserId: photoAuthorUserId,
      photoAuthorUserName: photoAuthorUserName,
      photoAuthorProfilePhotoUrl: photoAuthorProfilePhotoUrl,
      reactions: reactions ?? this.reactions,
      reactionsCount: reactionsCount ?? this.reactionsCount,
      myReaction: myReaction ?? this.myReaction,
      commentsCount: commentsCount,
      latestCommentText: latestCommentText,
      latestCommentUserId: latestCommentUserId,
      latestCommentUserName: latestCommentUserName,
      latestCommentUserProfilePhotoUrl: latestCommentUserProfilePhotoUrl,
      latestCommentCreatedAt: latestCommentCreatedAt,
      lastActivityAt: lastActivityAt,
    );
  }
}