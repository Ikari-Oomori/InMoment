import 'package:flutter/foundation.dart';

import '../../../core/api/api_error.dart';
import '../api/notifications_api.dart';
import '../models/notification_item.dart';
import '../services/push_notification_service.dart';

class NotificationsController extends ChangeNotifier {
  NotificationsController._();

  static final NotificationsController instance = NotificationsController._();

  static const Duration _maxNotificationAge = Duration(days: 30);

  final NotificationsApi _api = NotificationsApi();

  final List<NotificationItem> _items = [];
  bool _loading = false;
  bool _loadingMore = false;
  bool _markingAllRead = false;
  bool _refreshingFromRealtime = false;
  String? _error;
  String? _nextCursor;
  int _unreadCount = 0;
  bool _initialized = false;

  List<NotificationItem> get items => List.unmodifiable(_items);
  bool get loading => _loading;
  bool get loadingMore => _loadingMore;
  bool get markingAllRead => _markingAllRead;
  String? get error => _error;
  String? get nextCursor => _nextCursor;
  int get unreadCount => _unreadCount;
  bool get hasMore => _nextCursor != null && _nextCursor!.trim().isNotEmpty;
  bool get initialized => _initialized;

  Future<void> loadInitial({bool force = false}) async {
    if (_loading) return;
    if (_initialized && !force) return;

    if (force) {
      _items.clear();
      _nextCursor = null;
      _unreadCount = 0;
      _syncAppIconBadge();
      _error = null;
      _initialized = false;
    }

    _loading = true;
    _error = null;
    notifyListeners();

    try {
      final result = await _api.getNotifications(limit: 20);

      final filteredItems = _pruneExpired(result.items);

      _items
        ..clear()
        ..addAll(filteredItems);

      _nextCursor = result.nextCursor;
      _unreadCount = _countUnread(filteredItems, fallback: result.unreadCount);
      _syncAppIconBadge();
      _initialized = true;
    } catch (e) {
      _error = _normalizeError(e);
    } finally {
      _loading = false;
      notifyListeners();
    }
  }

  Future<void> refresh() async {
    try {
      final result = await _api.getNotifications(limit: 20);
      final filteredItems = _pruneExpired(result.items);

      _items
        ..clear()
        ..addAll(filteredItems);

      _nextCursor = result.nextCursor;
      _unreadCount = _countUnread(filteredItems, fallback: result.unreadCount);
      _syncAppIconBadge();
      _error = null;
      _initialized = true;
    } catch (e) {
      _error = _normalizeError(e);
    } finally {
      notifyListeners();
    }
  }

  Future<void> loadMore() async {
    if (_loadingMore || !hasMore) return;

    _loadingMore = true;
    notifyListeners();

    try {
      final result = await _api.getNotifications(
        limit: 20,
        cursor: _nextCursor,
      );

      final filteredItems = _pruneExpired(result.items);
      _items.addAll(filteredItems);
      _nextCursor = result.nextCursor;
      _unreadCount = _countUnread(_items, fallback: result.unreadCount);
      _syncAppIconBadge();
      _error = null;
    } catch (e) {
      _error = _normalizeError(e);
    } finally {
      _loadingMore = false;
      notifyListeners();
    }
  }

  Future<void> reloadUnreadCount() async {
    try {
      final count = await _api.getUnreadCount();
      _unreadCount = count < 0 ? 0 : count;
      _syncAppIconBadge();
      notifyListeners();
    } catch (_) {}
  }

  Future<void> applyRealtimeUnreadCount(int value) async {
    final normalized = value < 0 ? 0 : value;
    final changed = _unreadCount != normalized;

    _unreadCount = normalized;
    if (changed) {
      _syncAppIconBadge();
    }

    if (changed) {
      notifyListeners();
    }

    if (!_initialized) {
      return;
    }

    if (_refreshingFromRealtime || _loading || _loadingMore || _markingAllRead) {
      return;
    }

    _refreshingFromRealtime = true;
    try {
      await refresh();
    } finally {
      _refreshingFromRealtime = false;
    }
  }

  Future<void> markRead(String notificationId) async {
    final index = _items.indexWhere((x) => x.id == notificationId);
    if (index < 0) return;

    final item = _items[index];
    if (item.isRead) return;

    try {
      await _api.markRead(notificationId);

      _items[index] = item.copyWith(
        isRead: true,
        readAt: DateTime.now(),
      );

      if (_unreadCount > 0) {
        _unreadCount--;
      }

      _syncAppIconBadge();
      notifyListeners();
    } catch (_) {}
  }

  Future<void> markAllRead() async {
    if (_markingAllRead) return;

    _markingAllRead = true;
    notifyListeners();

    try {
      await _api.markAllRead();

      for (var i = 0; i < _items.length; i++) {
        _items[i] = _items[i].copyWith(
          isRead: true,
          readAt: DateTime.now(),
        );
      }

      _unreadCount = 0;
      _syncAppIconBadge();
      _error = null;
    } catch (e) {
      _error = _normalizeError(e);
    } finally {
      _markingAllRead = false;
      notifyListeners();
    }
  }

  void setUnreadCount(int value) {
    _unreadCount = value < 0 ? 0 : value;
    _syncAppIconBadge();
    notifyListeners();
  }

  void reset() {
    _items.clear();
    _loading = false;
    _loadingMore = false;
    _markingAllRead = false;
    _refreshingFromRealtime = false;
    _error = null;
    _nextCursor = null;
    _unreadCount = 0;
    _initialized = false;
    _syncAppIconBadge();
    notifyListeners();
  }

  void _syncAppIconBadge() {
    PushNotificationService.instance.updateAppIconBadge(_unreadCount);
  }

  List<NotificationItem> _pruneExpired(List<NotificationItem> source) {
    final now = DateTime.now();

    return source.where((item) {
      final age = now.difference(item.createdAt.toLocal());
      return age <= _maxNotificationAge;
    }).toList();
  }

  int _countUnread(List<NotificationItem> source, {required int fallback}) {
    if (source.isEmpty) return fallback < 0 ? 0 : fallback;

    final count = source.where((item) => !item.isRead).length;
    if (count < 0) return 0;
    return count;
  }

  String _normalizeError(Object error) {
    return ApiError.normalize(
      error,
      fallback: 'Не удалось загрузить уведомления. Попробуйте ещё раз.',
    );
  }
}