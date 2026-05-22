import 'package:device_info_plus/device_info_plus.dart';
import 'package:flutter/foundation.dart';

class DeviceProfile {
  static bool? _cachedLowEnd;
  static bool? _cachedAllowEmbeddedVideo;

  static Future<bool> isLowEndDevice() async {
    if (_cachedLowEnd != null) return _cachedLowEnd!;

    if (kIsWeb) {
      _cachedLowEnd = true;
      return true;
    }

    if (defaultTargetPlatform != TargetPlatform.android) {
      _cachedLowEnd = false;
      return false;
    }

    try {
      final info = await DeviceInfoPlugin().androidInfo;
      final sdk = info.version.sdkInt;
      final manufacturer = info.manufacturer.toLowerCase();
      final model = info.model.toLowerCase();

      final isOldAndroid = sdk <= 28;

      final isKnownWeakHuawei =
          manufacturer.contains('huawei') ||
          manufacturer.contains('honor') ||
          model.contains('huawei') ||
          model.contains('honor') ||
          model.contains('bnd');

      final isBudgetVendor =
          manufacturer.contains('xiaomi') ||
          manufacturer.contains('redmi') ||
          manufacturer.contains('oppo') ||
          manufacturer.contains('realme') ||
          manufacturer.contains('vivo') ||
          manufacturer.contains('tecno') ||
          manufacturer.contains('infinix');

      _cachedLowEnd = isOldAndroid || (sdk <= 29 && isKnownWeakHuawei);

      if (isBudgetVendor && sdk <= 28) {
        _cachedLowEnd = true;
      }

      return _cachedLowEnd!;
    } catch (_) {
      _cachedLowEnd = true;
      return true;
    }
  }

  static Future<bool> allowEmbeddedVideoPlayback() async {
    if (_cachedAllowEmbeddedVideo != null) {
      return _cachedAllowEmbeddedVideo!;
    }

    _cachedAllowEmbeddedVideo = true;
    return true;
  }

  static Future<bool> shouldReduceMotionAndMediaLoad() async {
    return isLowEndDevice();
  }

  static bool get allowAutoplay => false;

  static void resetCache() {
    _cachedLowEnd = null;
    _cachedAllowEmbeddedVideo = null;
  }
}