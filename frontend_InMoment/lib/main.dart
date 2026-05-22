import 'dart:async';

import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import 'app/app.dart';
import 'core/config/env.dart';
import 'features/notifications/services/push_notification_service.dart';

Future<void> main() async {
  await runZonedGuarded(() async {
    WidgetsFlutterBinding.ensureInitialized();

    Env.validate();
    Env.printResolvedConfig();

    FlutterError.onError = (details) {
      FlutterError.presentError(details);
    };

    PlatformDispatcher.instance.onError = (error, stack) {
      if (kDebugMode) {
        debugPrint('Uncaught platform error: $error');
        debugPrintStack(stackTrace: stack);
      }
      return true;
    };

    if (!kIsWeb &&
        (defaultTargetPlatform == TargetPlatform.android ||
            defaultTargetPlatform == TargetPlatform.iOS)) {
      final firebaseReady =
          await PushNotificationService.ensureFirebaseInitialized();

      if (firebaseReady) {
        FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);
        await PushNotificationService.instance.ensureInitialized();
      }
    }

    runApp(const InMomentApp());
  }, (error, stack) {
    if (kDebugMode) {
      debugPrint('Uncaught zone error: $error');
      debugPrintStack(stackTrace: stack);
    }
  });
}