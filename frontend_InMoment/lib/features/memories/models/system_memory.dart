class SystemMemoryMedia {
  final String photoId;
  final String url;
  final String contentType;
  final String? caption;
  final DateTime createdAt;

  const SystemMemoryMedia({
    required this.photoId,
    required this.url,
    required this.contentType,
    required this.caption,
    required this.createdAt,
  });

  factory SystemMemoryMedia.fromJson(Map<String, dynamic> json) {
    return SystemMemoryMedia(
      photoId: (json['photoId'] ?? '').toString(),
      url: (json['url'] ?? '').toString(),
      contentType: (json['contentType'] ?? '').toString(),
      caption: json['caption']?.toString(),
      createdAt: DateTime.parse(
        (json['createdAt'] ?? DateTime.now().toIso8601String()).toString(),
      ),
    );
  }
}

class SystemMemory {
  final String id;
  final int periodMonths;
  final String title;
  final String subtitle;
  final DateTime periodStartedAtUtc;
  final DateTime periodEndedAtUtc;
  final DateTime createdAtUtc;
  final DateTime? viewedAtUtc;
  final String? generatedVideoUrl;
  final String? generatedVideoContentType;
  final int itemsCount;
  final List<SystemMemoryMedia> items;

  const SystemMemory({
    required this.id,
    required this.periodMonths,
    required this.title,
    required this.subtitle,
    required this.periodStartedAtUtc,
    required this.periodEndedAtUtc,
    required this.createdAtUtc,
    required this.viewedAtUtc,
    required this.generatedVideoUrl,
    required this.generatedVideoContentType,
    required this.itemsCount,
    required this.items,
  });

  bool get hasGeneratedVideo =>
      generatedVideoUrl != null && generatedVideoUrl!.trim().isNotEmpty;

  factory SystemMemory.fromJson(Map<String, dynamic> json) {
    final rawItems = json['items'];

    return SystemMemory(
      id: (json['id'] ?? '').toString(),
      periodMonths: (json['periodMonths'] as num?)?.toInt() ?? 0,
      title: (json['title'] ?? '').toString(),
      subtitle: (json['subtitle'] ?? '').toString(),
      periodStartedAtUtc: DateTime.parse(
        (json['periodStartedAtUtc'] ?? DateTime.now().toIso8601String()).toString(),
      ),
      periodEndedAtUtc: DateTime.parse(
        (json['periodEndedAtUtc'] ?? DateTime.now().toIso8601String()).toString(),
      ),
      createdAtUtc: DateTime.parse(
        (json['createdAtUtc'] ?? DateTime.now().toIso8601String()).toString(),
      ),
      viewedAtUtc: json['viewedAtUtc'] == null
          ? null
          : DateTime.parse(json['viewedAtUtc'].toString()),
      generatedVideoUrl: json['generatedVideoUrl']?.toString(),
      generatedVideoContentType: json['generatedVideoContentType']?.toString(),
      itemsCount: (json['itemsCount'] as num?)?.toInt() ?? 0,
      items: rawItems is List
          ? rawItems
              .whereType<Map>()
              .map((item) => SystemMemoryMedia.fromJson(
                    item.map((key, value) => MapEntry(key.toString(), value)),
                  ))
              .toList()
          : const [],
    );
  }
}
