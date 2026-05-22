import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../core/api/api_client.dart';
import '../../../core/platform/platform_file.dart';

class PublishApi {
  final _api = ApiClient.create();

  Future<PresignResult> presign({
    required String groupId,
    required String contentType,
  }) async {
    try {
      final response = await _api.dio.post(
        '/api/uploads/photos/presign',
        data: {
          'groupId': groupId,
          'contentType': contentType,
        },
      );

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера на presign.');
      }

      return PresignResult(
        uploadUrl: (data['uploadUrl'] ?? '').toString(),
        storageKey: (data['storageKey'] ?? '').toString(),
        fileUrl: (data['fileUrl'] ?? '').toString(),
      );
        } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось подготовить загрузку файла.',
        ),
      );
    }
  }

  Future<void> uploadToStorage({
    required String uploadUrl,
    required String contentType,
    CancelToken? cancelToken,
    void Function(double progress)? onProgress,
    Uint8List? bytes,
    String? localFilePath,
    int? contentLength,
  }) async {
    final dio = Dio();

    try {
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

        resolvedLength = contentLength ?? await platformFileLength(localFilePath) ?? 0;
        uploadData = await platformFileOpenRead(localFilePath);
      } else {
        throw Exception('Нет данных для загрузки.');
      }

      await dio.put(
        uploadUrl,
        data: uploadData,
        cancelToken: cancelToken,
        options: Options(
          contentType: contentType,
          headers: {
            'Content-Type': contentType,
            if (!kIsWeb) 'Content-Length': resolvedLength,
          },
          sendTimeout: const Duration(minutes: 10),
          receiveTimeout: const Duration(minutes: 10),
        ),
        onSendProgress: (sent, total) {
          if (onProgress == null) return;

          final safeTotal = total > 0 ? total : resolvedLength;
          if (safeTotal <= 0) {
            onProgress(0);
            return;
          }

          final value = sent / safeTotal;
          onProgress(value.clamp(0, 1));
        },
      );
    } on DioException catch (e) {
      if (CancelToken.isCancel(e)) {
        throw Exception('Загрузка отменена.');
      }

      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось загрузить файл в хранилище.',
        ),
      );
    }
  }
  
  Future<void> createPhoto({
    required String groupId,
    required String storageKey,
    required String contentType,
    required int sizeBytes,
    String? caption,
    int? trimStartMs,
    int? trimEndMs,
  }) async {
    final data = <String, dynamic>{
      'storageKey': storageKey,
      'contentType': contentType,
      'sizeBytes': sizeBytes,
      'caption': caption,
    };

    if (trimStartMs != null) {
      data['trimStartMs'] = trimStartMs;
    }

    if (trimEndMs != null) {
      data['trimEndMs'] = trimEndMs;
    }

    try {
      await _api.dio.post(
        '/api/groups/$groupId/photos',
        data: data,
      );
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось создать публикацию.',
        ),
      );
    }
  }

  String _extractErrorMessage(
    DioException error, {
    required String fallback,
  }) {
    final responseData = error.response?.data;

    if (responseData is Map<String, dynamic>) {
      final title = responseData['title'];
      if (title is String && title.trim().isNotEmpty) {
        return title.trim();
      }

      final detail = responseData['detail'];
      if (detail is String && detail.trim().isNotEmpty) {
        return detail.trim();
      }

      final errors = responseData['errors'];
      if (errors is Map) {
        final buffer = <String>[];

        for (final entry in errors.entries) {
          final value = entry.value;
          if (value is List) {
            for (final item in value) {
              if (item is String && item.trim().isNotEmpty) {
                buffer.add(item.trim());
              }
            }
          } else if (value is String && value.trim().isNotEmpty) {
            buffer.add(value.trim());
          }
        }

        if (buffer.isNotEmpty) {
          return buffer.join('\n');
        }
      }
    }

    if (responseData is String && responseData.trim().isNotEmpty) {
      return responseData.trim();
    }

    if (responseData is String && responseData.trim().isNotEmpty) {
      return responseData.trim();
    }

    if (error.message != null && error.message!.trim().isNotEmpty) {
      return error.message!.trim();
    }

    return fallback;
  }
}

class PresignResult {
  final String uploadUrl;
  final String storageKey;
  final String fileUrl;

  const PresignResult({
    required this.uploadUrl,
    required this.storageKey,
    required this.fileUrl,
  });
}