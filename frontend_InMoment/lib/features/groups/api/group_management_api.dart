import 'package:dio/dio.dart';
import 'package:file_picker/file_picker.dart';

import '../../../core/api/api_client.dart';
import '../models/group_member.dart';
import '../models/group_settings.dart';

class GroupManagementApi {
  final Dio _api = ApiClient.create().dio;
  final Dio _publicDio = Dio();

  Future<GroupSettings> getSettings(String groupId) async {
    try {
      final response = await _api.get('/api/groups/$groupId/settings');
      final data = _requireMap(
        response.data,
        fallback: 'Некорректный ответ сервера при загрузке настроек группы.',
      );

      return GroupSettings.fromJson(data);
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось загрузить настройки группы.',
        ),
      );
    }
  }

  Future<List<GroupMember>> getMembers(String groupId) async {
    try {
      final response = await _api.get('/api/groups/$groupId/members');
      final data = response.data;

      if (data is! List) {
        throw Exception('Некорректный ответ сервера при загрузке участников.');
      }

      return data
          .whereType<Map<String, dynamic>>()
          .map(GroupMember.fromJson)
          .toList(growable: false);
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось загрузить участников группы.',
        ),
      );
    }
  }

  Future<GroupSettings> updateSettings({
    required String groupId,
    required String name,
    String? description,
  }) async {
    try {
      final response = await _api.patch(
        '/api/groups/$groupId/settings',
        data: {
          'name': name.trim(),
          'description': _normalizeNullable(description),
        },
      );

      final data = _requireMap(
        response.data,
        fallback: 'Некорректный ответ сервера при сохранении группы.',
      );

      return GroupSettings.fromJson(data);
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось сохранить настройки группы.',
        ),
      );
    }
  }

  Future<void> leaveGroup(String groupId) async {
    try {
      await _api.post('/api/groups/$groupId/leave');
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось выйти из группы.',
        ),
      );
    }
  }

  Future<void> deleteGroup(String groupId) async {
    try {
      await _api.delete('/api/groups/$groupId');
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось удалить группу.',
        ),
      );
    }
  }

  Future<void> removeMember({
    required String groupId,
    required String userId,
  }) async {
    try {
      await _api.delete('/api/groups/$groupId/members/$userId');
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось удалить участника.',
        ),
      );
    }
  }

  Future<void> makeAdmin({
    required String groupId,
    required String userId,
  }) async {
    try {
      await _api.post('/api/groups/$groupId/members/$userId/make-admin');
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось назначить админа.',
        ),
      );
    }
  }

  Future<void> removeAdmin({
    required String groupId,
    required String userId,
  }) async {
    try {
      await _api.post('/api/groups/$groupId/members/$userId/remove-admin');
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось снять админа.',
        ),
      );
    }
  }

  Future<void> transferOwnership({
    required String groupId,
    required String newOwnerUserId,
  }) async {
    try {
      await _api.post(
        '/api/groups/$groupId/transfer-ownership',
        data: {
          'newOwnerUserId': newOwnerUserId,
        },
      );
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось передать владение группой.',
        ),
      );
    }
  }

  Future<String> uploadAvatar({
    required String groupId,
    required PlatformFile file,
  }) async {
    final bytes = file.bytes;
    if (bytes == null || bytes.isEmpty) {
      throw Exception('Не удалось прочитать выбранный файл.');
    }

    final contentType = _resolveImageContentType(file.name);

    try {
      final presignResponse = await _api.post(
        '/api/uploads/group-avatar/presign',
        data: {
          'groupId': groupId,
          'contentType': contentType,
        },
      );

      final data = _requireMap(
        presignResponse.data,
        fallback: 'Некорректный ответ сервера при подготовке загрузки аватара.',
      );

      final uploadUrl = (data['uploadUrl'] ?? '').toString();
      final fileUrl = (data['fileUrl'] ?? '').toString();

      if (uploadUrl.trim().isEmpty || fileUrl.trim().isEmpty) {
        throw Exception('Сервер не вернул данные для загрузки аватара.');
      }

      await _publicDio.put(
        uploadUrl,
        data: Stream.fromIterable([bytes]),
        options: Options(
          headers: {
            'Content-Type': contentType,
            'Content-Length': bytes.length,
          },
        ),
      );

      await _api.post(
        '/api/groups/$groupId/avatar',
        data: {
          'avatarUrl': fileUrl,
        },
      );

      return fileUrl;
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось загрузить аватар группы.',
        ),
      );
    }
  }

  String? _normalizeNullable(String? value) {
    if (value == null) return null;
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  String _resolveImageContentType(String fileName) {
    final lower = fileName.toLowerCase();

    if (lower.endsWith('.png')) return 'image/png';
    if (lower.endsWith('.webp')) return 'image/webp';
    if (lower.endsWith('.jpg') || lower.endsWith('.jpeg')) return 'image/jpeg';
    if (lower.endsWith('.heic')) return 'image/heic';
    if (lower.endsWith('.heif')) return 'image/heif';

    throw Exception(
      'Поддерживаются JPG, PNG, WEBP, HEIC и HEIF для аватара группы.',
    );
  }

  Map<String, dynamic> _requireMap(
    Object? data, {
    required String fallback,
  }) {
    if (data is Map<String, dynamic>) {
      return data;
    }

    throw Exception(fallback);
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

  Future<String> createInviteCode({
    required String groupId,
    int? maxUses,
    int? expireHours,
  }) async {
    try {
      final response = await _api.post(
        '/api/groups/$groupId/invite-code',
        data: {
          'groupId': groupId,
          'maxUses': maxUses,
          'expireHours': expireHours,
        },
      );

      final data = response.data;
      final code = data?.toString().trim();

      if (code == null || code.isEmpty) {
        throw Exception('Сервер не вернул код приглашения.');
      }

      return code;
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось создать ссылку-приглашение.',
        ),
      );
    }
  }

  Future<void> joinByInviteCode(String code) async {
    try {
      await _api.post(
        '/api/groups/join-by-code',
        data: {
          'code': code.trim(),
        },
      );
    } on DioException catch (e) {
      throw Exception(
        _extractErrorMessage(
          e,
          fallback: 'Не удалось присоединиться к группе по ссылке.',
        ),
      );
    }
  }
}