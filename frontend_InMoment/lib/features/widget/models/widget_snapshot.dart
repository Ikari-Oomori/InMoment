class WidgetSnapshot {
  final String? activeGroupId;
  final String? activeGroupName;
  final String? activeGroupAvatarUrl;
  final String? latestPhotoId;
  final String? latestPhotoUrl;
  final DateTime? latestPhotoCreatedAt;
  final String? latestContentType;
  final int newReactionsCount;

  const WidgetSnapshot({
    required this.activeGroupId,
    required this.activeGroupName,
    required this.activeGroupAvatarUrl,
    required this.latestPhotoId,
    required this.latestPhotoUrl,
    required this.latestPhotoCreatedAt,
    required this.latestContentType,
    required this.newReactionsCount,
  });

  factory WidgetSnapshot.fromJson(Map<String, dynamic> json) {
    return WidgetSnapshot(
      activeGroupId: json['activeGroupId']?.toString(),
      activeGroupName: json['activeGroupName']?.toString(),
      activeGroupAvatarUrl: json['activeGroupAvatarUrl']?.toString(),
      latestPhotoId: json['latestPhotoId']?.toString(),
      latestPhotoUrl: json['latestPhotoUrl']?.toString(),
      latestPhotoCreatedAt: json['latestPhotoCreatedAt'] == null
          ? null
          : DateTime.tryParse(json['latestPhotoCreatedAt'].toString()),
      latestContentType: json['latestContentType']?.toString(),
      newReactionsCount: (json['newReactionsCount'] as num?)?.toInt() ?? 0,
    );
  }

  bool get hasActiveGroup =>
      activeGroupId != null && activeGroupId!.trim().isNotEmpty;

  bool get hasPhoto =>
      latestPhotoId != null &&
      latestPhotoId!.trim().isNotEmpty &&
      latestPhotoUrl != null &&
      latestPhotoUrl!.trim().isNotEmpty;
}