import 'dart:async';

import 'package:device_info_plus/device_info_plus.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter/services.dart';
import 'package:package_info_plus/package_info_plus.dart';

import '../../../core/controllers/app_session_controller.dart';
import '../../../firebase_options.dart';
import '../api/notifications_api.dart';
import 'notification_navigation.dart';

@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  final initialized = await PushNotificationService.ensureFirebaseInitialized();

  if (!initialized) {
    return;
  }

  await PushNotificationService.instance.ensureInitialized();
  await PushNotificationService.instance.showForegroundNotification(message);
}

class PushNotificationService {
  PushNotificationService._();

  static final PushNotificationService instance = PushNotificationService._();

  final FlutterLocalNotificationsPlugin _localNotifications =
      FlutterLocalNotificationsPlugin();

  static const MethodChannel _badgeChannel = MethodChannel('inmoment/badge');

  final NotificationsApi _api = NotificationsApi();
  final DeviceInfoPlugin _deviceInfo = DeviceInfoPlugin();

  FirebaseMessaging? _messaging;

  bool _initialized = false;
  bool _firebaseUnavailable = false;

  StreamSubscription<String>? _tokenRefreshSubscription;
  StreamSubscription<RemoteMessage>? _onMessageSubscription;
  StreamSubscription<RemoteMessage>? _onMessageOpenedSubscription;

  static Future<bool> ensureFirebaseInitialized() async {
    if (Firebase.apps.isNotEmpty) {
      return true;
    }

    try {
      await Firebase.initializeApp(
        options: DefaultFirebaseOptions.currentPlatform,
      );

      return true;
    } catch (error, stack) {
      if (kDebugMode) {
        debugPrint('Firebase initialization skipped: $error');
        debugPrintStack(stackTrace: stack);
      }

      return false;
    }
  }

  FirebaseMessaging? get _safeFirebaseMessaging {
    return _messaging;
  }

  Future<void> ensureInitialized() async {
    if (_initialized || _firebaseUnavailable || kIsWeb || !_isMobilePlatform) {
      return;
    }

    final firebaseReady = await ensureFirebaseInitialized();
    if (!firebaseReady) {
      _firebaseUnavailable = true;
      return;
    }

    try {
      _messaging = FirebaseMessaging.instance;

      const androidChannel = AndroidNotificationChannel(
        'inmoment_default',
        'InMoment',
        description: 'Основные уведомления приложения InMoment',
        importance: Importance.max,
      );

      await _localNotifications
          .resolvePlatformSpecificImplementation<
              AndroidFlutterLocalNotificationsPlugin>()
          ?.createNotificationChannel(androidChannel);

      const initializationSettings = InitializationSettings(
        android: AndroidInitializationSettings('@mipmap/ic_launcher'),
        iOS: DarwinInitializationSettings(),
        macOS: DarwinInitializationSettings(),
      );

      await _localNotifications.initialize(
        initializationSettings,
        onDidReceiveNotificationResponse: (response) async {
          final payload = response.payload;
          if (payload == null || payload.trim().isEmpty) return;

          final data = <String, dynamic>{};

          for (final pair in payload.split('&')) {
            final parts = pair.split('=');
            if (parts.length != 2) continue;

            data[Uri.decodeComponent(parts[0])] =
                Uri.decodeComponent(parts[1]);
          }

          await _markNotificationReadIfPossible(data);
          await NotificationNavigation.openFromPayload(data);
        },
      );

      _onMessageSubscription = FirebaseMessaging.onMessage.listen(
        (message) async {
          await showForegroundNotification(message);
        },
      );

      _onMessageOpenedSubscription =
          FirebaseMessaging.onMessageOpenedApp.listen(
        (message) async {
          await _handleMessageTap(message);
        },
      );

      final initialMessage = await _safeFirebaseMessaging?.getInitialMessage();
      if (initialMessage != null) {
        Future.microtask(() => _handleMessageTap(initialMessage));
      }

      _tokenRefreshSubscription =
          _safeFirebaseMessaging?.onTokenRefresh.listen((token) async {
        await _registerTokenOnBackend(token);
      });

      _initialized = true;
    } catch (error, stack) {
      _firebaseUnavailable = true;
      _messaging = null;

      if (kDebugMode) {
        debugPrint('Push notifications initialization skipped: $error');
        debugPrintStack(stackTrace: stack);
      }
    }
  }

  Future<void> requestPermissionAndRegisterCurrentDevice() async {
    if (kIsWeb || !_isMobilePlatform) return;
    if (!AppSessionController.instance.isAuthenticated) return;

    await ensureInitialized();

    final messaging = _safeFirebaseMessaging;
    if (messaging == null) return;

    try {
      final settings = await messaging.requestPermission(
        alert: true,
        badge: true,
        sound: true,
        provisional: false,
      );

      final authorized =
          settings.authorizationStatus == AuthorizationStatus.authorized ||
              settings.authorizationStatus == AuthorizationStatus.provisional;

      if (!authorized) {
        return;
      }

      if (defaultTargetPlatform == TargetPlatform.iOS) {
        await messaging.getAPNSToken();
      }

      final token = await messaging.getToken();
      if (token == null || token.trim().isEmpty) {
        return;
      }

      await _registerTokenOnBackend(token);
    } catch (error, stack) {
      if (kDebugMode) {
        debugPrint('Push token registration skipped: $error');
        debugPrintStack(stackTrace: stack);
      }
    }
  }

  Future<void> unregisterCurrentDevice() async {
    if (kIsWeb || !_isMobilePlatform) return;

    try {
      await ensureInitialized();

      final messaging = _safeFirebaseMessaging;
      if (messaging == null) return;

      final currentToken = await messaging.getToken();
      if (currentToken == null || currentToken.trim().isEmpty) {
        return;
      }

      final devices = await _api.getMyDevices();
      final match = devices.cast<dynamic>().firstWhere(
            (x) => x != null && x.token.trim() == currentToken.trim(),
            orElse: () => null,
          );

      if (match == null) {
        return;
      }

      await _api.revokeDevice(match.id as String);
    } catch (_) {
      // silent by design
    }
  }

  Future<void> updateAppIconBadge(int count) async {
    if (kIsWeb || !_isMobilePlatform) return;

    final normalized = count < 0 ? 0 : count;

    try {
      await _badgeChannel.invokeMethod<void>(
        'setBadge',
        <String, Object>{'count': normalized},
      );
    } catch (_) {
      // Android launcher badge support is vendor-specific.
    }
  }

 Future<void> showForegroundNotification(RemoteMessage message) async {
    if (kIsWeb || !_isMobilePlatform) return;

    final data = message.data;

    String normalize(String? value) => (value ?? '').trim();

    final title = normalize(message.notification?.title).isNotEmpty
        ? normalize(message.notification?.title)
        : normalize(data['title']?.toString()).isNotEmpty
            ? normalize(data['title']?.toString())
            : 'InMoment';

    final body = normalize(message.notification?.body).isNotEmpty
        ? normalize(message.notification?.body)
        : normalize(data['body']?.toString()).isNotEmpty
            ? normalize(data['body']?.toString())
            : _fallbackBodyForType(data['type']?.toString());

    final payload = _encodePayload(data);

    const androidDetails = AndroidNotificationDetails(
      'inmoment_default',
      'InMoment',
      channelDescription: 'Основные уведомления приложения InMoment',
      importance: Importance.max,
      priority: Priority.high,
      playSound: true,
      enableVibration: true,
      visibility: NotificationVisibility.public,
      category: AndroidNotificationCategory.message,
    );

    const notificationDetails = NotificationDetails(
      android: androidDetails,
      iOS: DarwinNotificationDetails(
        presentAlert: true,
        presentBadge: true,
        presentSound: true,
      ),
      macOS: DarwinNotificationDetails(),
    );

    try {
      await _localNotifications.show(
        DateTime.now().millisecondsSinceEpoch.remainder(2147483647),
        title,
        body,
        notificationDetails,
        payload: payload,
      );
    } catch (error, stack) {
      if (kDebugMode) {
        debugPrint('Foreground local notification failed: $error');
        debugPrintStack(stackTrace: stack);
        debugPrint('Foreground message data: $data');
      }
    }
  }

  String _fallbackBodyForType(String? rawType) {
    switch ((rawType ?? '').trim()) {
      case '1':
      case 'PhotoPublished':
        return 'Новая публикация в группе';
      case '2':
      case 'ReactionOnPhoto':
        return 'Новая реакция на публикацию';
      case '3':
      case 'CommentOnPhoto':
        return 'Новый комментарий к публикации';
      case '4':
      case 'GroupInvitation':
        return 'Новое приглашение в группу';
      case '5':
      case 'SystemAnnouncement':
        return 'Новое системное уведомление';
      default:
        return 'Новое уведомление';
    }
  }

  Future<void> dispose() async {
    await _tokenRefreshSubscription?.cancel();
    await _onMessageSubscription?.cancel();
    await _onMessageOpenedSubscription?.cancel();

    _tokenRefreshSubscription = null;
    _onMessageSubscription = null;
    _onMessageOpenedSubscription = null;
    _messaging = null;
    _initialized = false;
    _firebaseUnavailable = false;
  }

  Future<void> _handleMessageTap(RemoteMessage message) async {
    final data = Map<String, dynamic>.from(message.data);

    await _markNotificationReadIfPossible(data);

    if (data.isEmpty) {
      await NotificationNavigation.openFromPayload(const {});
      return;
    }

    await NotificationNavigation.openFromPayload(data);
  }

  Future<void> _markNotificationReadIfPossible(Map<String, dynamic> data) async {
    final rawId = data['notificationId']?.toString().trim();
    if (rawId == null || rawId.isEmpty) return;

    try {
      await _api.markRead(rawId);
    } catch (_) {
      // silent by design
    }
  }

  Future<void> _registerTokenOnBackend(String token) async {
    try {
      await _api.registerDevice(
        token: token,
        platform: _resolvePlatform(),
        provider: 2,
        deviceName: await _buildDeviceName(),
      );
    } catch (_) {
      // silent by design
    }
  }

  int _resolvePlatform() {
    if (kIsWeb) return 3;

    if (defaultTargetPlatform == TargetPlatform.iOS ||
        defaultTargetPlatform == TargetPlatform.macOS) {
      return 1;
    }

    return 2;
  }

  Future<String> _buildDeviceName() async {
    final package = await PackageInfo.fromPlatform();

    try {
      if (defaultTargetPlatform == TargetPlatform.android) {
        final info = await _deviceInfo.androidInfo;
        final model = '${info.manufacturer} ${info.model}'.trim();
        return 'Android • $model • ${package.version}';
      }

      if (defaultTargetPlatform == TargetPlatform.iOS) {
        final info = await _deviceInfo.iosInfo;
        final model = info.utsname.machine.trim().isNotEmpty
            ? info.utsname.machine.trim()
            : info.model;
        return 'iPhone • $model • ${package.version}';
      }

      if (defaultTargetPlatform == TargetPlatform.macOS) {
        final info = await _deviceInfo.macOsInfo;
        return 'macOS • ${info.model} • ${package.version}';
      }
    } catch (_) {}

    return 'InMoment device • ${package.version}';
  }

  bool get _isMobilePlatform {
    return defaultTargetPlatform == TargetPlatform.android ||
        defaultTargetPlatform == TargetPlatform.iOS;
  }

  String _encodePayload(Map<String, dynamic> data) {
    final parts = <String>[];

    data.forEach((key, value) {
      parts.add(
        '${Uri.encodeComponent(key)}=${Uri.encodeComponent(value.toString())}',
      );
    });

    return parts.join('&');
  }
}