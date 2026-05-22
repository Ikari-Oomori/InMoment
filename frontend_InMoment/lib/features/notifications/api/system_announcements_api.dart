import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_error.dart';
import '../../../core/platform/platform_file.dart';
import '../models/system_announcement.dart';

class SystemAnnouncementsApi {
  final _api = ApiClient.create();

  Future<List<SystemAnnouncement>> list({int limit = 50}) async {
    try {
      final response = await _api.dio.get(
        '/api/system-announcements',
        queryParameters: {'limit': limit},
      );

      final data = response.data;
      if (data is! List) {
        throw Exception('Некорректный ответ сервера.');
      }

      return data
          .whereType<Map<String, dynamic>>()
          .map(SystemAnnouncement.fromJson)
          .toList(growable: false);
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось загрузить системные уведомления.',
      ));
    }
  }

  Future<SystemAnnouncement> getById(String id) async {
    try {
      final response = await _api.dio.get('/api/system-announcements/$id');

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера.');
      }

      return SystemAnnouncement.fromJson(data);
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось загрузить системное уведомление.',
      ));
    }
  }

  Future<void> create({
    required String text,
    String? mediaUrl,
    String? mediaContentType,
  }) async {
    try {
      await _api.dio.post(
        '/api/system-announcements',
        data: {
          'text': text,
          'mediaUrl': mediaUrl,
          'mediaContentType': mediaContentType,
        },
      );
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось отправить системное уведомление.',
      ));
    }
  }

  Future<void> update({
    required String id,
    required String text,
    String? mediaUrl,
    String? mediaContentType,
  }) async {
    try {
      await _api.dio.put(
        '/api/system-announcements/$id',
        data: {
          'text': text,
          'mediaUrl': mediaUrl,
          'mediaContentType': mediaContentType,
        },
      );
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось обновить системное уведомление.',
      ));
    }
  }

  Future<PresignSystemAnnouncementMediaResult> presignMedia({
    required String contentType,
  }) async {
    try {
      final response = await _api.dio.post(
        '/api/uploads/system-announcements/presign',
        data: {'contentType': contentType},
      );

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера.');
      }

      return PresignSystemAnnouncementMediaResult(
        uploadUrl: (data['uploadUrl'] ?? '').toString(),
        fileUrl: (data['fileUrl'] ?? '').toString(),
      );
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось подготовить загрузку медиа.',
      ));
    }
  }

  Future<void> uploadToStorage({
    required String uploadUrl,
    required String contentType,
    Uint8List? bytes,
    String? localFilePath,
    int? contentLength,
  }) async {
    final dio = Dio();

    late final Object uploadData;
    late final int resolvedLength;

    if (bytes != null) {
      uploadData = bytes;
      resolvedLength = contentLength ?? bytes.length;
    } else if (!kIsWeb &&
        localFilePath != null &&
        localFilePath.trim().isNotEmpty) {
      if (!await platformFileExists(localFilePath)) {
        throw Exception('Файл для загрузки не найден.');
      }

      uploadData = await platformFileOpenRead(localFilePath);
      resolvedLength = contentLength ?? await platformFileLength(localFilePath) ?? 0;
    } else {
      throw Exception('Нет данных для загрузки.');
    }

    await dio.put(
      uploadUrl,
      data: uploadData,
      options: Options(
        contentType: contentType,
        headers: {
          'Content-Type': contentType,
          if (!kIsWeb) 'Content-Length': resolvedLength,
        },
        sendTimeout: const Duration(minutes: 10),
        receiveTimeout: const Duration(minutes: 10),
      ),
    );
  }

  Future<void> delete(String id) async {
    try {
      await _api.dio.delete('/api/system-announcements/$id');
    } on DioException catch (e) {
      throw Exception(ApiError.fromDio(
        e,
        fallback: 'Не удалось удалить системное уведомление.',
      ));
    }
  }
}

class PresignSystemAnnouncementMediaResult {
  final String uploadUrl;
  final String fileUrl;

  const PresignSystemAnnouncementMediaResult({
    required this.uploadUrl,
    required this.fileUrl,
  });
}