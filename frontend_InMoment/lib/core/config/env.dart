import 'package:flutter/foundation.dart';

import 'app_contacts.dart';
import 'app_links.dart';

enum AppFlavor {
  development,
  production,
}

class Env {
  static const String _rawBaseUrl = String.fromEnvironment(
    'INMOMENT_API_BASE_URL',
    defaultValue: 'http://localhost:5293',
  );

  static const String _rawFlavor = String.fromEnvironment(
    'INMOMENT_FLAVOR',
    defaultValue: 'development',
  );

  static const bool _allowDevelopmentRelease = bool.fromEnvironment(
    'INMOMENT_ALLOW_DEVELOPMENT_RELEASE',
    defaultValue: false,
  );

  static String get baseUrl {
    final value = _normalizeBaseUrl(_rawBaseUrl);

    if (value.isEmpty) {
      throw StateError(
        'INMOMENT_API_BASE_URL is empty. Pass it with --dart-define.',
      );
    }

    final uri = Uri.tryParse(value);

    if (uri == null || !uri.hasScheme || !uri.hasAuthority) {
      throw StateError(
        'INMOMENT_API_BASE_URL must be an absolute URL.',
      );
    }

    if (isProduction) {
      if (uri.scheme.toLowerCase() != 'https') {
        throw StateError(
          'Production INMOMENT_API_BASE_URL must use HTTPS.',
        );
      }

      if (_isLocalOrPrivateHost(uri.host)) {
        throw StateError(
          'Production INMOMENT_API_BASE_URL must not point to a local/private host.',
        );
      }
    }

    return value;
  }

  static AppFlavor get flavor {
    final value = _rawFlavor.trim().toLowerCase();

    switch (value) {
      case 'development':
      case 'dev':
        return AppFlavor.development;
      case 'production':
      case 'prod':
        return AppFlavor.production;
      default:
        throw StateError(
          'Unknown INMOMENT_FLAVOR "$_rawFlavor". Use "development" or "production".',
        );
    }
  }

  static bool get isProduction => flavor == AppFlavor.production;
  static bool get isDevelopment => flavor == AppFlavor.development;

  static Duration get connectTimeout => const Duration(seconds: 15);
  static Duration get receiveTimeout => const Duration(seconds: 15);
  static Duration get sendTimeout => const Duration(seconds: 15);

  static void validate() {
    final resolvedFlavor = flavor;
    final resolvedBaseUrl = baseUrl;

    if (kReleaseMode &&
        resolvedFlavor == AppFlavor.development &&
        !_allowDevelopmentRelease) {
      throw StateError(
        'Release build cannot use development flavor. '
        'Pass --dart-define=INMOMENT_FLAVOR=production for store builds. '
        'For local release testing only, pass '
        '--dart-define=INMOMENT_ALLOW_DEVELOPMENT_RELEASE=true.',
      );
    }

    if (resolvedFlavor == AppFlavor.development &&
        resolvedBaseUrl.startsWith('https://') &&
        !_isLocalOrPrivateHost(Uri.parse(resolvedBaseUrl).host)) {
      debugPrint(
        '[Env] Development flavor points to a public HTTPS API. '
        'Check whether INMOMENT_FLAVOR should be production.',
      );
    }

    if (resolvedFlavor == AppFlavor.production) {
      final warnings = <String>[
        ...AppContacts.productionWarnings,
        ...AppLinks.productionWarnings,
      ];

      if (warnings.isNotEmpty) {
        final message = warnings.join(' ');

        if (kReleaseMode && !_allowDevelopmentRelease) {
          throw StateError(
            'Production release configuration is incomplete. $message',
          );
        }

        debugPrint('[Env] Production configuration warning: $message');
      }
    }
  }

  static void printResolvedConfig() {
    if (!kDebugMode) return;

    debugPrint(
      '[Env] flavor=${flavor.name}, baseUrl=$baseUrl',
    );
  }

  static String _normalizeBaseUrl(String value) {
    var result = value.trim();

    while (result.endsWith('/')) {
      result = result.substring(0, result.length - 1);
    }

    return result;
  }

  static bool _isLocalOrPrivateHost(String host) {
    final normalized = host.trim().toLowerCase();

    if (normalized == 'localhost') return true;
    if (normalized == '127.0.0.1') return true;
    if (normalized == '10.0.2.2') return true;
    if (normalized.startsWith('192.168.')) return true;
    if (normalized.startsWith('10.')) return true;

    final secondPrivateRange = RegExp(r'^172\.(1[6-9]|2\d|3[0-1])\.');
    if (secondPrivateRange.hasMatch(normalized)) return true;

    return false;
  }
}