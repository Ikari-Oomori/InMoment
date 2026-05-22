class SystemAnnouncement {
  final String id;
  final String text;
  final String? mediaUrl;
  final String? mediaContentType;
  final DateTime createdAtUtc;
  final DateTime? updatedAtUtc;
  final bool canEdit;

  const SystemAnnouncement({
    required this.id,
    required this.text,
    required this.mediaUrl,
    required this.mediaContentType,
    required this.createdAtUtc,
    required this.updatedAtUtc,
    required this.canEdit,
  });

  factory SystemAnnouncement.fromJson(Map<String, dynamic> json) {
    return SystemAnnouncement(
      id: (json['id'] ?? '').toString(),
      text: (json['text'] ?? '').toString(),
      mediaUrl: json['mediaUrl']?.toString(),
      mediaContentType: json['mediaContentType']?.toString(),
      createdAtUtc: DateTime.parse(json['createdAtUtc'].toString()),
      updatedAtUtc: json['updatedAtUtc'] == null
          ? null
          : DateTime.parse(json['updatedAtUtc'].toString()),
      canEdit: json['canEdit'] as bool? ?? false,
    );
  }
}