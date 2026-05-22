import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/navigation/app_navigator.dart';
import '../../home/home_screen.dart';
import '../../photo/pages/photo_details_page.dart';
import '../../profile/pages/public_user_profile_page.dart';
import '../../profile/pages/settings_page.dart';
import '../../reports/pages/my_reports_page.dart';
import '../../shell/models/app_shell_tab.dart';
import '../../support/pages/support_page.dart';
import '../models/notification_item.dart';
import '../pages/notifications_page.dart';
import '../pages/announcement_details_page.dart';
import '../api/notifications_api.dart';

class NotificationNavigation {
  const NotificationNavigation._();

  static Map<String, dynamic>? _pendingPayload;
  static Timer? _retryTimer;
  static bool _isNavigating = false;

  static Future<void> openFromItem(NotificationItem item) {
    return openFromPayload({
      'notificationId': item.id,
      'type': item.type.toString(),
      'targetType': item.targetType.toString(),
      if (item.photoId != null) 'photoId': item.photoId!,
      if (item.groupId != null) 'groupId': item.groupId!,
      if (item.commentId != null) 'commentId': item.commentId!,
      if (item.invitationId != null) 'invitationId': item.invitationId!,
      if (item.systemMemoryId != null) 'systemMemoryId': item.systemMemoryId!,
      if (item.actorUserId != null) 'actorUserId': item.actorUserId!,
      if (item.targetId != null) 'targetId': item.targetId!,
      if (item.targetRoute != null) 'targetRoute': item.targetRoute!,
    });
  }

  static Future<void> openActorProfile(String? userId) async {
    final normalizedUserId = _normalizeId(userId);
    if (normalizedUserId == null) return;

    await _pushWhenReady(
      MaterialPageRoute(
        builder: (_) => PublicUserProfilePage(userId: normalizedUserId),
      ),
    );
  }

  static Future<void> openFromPayload(Map<String, dynamic> data) async {
    final payload = _normalizePayload(data);

    final navigator = appNavigatorKey.currentState;
    if (navigator == null) {
      _queuePayload(payload);
      return;
    }

    if (_isNavigating) {
      _pendingPayload = payload;
      _schedulePendingFlush();
      return;
    }

    _isNavigating = true;
    try {
      final target = _NotificationTarget.fromPayload(payload);
      await _openTarget(navigator, target);
    } finally {
      _isNavigating = false;

      final pending = _pendingPayload;
      if (pending != null) {
        _pendingPayload = null;
        _schedulePendingFlush(payload: pending);
      }
    }
  }

  static Future<void> openNotificationsPage() async {
    await _pushWhenReady(
      MaterialPageRoute(builder: (_) => const NotificationsPage()),
    );
  }

  static Future<void> _openTarget(
    NavigatorState navigator,
    _NotificationTarget target,
  ) async {
    if (target.hasPhoto) {
      await navigator.push(
        MaterialPageRoute(
          builder: (_) => PhotoDetailsPage(
            photoId: target.photoId!,
            groupId: target.groupId,
            initialCommentId: target.commentId,
          ),
        ),
      );
      return;
    }

    if (target.isInvitation) {
      await navigator.push(
        MaterialPageRoute(
          builder: (_) => const HomeScreen(initialTab: AppShellTab.profile),
        ),
      );
      return;
    }

    if (target.isReports) {
      await navigator.push(
        MaterialPageRoute(builder: (_) => const MyReportsPage()),
      );
      return;
    }

    if (target.systemMemoryId != null && target.systemMemoryId!.isNotEmpty) {
      await navigator.push(
        MaterialPageRoute(
          builder: (_) => HomeScreen(
            initialTab: AppShellTab.memories,
            initialSystemMemoryId: target.systemMemoryId,
          ),
        ),
      );
      return;
    }

    if (target.isMemories) {
      await navigator.push(
        MaterialPageRoute(
          builder: (_) => const HomeScreen(initialTab: AppShellTab.memories),
        ),
      );
      return;
    }

    if (target.isCamera) {
      await navigator.push(
        MaterialPageRoute(
          builder: (_) => const HomeScreen(initialTab: AppShellTab.camera),
        ),
      );
      return;
    }

    if (target.isSettings) {
      await navigator.push(
        MaterialPageRoute(builder: (_) => const SettingsPage()),
      );
      return;
    }

    if (target.isSuggestion) {
      await navigator.push(
        MaterialPageRoute(builder: (_) => const SupportPage.suggestion()),
      );
      return;
    }

    if (target.profileUserId != null) {
      await navigator.push(
        MaterialPageRoute(
          builder: (_) => PublicUserProfilePage(userId: target.profileUserId!),
        ),
      );
      return;
    }

    if (target.isSystemAnnouncement) {
      try {
        final page = await NotificationsApi().getNotifications(limit: 50);

        final item = page.items
            .where((item) {
              if (target.notificationId != null &&
                  target.notificationId!.isNotEmpty &&
                  item.id == target.notificationId) {
                return true;
              }

              if (target.systemAnnouncementId != null &&
                  target.systemAnnouncementId!.isNotEmpty &&
                  item.systemAnnouncementId == target.systemAnnouncementId) {
                return true;
              }

              return false;
            })
            .cast<NotificationItem?>()
            .firstWhere(
              (item) => item != null,
              orElse: () => null,
            );

        if (item != null) {
          await navigator.push(
            MaterialPageRoute(
              builder: (_) => AnnouncementDetailsPage(item: item),
            ),
          );
          return;
        }
      } catch (_) {

      }

      await navigator.push(
        MaterialPageRoute(builder: (_) => const NotificationsPage()),
      );
      return;
    }

    await navigator.push(
      MaterialPageRoute(builder: (_) => const NotificationsPage()),
    );
  }

  static Future<void> _pushWhenReady(Route<void> route) async {
    final navigator = appNavigatorKey.currentState;
    if (navigator == null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        unawaited(_pushWhenReady(route));
      });
      return;
    }

    await navigator.push(route);
  }

  static void _queuePayload(Map<String, dynamic> payload) {
    _pendingPayload = payload;
    _schedulePendingFlush();
  }

  static void _schedulePendingFlush({Map<String, dynamic>? payload}) {
    if (payload != null) {
      _pendingPayload = payload;
    }

    _retryTimer?.cancel();
    _retryTimer = Timer(const Duration(milliseconds: 220), () {
      final pending = _pendingPayload;
      if (pending == null) return;

      final navigator = appNavigatorKey.currentState;
      if (navigator == null || _isNavigating) {
        _schedulePendingFlush(payload: pending);
        return;
      }

      _pendingPayload = null;
      unawaited(openFromPayload(pending));
    });
  }

  static Map<String, dynamic> _normalizePayload(Map<String, dynamic> data) {
    final normalized = <String, dynamic>{};

    for (final entry in data.entries) {
      final key = entry.key.toString().trim();
      if (key.isEmpty || entry.value == null) continue;
      normalized[key] = entry.value;
    }

    final targetRoute = _readAny(normalized, const [
      'targetRoute',
      'TargetRoute',
      'target_route',
      'route',
      'Route',
    ]);

    if (targetRoute != null) {
      final routeData = _parseRoute(targetRoute);
      normalized.addAll(routeData);
      normalized['targetRoute'] = targetRoute;
    }

    return normalized;
  }

  static Map<String, String> _parseRoute(String route) {
    final result = <String, String>{};

    final uri = Uri.tryParse(route);
    if (uri == null) return result;

    final segments = uri.pathSegments;

    for (var i = 0; i < segments.length; i++) {
      final current = segments[i].toLowerCase();
      final next = i + 1 < segments.length ? segments[i + 1] : null;

      if (next == null || next.trim().isEmpty) continue;

      if (current == 'groups') result.putIfAbsent('groupId', () => next);
      if (current == 'photos') result.putIfAbsent('photoId', () => next);
      if (current == 'comments') result.putIfAbsent('commentId', () => next);
      if (current == 'announcements' || current == 'system-announcements') {
        result.putIfAbsent('systemAnnouncementId', () => next);
      }
      if (current == 'invitations') {
        result.putIfAbsent('invitationId', () => next);
      }
      if (current == 'users' || current == 'profiles' || current == 'profile') {
        result.putIfAbsent('profileUserId', () => next);
      }
      if ((current == 'memories' || current == 'memory-videos') &&
          next.toLowerCase() == 'system' &&
          i + 2 < segments.length &&
          segments[i + 2].trim().isNotEmpty) {
        result.putIfAbsent('systemMemoryId', () => segments[i + 2].trim());
        continue;
      }

      if (current == 'memories' || current == 'memory-videos') {
        result.putIfAbsent('memoryId', () => next);
      }
    }

    final commentId = uri.queryParameters['commentId'] ??
        uri.queryParameters['comment_id'] ??
        uri.queryParameters['CommentId'];
    if (commentId != null && commentId.trim().isNotEmpty) {
      result.putIfAbsent('commentId', () => commentId.trim());
    }

    final memoryId = uri.queryParameters['memoryId'] ??
        uri.queryParameters['memory_id'] ??
        uri.queryParameters['MemoryId'];
    if (memoryId != null && memoryId.trim().isNotEmpty) {
      result.putIfAbsent('memoryId', () => memoryId.trim());
    }

    final systemMemoryId = uri.queryParameters['systemMemoryId'] ??
        uri.queryParameters['system_memory_id'] ??
        uri.queryParameters['SystemMemoryId'];
    if (systemMemoryId != null && systemMemoryId.trim().isNotEmpty) {
      result.putIfAbsent('systemMemoryId', () => systemMemoryId.trim());
    }

    return result;
  }

  static String? _readAny(Map<String, dynamic> data, List<String> keys) {
    for (final key in keys) {
      final value = _readString(data, key);
      if (value != null) return value;
    }

    return null;
  }

  static String? _readString(Map<String, dynamic> data, String key) {
    final value = data[key];
    if (value == null) return null;

    final text = value.toString().trim();
    if (text.isEmpty) return null;

    return text;
  }

  static String? _normalizeId(String? raw) {
    final value = raw?.trim();
    if (value == null || value.isEmpty) return null;
    return value;
  }

  static String? _normalizeType(String? raw) {
    if (raw == null || raw.trim().isEmpty) return null;

    final value = raw.trim();

    switch (value) {
      case '1':
      case 'GroupInvitationReceived':
      case 'group_invitation_received':
      case 'invitation':
        return 'group_invitation_received';

      case '2':
      case 'ReactionOnPhoto':
      case 'reaction_on_photo':
      case 'reaction':
        return 'reaction_on_photo';

      case '3':
      case 'CommentOnPhoto':
      case 'comment_on_photo':
      case 'comment':
        return 'comment_on_photo';

      case '4':
      case 'ReplyToComment':
      case 'reply_to_comment':
      case 'reply':
        return 'reply_to_comment';

      case '5':
      case 'CommentMention':
      case 'comment_mention':
      case 'mention':
        return 'comment_mention';

      case '6':
      case 'PhotoPublishedInGroup':
      case 'photo_published_in_group':
      case 'post':
        return 'photo_published_in_group';

      case '7':
      case 'ReportReviewed':
      case 'report_reviewed':
        return 'report_reviewed';

      case '8':
      case 'ReportAppealSubmitted':
      case 'report_appeal_submitted':
        return 'report_appeal_submitted';

      case '9':
      case 'ReportAppealReviewed':
      case 'report_appeal_reviewed':
        return 'report_appeal_reviewed';

      case '10':
      case 'ShareReminder':
      case 'share_reminder':
        return 'share_reminder';

      case '11':
      case 'FeedbackPrompt':
      case 'feedback_prompt':
        return 'feedback_prompt';

      case '12':
      case 'Anniversary':
      case 'anniversary':
        return 'anniversary';

      case '13':
      case 'ProductAnnouncement':
      case 'product_announcement':
        return 'product_announcement';

      case '14':
      case 'SystemMemoryReady':
      case 'system_memory_ready':
      case 'system_memory':
        return 'system_memory_ready';

      case '20':
      case 'ModeratorAnnouncement':
      case 'moderator_announcement':
      case 'system_announcement':
      case 'announcement':
        return 'moderator_announcement';

      default:
        return value.toLowerCase();
    }
  }

  static String? _normalizeTargetType(String? raw) {
    if (raw == null || raw.trim().isEmpty) return null;

    final value = raw.trim();

    switch (value) {
      case '1':
      case 'invitation':
      case 'Invitation':
        return 'invitation';

      case '2':
      case 'photo':
      case 'Photo':
        return 'photo';

      case '3':
      case 'comment':
      case 'Comment':
        return 'comment';

      case '4':
      case 'reports':
      case 'Reports':
        return 'reports';

      case '5':
      case 'system_memory':
      case 'SystemMemory':
      case 'systemMemory':
        return 'system_memory';

      case '6':
      case 'system_announcement':
      case 'SystemAnnouncement':
      case 'systemAnnouncement':
      case 'announcement':
        return 'system_announcement';

      default:
        return value.toLowerCase();
    }
  }
}

class _NotificationTarget {
  final String? notificationId;
  final String? type;
  final String? targetType;
  final String? targetRoute;
  final String? groupId;
  final String? photoId;
  final String? commentId;
  final String? invitationId;
  final String? actorUserId;
  final String? targetId;
  final String? profileUserId;
  final String? memoryId;
  final String? systemMemoryId;
  final String? systemAnnouncementId;

  const _NotificationTarget({
    required this.notificationId,
    required this.type,
    required this.targetType,
    required this.targetRoute,
    required this.groupId,
    required this.photoId,
    required this.commentId,
    required this.invitationId,
    required this.actorUserId,
    required this.targetId,
    required this.profileUserId,
    required this.memoryId,
    required this.systemMemoryId,
    required this.systemAnnouncementId,
  });

  factory _NotificationTarget.fromPayload(Map<String, dynamic> data) {
    final type = NotificationNavigation._normalizeType(
      NotificationNavigation._readAny(data, const ['type', 'Type']),
    );
    final targetType = NotificationNavigation._normalizeTargetType(
      NotificationNavigation._readAny(data, const [
        'targetType',
        'TargetType',
        'target_type',
      ]),
    );

    final targetRoute = NotificationNavigation._readAny(data, const [
      'targetRoute',
      'TargetRoute',
      'target_route',
      'route',
      'Route',
    ]);

    final targetId = NotificationNavigation._readAny(data, const [
      'targetId',
      'TargetId',
      'target_id',
    ]);

    return _NotificationTarget(
      notificationId: NotificationNavigation._readAny(data, const [
        'notificationId',
        'NotificationId',
        'notification_id',
        'id',
        'Id',
      ]),
      type: type,
      targetType: targetType,
      targetRoute: targetRoute,
      groupId: NotificationNavigation._readAny(data, const [
        'groupId',
        'GroupId',
        'group_id',
      ]),
      photoId: NotificationNavigation._readAny(data, const [
        'photoId',
        'PhotoId',
        'photo_id',
      ]),
      commentId: NotificationNavigation._readAny(data, const [
        'commentId',
        'CommentId',
        'comment_id',
      ]) ??
          (targetType == 'comment' ? targetId : null),
      invitationId: NotificationNavigation._readAny(data, const [
        'invitationId',
        'InvitationId',
        'invitation_id',
      ]) ??
          (targetType == 'invitation' ? targetId : null),
      actorUserId: NotificationNavigation._readAny(data, const [
        'actorUserId',
        'ActorUserId',
        'actor_user_id',
      ]),
      targetId: targetId,
      profileUserId: NotificationNavigation._readAny(data, const [
        'profileUserId',
        'ProfileUserId',
        'profile_user_id',
        'userId',
        'UserId',
        'user_id',
      ]),
      memoryId: NotificationNavigation._readAny(data, const [
        'memoryId',
        'MemoryId',
        'memory_id',
      ]),
      systemMemoryId: NotificationNavigation._readAny(data, const [
        'systemMemoryId',
        'SystemMemoryId',
        'system_memory_id',
      ]) ??
          (targetType == 'system_memory' ? targetId : null),
      systemAnnouncementId: NotificationNavigation._readAny(data, const [
        'systemAnnouncementId',
        'SystemAnnouncementId',
        'system_announcement_id',
        'announcementId',
        'AnnouncementId',
        'announcement_id',
      ]) ??
          (targetType == 'system_announcement' ? targetId : null),
    );
  }

  bool get hasPhoto => photoId != null && photoId!.isNotEmpty;

  bool get isInvitation =>
      targetType == 'invitation' ||
      invitationId != null ||
      type == 'group_invitation_received' ||
      _routeStartsWith('/invitations');

  bool get isReports =>
      targetType == 'reports' ||
      type == 'report_reviewed' ||
      type == 'report_appeal_submitted' ||
      type == 'report_appeal_reviewed' ||
      _routeStartsWith('/reports');

  bool get isMemories =>
      type == 'anniversary' ||
      type == 'system_memory_ready' ||
      systemMemoryId != null ||
      memoryId != null ||
      _routeStartsWith('/memories') ||
      _routeStartsWith('/memory-videos');

  bool get isCamera => type == 'share_reminder' || _routeStartsWith('/camera');

  bool get isSettings =>
      type == 'product_announcement' || _routeStartsWith('/settings');

  bool get isSuggestion =>
      type == 'feedback_prompt' || _routeStartsWith('/support/suggestion');

   bool get isSystemAnnouncement =>
      targetType == 'system_announcement' ||
      type == 'moderator_announcement' ||
      systemAnnouncementId != null ||
      _routeStartsWith('/announcements') ||
      _routeStartsWith('/system-announcements');

  bool _routeStartsWith(String path) {
    final route = targetRoute?.trim().toLowerCase();
    if (route == null || route.isEmpty) return false;

    return route == path ||
        route.startsWith('$path/') ||
        route.startsWith('$path?');
  }
}