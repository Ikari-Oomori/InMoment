import 'package:device_info_plus/device_info_plus.dart';
import 'package:flutter/foundation.dart';

class DeviceProfile {
  static bool? _cachedLowEnd;

  static Future<bool> isLowEndDevice() async {
    if (_cachedLowEnd != null) {
      return _cachedLowEnd!;
    }

    if (kIsWeb) {
      _cachedLowEnd = true;
      return true;
    }

    if (defaultTargetPlatform == TargetPlatform.android) {
      final info = await DeviceInfoPlugin().androidInfo;
      final sdk = info.version.sdkInt;
      _cachedLowEnd = sdk <= 28;
      return _cachedLowEnd!;
    }

    _cachedLowEnd = false;
    return false;
  }

  static void resetCache() {
    _cachedLowEnd = null;
  }
}