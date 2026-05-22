class PhotoReactionSummary {
  final int type;
  final int count;

  const PhotoReactionSummary({
    required this.type,
    required this.count,
  });

  factory PhotoReactionSummary.fromJson(Map<String, dynamic> json) {
    return PhotoReactionSummary(
      type: (json['type'] as num?)?.toInt() ?? 0,
      count: (json['count'] as num?)?.toInt() ?? 0,
    );
  }
}

class PhotoDetails {
  final String id;
  final String groupId;
  final String authorId;
  final String url;
  final String contentType;
  final String? caption;
  final DateTime createdAt;

  final String authorUserName;
  final String? authorFirstName;
  final String? authorLastName;
  final String? authorProfilePhotoUrl;
  final bool authorIsActive;

  final bool isMine;
  final bool canEdit;
  final bool canDelete;

  final int myReaction;
  final List<PhotoReactionSummary> reactions;
  final int commentsCount;

  const PhotoDetails({
    required this.id,
    required this.groupId,
    required this.authorId,
    required this.url,
    required this.contentType,
    required this.caption,
    required this.createdAt,
    required this.authorUserName,
    required this.authorFirstName,
    required this.authorLastName,
    required this.authorProfilePhotoUrl,
    required this.authorIsActive,
    required this.isMine,
    required this.canEdit,
    required this.canDelete,
    required this.myReaction,
    required this.reactions,
    required this.commentsCount,
  });

  bool get isVideo => contentType.toLowerCase().startsWith('video/');
  bool get isImage => contentType.toLowerCase().startsWith('image/');

  factory PhotoDetails.fromJson(Map<String, dynamic> json) {
    final reactionsRaw = json['reactions'];

    return PhotoDetails(
      id: (json['photoId'] ?? json['id'] ?? '').toString(),
      groupId: (json['groupId'] ?? '').toString(),
      authorId: (json['authorId'] ?? '').toString(),
      url: (json['url'] ?? json['photoUrl'] ?? '').toString(),
      contentType: (json['contentType'] ?? 'image/jpeg').toString(),
      caption: json['caption']?.toString(),
      createdAt: DateTime.parse(
        (json['createdAt'] ?? DateTime.now().toIso8601String()).toString(),
      ),
      authorUserName: (json['authorUserName'] ?? '').toString(),
      authorFirstName: json['authorFirstName']?.toString(),
      authorLastName: json['authorLastName']?.toString(),
      authorProfilePhotoUrl: json['authorProfilePhotoUrl']?.toString(),
      authorIsActive: json['authorIsActive'] as bool? ?? true,
      isMine: json['isMine'] as bool? ?? false,
      canEdit: json['canEdit'] as bool? ?? false,
      canDelete: json['canDelete'] as bool? ?? false,
      myReaction: (json['myReaction'] as num?)?.toInt() ?? 0,
      reactions: reactionsRaw is List
          ? reactionsRaw
              .whereType<Map<String, dynamic>>()
              .map(PhotoReactionSummary.fromJson)
              .toList()
          : const [],
      commentsCount: (json['commentsCount'] as num?)?.toInt() ?? 0,
    );
  }

  PhotoDetails copyWith({
    String? id,
    String? groupId,
    String? authorId,
    String? url,
    String? contentType,
    String? caption,
    DateTime? createdAt,
    String? authorUserName,
    String? authorFirstName,
    String? authorLastName,
    String? authorProfilePhotoUrl,
    bool? authorIsActive,
    bool? isMine,
    bool? canEdit,
    bool? canDelete,
    int? myReaction,
    List<PhotoReactionSummary>? reactions,
    int? commentsCount,
  }) {
    return PhotoDetails(
      id: id ?? this.id,
      groupId: groupId ?? this.groupId,
      authorId: authorId ?? this.authorId,
      url: url ?? this.url,
      contentType: contentType ?? this.contentType,
      caption: caption ?? this.caption,
      createdAt: createdAt ?? this.createdAt,
      authorUserName: authorUserName ?? this.authorUserName,
      authorFirstName: authorFirstName ?? this.authorFirstName,
      authorLastName: authorLastName ?? this.authorLastName,
      authorProfilePhotoUrl:
          authorProfilePhotoUrl ?? this.authorProfilePhotoUrl,
      authorIsActive: authorIsActive ?? this.authorIsActive,
      isMine: isMine ?? this.isMine,
      canEdit: canEdit ?? this.canEdit,
      canDelete: canDelete ?? this.canDelete,
      myReaction: myReaction ?? this.myReaction,
      reactions: reactions ?? this.reactions,
      commentsCount: commentsCount ?? this.commentsCount,
    );
  }
}