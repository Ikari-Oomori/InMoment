import 'dart:async';

import 'package:app_links/app_links.dart' as app_links;
import 'package:flutter/material.dart';

import '../../../core/config/app_links.dart';
import '../../../core/controllers/app_session_controller.dart';
import '../../../core/navigation/app_navigator.dart';
import '../../home/home_screen.dart';
import '../../shell/models/app_shell_tab.dart';
import '../api/group_management_api.dart';
import '../controllers/active_group_controller.dart';

class GroupInviteLinkHandler {
  GroupInviteLinkHandler._();

  static final GroupInviteLinkHandler instance = GroupInviteLinkHandler._();

  final app_links.AppLinks _appLinks = app_links.AppLinks();
  final GroupManagementApi _api = GroupManagementApi();

  StreamSubscription<Uri>? _subscription;
  String? _pendingCode;
  bool _started = false;
  bool _joining = false;

  Future<void> start() async {
    if (_started) return;
    _started = true;

    AppSessionController.instance.addListener(_tryJoinPendingCode);

    try {
      final initialUri = await _appLinks.getInitialLink();
      _handleUri(initialUri);
    } catch (e) {
      debugPrint('GroupInviteLinkHandler initial link error: $e');
    }

    _subscription = _appLinks.uriLinkStream.listen(
      _handleUri,
      onError: (Object error) {
        debugPrint('GroupInviteLinkHandler stream error: $error');
      },
    );
  }

  void dispose() {
    AppSessionController.instance.removeListener(_tryJoinPendingCode);
    _subscription?.cancel();
    _subscription = null;
    _started = false;
  }

  void _handleUri(Uri? uri) {
    if (uri == null) return;

    final code = _extractInviteCode(uri);
    if (code == null || code.isEmpty) return;

    _pendingCode = code;
    unawaited(_tryJoinPendingCode());
  }

  String? _extractInviteCode(Uri uri) {
    if (uri.scheme == 'inmoment' && uri.host == 'invite') {
      if (uri.pathSegments.length >= 2 &&
          uri.pathSegments[0].toLowerCase() == 'group') {
        final code = uri.pathSegments[1].trim().toUpperCase();
        return code.isEmpty ? null : code;
      }

      final queryCode = uri.queryParameters['code']?.trim().toUpperCase();
      if (queryCode != null && queryCode.isNotEmpty) {
        return queryCode;
      }
    }

    if (_isConfiguredInviteLink(uri)) {
      final queryCode = uri.queryParameters['code']?.trim().toUpperCase();
      if (queryCode != null && queryCode.isNotEmpty) {
        return queryCode;
      }

      final segments = uri.pathSegments.map((e) => e.toLowerCase()).toList();
      final inviteIndex = segments.indexOf('invite');

      if (inviteIndex >= 0 && uri.pathSegments.length > inviteIndex + 1) {
        final code = uri.pathSegments[inviteIndex + 1].trim().toUpperCase();
        return code.isEmpty ? null : code;
      }
    }

    return null;
  }

  bool _isConfiguredInviteLink(Uri uri) {
    if (uri.scheme != 'https' && uri.scheme != 'http') {
      return false;
    }

    final configuredBase = Uri.tryParse(AppLinks.websiteBaseUrl.trim());
    final configuredHost = configuredBase?.host.trim().toLowerCase();

    if (configuredHost == null || configuredHost.isEmpty) {
      return false;
    }

    return uri.host.trim().toLowerCase() == configuredHost;
  }

  Future<void> _tryJoinPendingCode() async {
    if (_joining) return;

    final code = _pendingCode;
    if (code == null || code.isEmpty) return;

    if (!AppSessionController.instance.isAuthenticated) {
      _showMessage(
        'Войдите в аккаунт, чтобы принять приглашение в группу.',
      );
      return;
    }

    _joining = true;

    try {
      await _api.joinByInviteCode(code);
      _pendingCode = null;

      await ActiveGroupController.instance.load(force: true);

      final navigator = appNavigatorKey.currentState;
      if (navigator != null) {
        await navigator.push(
          MaterialPageRoute(
            builder: (_) => const HomeScreen(initialTab: AppShellTab.profile),
          ),
        );
      }

      _showMessage('Вы присоединились к группе.');
    } catch (e) {
      _showMessage(_normalizeError(e));
    } finally {
      _joining = false;
    }
  }

  void _showMessage(String text) {
    final context = appNavigatorKey.currentContext;
    if (context == null) return;

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(text)),
    );
  }

  String _normalizeError(Object error) {
    final text = error.toString().trim();
    const prefix = 'Exception: ';
    if (text.startsWith(prefix)) {
      return text.substring(prefix.length).trim();
    }
    return text;
  }
}