class CommentReactionSummary {
  final int type;
  final int count;

  const CommentReactionSummary({
    required this.type,
    required this.count,
  });

  factory CommentReactionSummary.fromJson(Map<String, dynamic> json) {
    return CommentReactionSummary(
      type: (json['type'] as num?)?.toInt() ?? 0,
      count: (json['count'] as num?)?.toInt() ?? 0,
    );
  }
}

class CommentItem {
  final String id;
  final String photoId;
  final String userId;
  final String userName;
  final String? firstName;
  final String? lastName;
  final String? profilePhotoUrl;
  final bool userIsActive;
  final String? parentCommentId;
  final String? parentCommentUserId;
  final String? parentCommentUserName;
  final bool? parentCommentUserIsActive;
  final String? parentCommentTextPreview;
  final String text;
  final DateTime createdAt;
  final DateTime? editedAt;
  final bool isMine;
  final List<CommentReactionSummary> reactions;
  final int reactionsCount;
  final int myReaction;
  final String? gifUrl;

  CommentItem({
    required this.id,
    required this.photoId,
    required this.userId,
    required this.userName,
    required this.firstName,
    required this.lastName,
    required this.profilePhotoUrl,
    required this.userIsActive,
    required this.parentCommentId,
    required this.parentCommentUserId,
    required this.parentCommentUserName,
    required this.parentCommentUserIsActive,
    required this.parentCommentTextPreview,
    required this.text,
    required this.createdAt,
    required this.editedAt,
    required this.isMine,
    required this.reactions,
    required this.reactionsCount,
    required this.myReaction,
    required this.gifUrl,
  });

  factory CommentItem.fromJson(Map<String, dynamic> json) {
    final reactionsRaw = json['reactions'];

    return CommentItem(
      id: json['id'] as String,
      photoId: json['photoId'] as String,
      userId: json['userId'] as String,
      userName: json['userName'] as String? ?? '',
      firstName: json['firstName'] as String?,
      lastName: json['lastName'] as String?,
      profilePhotoUrl: json['profilePhotoUrl'] as String?,
      userIsActive: json['userIsActive'] as bool? ?? true,
      parentCommentId: json['parentCommentId'] as String?,
      parentCommentUserId: json['parentCommentUserId'] as String?,
      parentCommentUserName: json['parentCommentUserName'] as String?,
      parentCommentUserIsActive: json['parentCommentUserIsActive'] as bool?,
      parentCommentTextPreview: json['parentCommentTextPreview'] as String?,
      text: json['text'] as String,
      gifUrl: json['gifUrl'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
      editedAt: json['editedAt'] != null
          ? DateTime.parse(json['editedAt'] as String)
          : null,
      isMine: json['isMine'] as bool? ?? false,
      reactions: reactionsRaw is List
          ? reactionsRaw
              .whereType<Map<String, dynamic>>()
              .map(CommentReactionSummary.fromJson)
              .toList()
          : const [],
      reactionsCount: (json['reactionsCount'] as num?)?.toInt() ?? 0,
      myReaction: (json['myReaction'] as num?)?.toInt() ?? 0,
    );
  }
}