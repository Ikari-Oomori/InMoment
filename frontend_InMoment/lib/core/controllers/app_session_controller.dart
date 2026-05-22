import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../features/groups/controllers/active_group_controller.dart';
import '../../features/notifications/controllers/notifications_controller.dart';
import '../../features/notifications/services/push_notification_service.dart';
import '../../features/widget/services/widget_sync_service.dart';
import '../realtime/group_realtime_service.dart';
import '../realtime/users_realtime_service.dart';
import '../storage/token_storage.dart';

enum AppSessionStatus {
  unknown,
  authenticated,
  unauthenticated,
}

class AppSessionController extends ChangeNotifier {
  AppSessionController._();

  static final AppSessionController instance = AppSessionController._();

  final TokenStorage _tokenStorage = const TokenStorage();

  AppSessionStatus _status = AppSessionStatus.unknown;

  AppSessionStatus get status => _status;

  bool get isAuthenticated => _status == AppSessionStatus.authenticated;
  bool get isUnauthenticated => _status == AppSessionStatus.unauthenticated;
  bool get isUnknown => _status == AppSessionStatus.unknown;

  Future<void> bootstrap() async {
    final hasAccessToken = await _safeHasAccessToken();

    _status = hasAccessToken
        ? AppSessionStatus.authenticated
        : AppSessionStatus.unauthenticated;

    notifyListeners();

    if (!hasAccessToken) {
      await _clearRuntimeStateSafe();
      await _runOptional(
        label: 'clear widget data',
        action: WidgetSyncService.instance.clear,
      );
      return;
    }

    _startPostAuthServices();
  }

  Future<void> markAuthenticated() async {
    await _clearRuntimeStateSafe();

    _status = AppSessionStatus.authenticated;
    notifyListeners();

    _startPostAuthServices();
  }

  Future<void> logout() async {
    await _runOptional(
      label: 'unregister push device',
      action: PushNotificationService.instance.unregisterCurrentDevice,
    );

    await _tokenStorage.clear();
    await _clearRuntimeStateSafe();

    await _runOptional(
      label: 'clear widget data',
      action: WidgetSyncService.instance.clear,
    );

    _status = AppSessionStatus.unauthenticated;
    notifyListeners();
  }

  Future<bool> _safeHasAccessToken() async {
    try {
      return _tokenStorage.hasAccessToken();
    } catch (error, stackTrace) {
      debugPrint('[Session] Failed to read access token: $error');
      debugPrintStack(stackTrace: stackTrace);

      await _runOptional(
        label: 'clear token storage after failed token read',
        action: _tokenStorage.clear,
      );

      return false;
    }
  }

  void _startPostAuthServices() {
    unawaited(_runPostAuthServices());
  }

  Future<void> _runPostAuthServices() async {
    if (!isAuthenticated) return;

    if (!kIsWeb &&
        (defaultTargetPlatform == TargetPlatform.android ||
            defaultTargetPlatform == TargetPlatform.iOS)) {
      await _runOptional(
        label: 'register push device',
        action: PushNotificationService.instance
            .requestPermissionAndRegisterCurrentDevice,
      );
    }

    if (!isAuthenticated) return;

    await _runOptional(
      label: 'connect user realtime',
      action: UsersRealtimeService.instance.ensureConnected,
    );

    if (!isAuthenticated) return;

    await _runOptional(
      label: 'sync widget from backend',
      action: WidgetSyncService.instance.syncFromBackend,
    );
  }

  Future<void> _clearRuntimeStateSafe() async {
    try {
      ActiveGroupController.instance.reset();
    } catch (error, stackTrace) {
      debugPrint('[Session] Failed to reset active group state: $error');
      debugPrintStack(stackTrace: stackTrace);
    }

    try {
      NotificationsController.instance.reset();
    } catch (error, stackTrace) {
      debugPrint('[Session] Failed to reset notifications state: $error');
      debugPrintStack(stackTrace: stackTrace);
    }

    await _runOptional(
      label: 'dispose group realtime connection',
      action: GroupRealtimeService.instance.disposeConnection,
    );

    await _runOptional(
      label: 'dispose user realtime connection',
      action: UsersRealtimeService.instance.disposeConnection,
    );
  }

  Future<void> _runOptional({
    required String label,
    required Future<void> Function() action,
  }) async {
    try {
      await action();
    } catch (error, stackTrace) {
      debugPrint('[Session] Optional session task failed: $label: $error');
      debugPrintStack(stackTrace: stackTrace);
    }
  }
}