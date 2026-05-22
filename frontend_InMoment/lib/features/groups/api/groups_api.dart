import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../models/group.dart';

class GroupsApi {
  final _api = ApiClient.create();

  Future<List<Group>> getMyGroups() async {
    try {
      final response = await _api.dio.get('/api/groups/my');

      final data = response.data;
      if (data is! List) {
        throw Exception('Некорректный ответ сервера при загрузке групп.');
      }

      return data
          .whereType<Map<String, dynamic>>()
          .map(Group.fromJson)
          .toList();
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось загрузить список групп.',
      ));
    }
  }

  Future<String> createGroup(String name) async {
    try {
      final response = await _api.dio.post(
        '/api/groups',
        data: {
          'name': name.trim(),
        },
      );

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера при создании группы.');
      }

      final groupId = (data['groupId'] ?? '').toString().trim();
      if (groupId.isEmpty) {
        throw Exception('Сервер не вернул идентификатор новой группы.');
      }

      return groupId;
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось создать группу.',
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
        return title;
      }

      final detail = responseData['detail'];
      if (detail is String && detail.trim().isNotEmpty) {
        return detail;
      }

      final errors = responseData['errors'];
      if (errors is Map) {
        final messages = <String>[];

        for (final entry in errors.entries) {
          final value = entry.value;
          if (value is List) {
            for (final item in value) {
              if (item is String && item.trim().isNotEmpty) {
                messages.add(item.trim());
              }
            }
          } else if (value is String && value.trim().isNotEmpty) {
            messages.add(value.trim());
          }
        }

        if (messages.isNotEmpty) {
          return messages.join('\n');
        }
      }
    }

    return fallback;
  }
}