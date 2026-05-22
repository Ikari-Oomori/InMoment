import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

import '../../notifications/services/notification_navigation.dart';

import '../api/widget_api.dart';
import '../models/widget_snapshot.dart';
import 'widget_image_cache_service.dart';

class WidgetSyncService {
  WidgetSyncService._();

  static final WidgetSyncService instance = WidgetSyncService._();

  static const MethodChannel _channel = MethodChannel('inmoment/widget');

  final WidgetApi _api = WidgetApi();
  bool _navigationInitialized = false;

  Future<void> initializeWidgetNavigation() async {
    if (!_supported || _navigationInitialized) return;
    _navigationInitialized = true;

    _channel.setMethodCallHandler((call) async {
      if (call.method != 'openWidgetPhoto') return;

      final payload = _normalizeWidgetPayload(call.arguments);
      if (payload != null) {
        await NotificationNavigation.openFromPayload(payload);
      }
    });

    try {
      final initialPayload = await _channel.invokeMapMethod<String, dynamic>(
        'getInitialWidgetPayload',
      );

      if (initialPayload != null) {
        await NotificationNavigation.openFromPayload(initialPayload);
      }
    } catch (_) {
      // silent by design
    }
  }

  Map<String, dynamic>? _normalizeWidgetPayload(Object? raw) {
    if (raw is! Map) return null;

    final photoId = raw['photoId']?.toString().trim();
    if (photoId == null || photoId.isEmpty) return null;

    final groupId = raw['groupId']?.toString().trim();

    return <String, dynamic>{
      'source': 'android_widget',
      'targetType': 'photo',
      'photoId': photoId,
      if (groupId != null && groupId.isNotEmpty) 'groupId': groupId,
    };
  }

  bool get _supported =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android;

  Future<void> syncFromBackend() async {
    if (!_supported) return;

    try {
      final snapshot = await _api.getWidgetSnapshot();

      final latestUrl = snapshot.latestPhotoUrl?.trim();
      final contentKind = _contentKind(
        url: latestUrl,
        contentType: snapshot.latestContentType,
      );

      final cachedPreview = contentKind == 'video'
          ? await WidgetImageCacheService.instance.cacheVideoPreview(
              videoUrl: latestUrl,
            )
          : await WidgetImageCacheService.instance.cachePreview(
              imageUrl: latestUrl,
            );

      await _setWidgetData(
        snapshot: snapshot,
        cachedPhotoPath: cachedPreview.cachedPath,
        cachedPhotoUrl: cachedPreview.sourceUrl,
      );
    } catch (_) {
      // silent by design
    }
  }

  String _contentKind({String? url, String? contentType}) {
    final normalizedType = contentType?.trim().toLowerCase() ?? '';
    if (normalizedType.startsWith('video/')) return 'video';
    if (normalizedType.startsWith('image/')) return 'image';

    final value = (url ?? '').trim().toLowerCase().split('?').first;

    if (value.endsWith('.mp4') ||
        value.endsWith('.mov') ||
        value.endsWith('.m4v') ||
        value.endsWith('.webm') ||
        value.endsWith('.3gp') ||
        value.endsWith('.3gpp')) {
      return 'video';
    }

    if (value.endsWith('.jpg') ||
        value.endsWith('.jpeg') ||
        value.endsWith('.png') ||
        value.endsWith('.webp') ||
        value.endsWith('.heic') ||
        value.endsWith('.heif')) {
      return 'image';
    }

    return value.isEmpty ? 'empty' : 'unknown';
  }

  Future<void> clear() async {
    if (!_supported) return;

    try {
      await _channel.invokeMethod('clearWidgetData');
    } catch (_) {
      // silent by design
    }
  }

  Future<void> _setWidgetData({
    required WidgetSnapshot snapshot,
    required String? cachedPhotoPath,
    required String? cachedPhotoUrl,
  }) async {
    final createdAt = snapshot.latestPhotoCreatedAt?.toUtc();

    await _channel.invokeMethod(
      'setWidgetData',
      <String, dynamic>{
        'activeGroupId': snapshot.activeGroupId,
        'activeGroupName': snapshot.activeGroupName,
        'activeGroupAvatarUrl': snapshot.activeGroupAvatarUrl,
        'latestPhotoId': snapshot.latestPhotoId,
        'latestPhotoUrl': snapshot.latestPhotoUrl,
        'latestContentKind': _contentKind(
          url: snapshot.latestPhotoUrl,
          contentType: snapshot.latestContentType,
        ),
        'latestPhotoCreatedAtIso': createdAt?.toIso8601String(),
        'newReactionsCount': snapshot.newReactionsCount,
        'cachedPhotoPath': cachedPhotoPath,
        'cachedPhotoUrl': cachedPhotoUrl,
      },
    );
  }
}