class FeedVideoController {
  static String? _activeVideoId;

  static String? get activeVideoId => _activeVideoId;

  static void setActive(String id) {
    _activeVideoId = id;
  }

  static bool isActive(String id) {
    return _activeVideoId == id;
  }

  static void clear() {
    _activeVideoId = null;
  }
}