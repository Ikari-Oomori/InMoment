import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../models/photo_details.dart';

enum PhotoDetailsFailureType {
  forbidden,
  notFound,
  methodNotAllowed,
  unauthorized,
  unknown,
}

class PhotoDetailsFailure implements Exception {
  final PhotoDetailsFailureType type;
  final String message;
  final int? statusCode;

  const PhotoDetailsFailure({
    required this.type,
    required this.message,
    this.statusCode,
  });

  @override
  String toString() => message;
}

class PhotoApi {
  final _api = ApiClient.create();

  Future<PhotoDetails> getPhotoDetails(
    String photoId, {
    String? groupId,
  }) async {
    try {
      final response = await _api.dio.get(
        '/api/photos/$photoId',
      );

      return PhotoDetails.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      final status = e.response?.statusCode;

      if (status == 401) {
        throw PhotoDetailsFailure(
          type: PhotoDetailsFailureType.unauthorized,
          message: 'Сессия истекла. Войдите снова.',
          statusCode: status,
        );
      }

      if (status == 403) {
        throw PhotoDetailsFailure(
          type: PhotoDetailsFailureType.forbidden,
          message:
              'Публикация недоступна. Возможно, пользователь заблокирован или у вас больше нет доступа.',
          statusCode: status,
        );
      }

      if (status == 404) {
        throw PhotoDetailsFailure(
          type: PhotoDetailsFailureType.notFound,
          message: 'Публикация не найдена или была удалена.',
          statusCode: status,
        );
      }

      if (status == 405) {
        throw PhotoDetailsFailure(
          type: PhotoDetailsFailureType.methodNotAllowed,
          message: 'Публикация временно недоступна.',
          statusCode: status,
        );
      }

      throw PhotoDetailsFailure(
        type: PhotoDetailsFailureType.unknown,
        message: _extractErrorMessage(
          e,
          fallback: 'Не удалось открыть публикацию.',
        ),
        statusCode: status,
      );
    }
  }

  Future<void> updatePhotoCaption({
    required String groupId,
    required String photoId,
    required String? caption,
  }) async {
    try {
      await _api.dio.patch(
        '/api/groups/$groupId/photos/$photoId',
        data: {
          'caption': caption,
        },
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось обновить публикацию.',
      ));
    }
  }

  Future<void> setReaction({
    required String photoId,
    required int type,
  }) async {
    try {
      await _api.dio.post(
        '/api/photos/$photoId/reactions',
        data: {
          'type': type,
        },
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось обновить реакцию.',
      ));
    }
  }

  Future<void> deletePhoto({
    required String groupId,
    required String photoId,
  }) async {
    try {
      await _api.dio.delete(
        '/api/groups/$groupId/photos/$photoId',
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось удалить публикацию.',
      ));
    }
  }

  Future<void> removeReaction(String photoId) async {
    try {
      await _api.dio.delete('/api/photos/$photoId/reactions');
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось удалить реакцию.',
      ));
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

    return fallback;
  }
}