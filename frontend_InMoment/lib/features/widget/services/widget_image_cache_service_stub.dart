class WidgetImageCacheResult {
  final String? cachedPath;
  final String? sourceUrl;

  const WidgetImageCacheResult({
    required this.cachedPath,
    required this.sourceUrl,
  });
}

class WidgetImageCacheService {
  WidgetImageCacheService._();

  static final WidgetImageCacheService instance = WidgetImageCacheService._();

  Future<WidgetImageCacheResult> cachePreview({
    required String? imageUrl,
  }) async {
    final safeUrl = imageUrl?.trim();

    return WidgetImageCacheResult(
      cachedPath: null,
      sourceUrl: safeUrl == null || safeUrl.isEmpty ? null : safeUrl,
    );
  }

  Future<WidgetImageCacheResult> cacheVideoPreview({
    required String? videoUrl,
  }) async {
    final safeUrl = videoUrl?.trim();

    return WidgetImageCacheResult(
      cachedPath: null,
      sourceUrl: safeUrl == null || safeUrl.isEmpty ? null : safeUrl,
    );
  }
}