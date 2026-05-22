import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../models/mention_user.dart';

class MentionsApi {
  final _api = ApiClient.create();

  Future<List<MentionUser>> searchUsers({
    required String query,
    int limit = 5,
    String? groupId,
  }) async {
    final safeQuery = query.trim();

    try {
      final response = await _api.dio.get(
        '/api/mentions/users',
        queryParameters: {
          'q': safeQuery,
          'limit': limit,
          if (groupId != null && groupId.trim().isNotEmpty)
            'groupId': groupId.trim(),
        },
      );

      final data = response.data;

      if (data is! List) {
        return const [];
      }

      return data
          .whereType<Map<String, dynamic>>()
          .map(MentionUser.fromJson)
          .toList();
    } on DioException catch (e) {
      throw Exception(_extractError(e));
    }
  }

  String _extractError(DioException error) {
    final data = error.response?.data;

    if (data is Map<String, dynamic>) {
      final title = data['title'];
      if (title is String && title.trim().isNotEmpty) {
        return title.trim();
      }

      final detail = data['detail'];
      if (detail is String && detail.trim().isNotEmpty) {
        return detail.trim();
      }
    }

    return 'Не удалось загрузить подсказки для упоминаний.';
  }
}