class GifSearchItem {
  final String id;
  final String title;
  final String previewUrl;
  final String gifUrl;

  const GifSearchItem({
    required this.id,
    required this.title,
    required this.previewUrl,
    required this.gifUrl,
  });

  factory GifSearchItem.fromJson(Map<String, dynamic> json) {
    return GifSearchItem(
      id: json['id'] as String? ?? '',
      title: json['title'] as String? ?? 'GIF',
      previewUrl: json['previewUrl'] as String? ?? '',
      gifUrl: json['gifUrl'] as String? ?? '',
    );
  }
}