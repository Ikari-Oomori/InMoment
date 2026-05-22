class FeedReactionSummary {
  final int type;
  final int count;

  const FeedReactionSummary({
    required this.type,
    required this.count,
  });

  factory FeedReactionSummary.fromJson(Map<String, dynamic> json) {
    return FeedReactionSummary(
      type: (json['type'] as num?)?.toInt() ?? 0,
      count: (json['count'] as num?)?.toInt() ?? 0,
    );
  }
}

class FeedItem {
  final String photoId;
  final String groupId;
  final String authorId;
  final String authorUserName;
  final String? authorProfilePhotoUrl;
  final String url;
  final String contentType;
  final int sizeBytes;
  final String? caption;
  final DateTime createdAt;
  final int commentsCount;
  final int reactionsCount;
  final int myReaction;
  final List<FeedReactionSummary> reactions;

  FeedItem({
    required this.photoId,
    required this.groupId,
    required this.authorId,
    required this.authorUserName,
    required this.authorProfilePhotoUrl,
    required this.url,
    required this.contentType,
    required this.sizeBytes,
    required this.caption,
    required this.createdAt,
    required this.commentsCount,
    required this.reactionsCount,
    required this.myReaction,
    required this.reactions,
  });

  bool get isVideo => contentType.toLowerCase().startsWith('video/');
  bool get isImage => contentType.toLowerCase().startsWith('image/');

  factory FeedItem.fromJson(Map<String, dynamic> json) {
    final reactionsRaw = json['reactions'];

    final parsedReactions = reactionsRaw is List
        ? reactionsRaw
            .whereType<Map<String, dynamic>>()
            .map(FeedReactionSummary.fromJson)
            .toList()
        : const <FeedReactionSummary>[];

    final explicitReactionsCount = (json['reactionsCount'] as num?)?.toInt();
    final derivedReactionsCount = parsedReactions.fold<int>(
      0,
      (sum, item) => sum + item.count,
    );

    return FeedItem(
      photoId: (json['photoId'] ?? '').toString(),
      groupId: (json['groupId'] ?? '').toString(),
      authorId: (json['authorId'] ?? '').toString(),
      authorUserName: (json['authorUserName'] ?? '').toString(),
      authorProfilePhotoUrl: json['authorProfilePhotoUrl']?.toString(),
      url: (json['url'] ?? '').toString(),
      contentType: (json['contentType'] ?? 'image/jpeg').toString(),
      sizeBytes: (json['sizeBytes'] as num?)?.toInt() ?? 0,
      caption: json['caption']?.toString(),
      createdAt: DateTime.parse(
        (json['createdAt'] ?? DateTime.now().toIso8601String()).toString(),
      ),
      commentsCount: (json['commentsCount'] as num?)?.toInt() ?? 0,
      reactionsCount: explicitReactionsCount ?? derivedReactionsCount,
      myReaction: (json['myReaction'] as num?)?.toInt() ?? 0,
      reactions: parsedReactions,
    );
  }

  FeedItem copyWith({
    String? photoId,
    String? groupId,
    String? authorId,
    String? authorUserName,
    String? authorProfilePhotoUrl,
    String? url,
    String? contentType,
    int? sizeBytes,
    String? caption,
    DateTime? createdAt,
    int? commentsCount,
    int? reactionsCount,
    int? myReaction,
    List<FeedReactionSummary>? reactions,
  }) {
    return FeedItem(
      photoId: photoId ?? this.photoId,
      groupId: groupId ?? this.groupId,
      authorId: authorId ?? this.authorId,
      authorUserName: authorUserName ?? this.authorUserName,
      authorProfilePhotoUrl: authorProfilePhotoUrl ?? this.authorProfilePhotoUrl,
      url: url ?? this.url,
      contentType: contentType ?? this.contentType,
      sizeBytes: sizeBytes ?? this.sizeBytes,
      caption: caption ?? this.caption,
      createdAt: createdAt ?? this.createdAt,
      commentsCount: commentsCount ?? this.commentsCount,
      reactionsCount: reactionsCount ?? this.reactionsCount,
      myReaction: myReaction ?? this.myReaction,
      reactions: reactions ?? this.reactions,
    );
  }
}