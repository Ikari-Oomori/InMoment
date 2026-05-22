import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_error.dart';
import '../models/device_token_binding.dart';
import '../models/notification_item.dart';
import '../models/notification_settings.dart';

class NotificationsApi {
  final _api = ApiClient.create();

  Future<NotificationsPageResult> getNotifications({
    int limit = 20,
    String? cursor,
  }) async {
    try {
      final response = await _api.dio.get(
        '/api/notifications',
        queryParameters: {
          'limit': limit,
          if (cursor != null && cursor.trim().isNotEmpty) 'cursor': cursor.trim(),
        },
      );

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw Exception('Unexpected notifications response format');
      }

      return NotificationsPageResult.fromJson(data);
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось загрузить уведомления.',
      ));
    }
  }

  Future<int> getUnreadCount() async {
    try {
      final response = await _api.dio.get('/api/notifications/unread-count');

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw Exception('Unexpected unread count response format');
      }

      return UnreadNotificationsCount.fromJson(data).count;
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось загрузить счётчик уведомлений.',
      ));
    }
  }

  Future<void> markRead(String notificationId) async {
    try {
      await _api.dio.post('/api/notifications/$notificationId/read');
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось отметить уведомление как прочитанное.',
      ));
    }
  }

  Future<void> markAllRead() async {
    try {
      await _api.dio.post('/api/notifications/read-all');
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось отметить все уведомления как прочитанные.',
      ));
    }
  }

  Future<NotificationSettingsModel> getSettings() async {
    try {
      final response = await _api.dio.get('/api/notifications/settings');
      final data = response.data;

      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера при загрузке настроек уведомлений.');
      }

      return NotificationSettingsModel.fromJson(data);
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось загрузить настройки уведомлений.',
      ));
    }
  }

  Future<NotificationSettingsModel> updateSettings(
    NotificationSettingsModel value,
  ) async {
    try {
      final response = await _api.dio.put(
        '/api/notifications/settings',
        data: {
          'pushEnabled': value.pushEnabled,
          'pushGroupInvitations': value.pushGroupInvitations,
          'pushReactions': value.pushReactions,
          'pushComments': value.pushComments,
          'pushReplies': value.pushReplies,
          'pushMentions': value.pushMentions,
          'pushPosts': value.pushPosts,
          'pushRetention': value.pushRetention,
          'pushProductUpdates': value.pushProductUpdates,
        },
      );

      final data = response.data;

      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера при сохранении уведомлений.');
      }

      return NotificationSettingsModel.fromJson(data);
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось сохранить настройки уведомлений.',
      ));
    }
  }

  Future<void> registerDevice({
    required String token,
    required int platform,
    required int provider,
    required String? deviceName,
  }) async {
    try {
      await _api.dio.post(
        '/api/notifications/devices',
        data: {
          'token': token,
          'platform': platform,
          'provider': provider,
          'deviceName': deviceName,
        },
      );
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось зарегистрировать устройство для push.',
      ));
    }
  }

  Future<List<DeviceTokenBinding>> getMyDevices() async {
    try {
      final response = await _api.dio.get('/api/notifications/devices');
      final data = response.data;

      if (data is! List) {
        throw Exception('Некорректный ответ сервера при загрузке устройств.');
      }

      return data
          .whereType<Map<String, dynamic>>()
          .map(DeviceTokenBinding.fromJson)
          .toList(growable: false);
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось загрузить список устройств.',
      ));
    }
  }

  Future<void> revokeDevice(String deviceTokenId) async {
    try {
      await _api.dio.delete('/api/notifications/devices/$deviceTokenId');
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось отвязать устройство от push.',
      ));
    }
  }
}