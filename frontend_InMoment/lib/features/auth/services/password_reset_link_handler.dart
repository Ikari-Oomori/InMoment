import 'dart:async';

import 'package:app_links/app_links.dart';
import 'package:flutter/material.dart';

import '../../../core/config/app_contacts.dart';
import '../../../core/navigation/app_navigator.dart';
import '../pages/reset_password_page.dart';

class PasswordResetLinkHandler {
  PasswordResetLinkHandler._();

  static final PasswordResetLinkHandler instance = PasswordResetLinkHandler._();

  final AppLinks _appLinks = AppLinks();
  StreamSubscription<Uri>? _subscription;
  bool _started = false;

  Future<void> start() async {
    if (_started) return;
    _started = true;

    try {
      final initialUri = await _appLinks.getInitialLink();
      _handleUri(initialUri);
    } catch (e) {
      debugPrint('PasswordResetLinkHandler initial link error: $e');
    }

    _subscription = _appLinks.uriLinkStream.listen(
      _handleUri,
      onError: (Object error) {
        debugPrint('PasswordResetLinkHandler stream error: $error');
      },
    );
  }

  void dispose() {
    _subscription?.cancel();
    _subscription = null;
    _started = false;
  }

  void _handleUri(Uri? uri) {
    if (uri == null) return;

    final token = _extractToken(uri);
    if (token == null || token.isEmpty) return;

    final context = appNavigatorKey.currentContext;
    if (context == null) return;

    appNavigatorKey.currentState?.push(
      MaterialPageRoute(
        builder: (_) => ResetPasswordPage(
          initialToken: token,
          openedFromLink: true,
        ),
      ),
    );
  }

  String? _extractToken(Uri uri) {
    final token = uri.queryParameters['token']?.trim();
    if (token == null || token.isEmpty) {
      return null;
    }

    final isCustomScheme =
        uri.scheme == AppContacts.passwordResetScheme &&
        uri.host == AppContacts.passwordResetHost;

    final isWebResetPath = uri.pathSegments.contains('reset-password');

    if (isCustomScheme || isWebResetPath) {
      return token;
    }

    return null;
  }
}