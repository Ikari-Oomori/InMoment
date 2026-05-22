class AccountDataSummary {
  final String userId;
  final bool isActive;
  final int groupsCount;
  final int ownedGroupsCount;
  final int photosCount;
  final int commentsCount;
  final int reactionsCount;
  final int friendshipsCount;
  final int activeSessionsCount;

  const AccountDataSummary({
    required this.userId,
    required this.isActive,
    required this.groupsCount,
    required this.ownedGroupsCount,
    required this.photosCount,
    required this.commentsCount,
    required this.reactionsCount,
    required this.friendshipsCount,
    required this.activeSessionsCount,
  });

  factory AccountDataSummary.fromJson(Map<String, dynamic> json) {
    int readInt(String key) => (json[key] as num?)?.toInt() ?? 0;

    return AccountDataSummary(
      userId: (json['userId'] ?? '').toString(),
      isActive: json['isActive'] as bool? ?? true,
      groupsCount: readInt('groupsCount'),
      ownedGroupsCount: readInt('ownedGroupsCount'),
      photosCount: readInt('photosCount'),
      commentsCount: readInt('commentsCount'),
      reactionsCount: readInt('reactionsCount'),
      friendshipsCount: readInt('friendshipsCount'),
      activeSessionsCount: readInt('activeSessionsCount'),
    );
  }
}