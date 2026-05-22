import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../models/blocked_user.dart';

class BlocksApi {
  final _api = ApiClient.create();

  Future<List<BlockedUser>> getBlockedUsers() async {
    try {
      final response = await _api.dio.get('/api/blocks');
      final data = response.data;

      if (data is! List) {
        throw Exception('Некорректный ответ сервера при загрузке блокировок.');
      }

      return data
          .whereType<Map<String, dynamic>>()
          .map(BlockedUser.fromJson)
          .toList();
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось загрузить блокировки.',
        ),
      );
    }
  }

  Future<void> blockUser(String userId) async {
    try {
      await _api.dio.post('/api/blocks/$userId');
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось заблокировать пользователя.',
        ),
      );
    }
  }

  Future<void> unblockUser(String userId) async {
    try {
      await _api.dio.delete('/api/blocks/$userId');
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось разблокировать пользователя.',
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
    }

    return fallback;
  }
}