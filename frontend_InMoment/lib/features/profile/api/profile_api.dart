import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../../../core/storage/token_storage.dart';
import '../models/user_profile.dart';
import '../models/public_user_profile.dart';

class ProfileApi {
  final _api = ApiClient.create();
  final _tokenStorage = const TokenStorage();

  Future<UserProfile> getMe() async {
    try {
      final response = await _api.dio.get('/api/users/me');

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера при загрузке профиля.');
      }

      return UserProfile.fromJson(data);
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось загрузить профиль.',
      ));
    }
  }

    Future<PublicUserProfile> getPublicProfile(String userId) async {
    try {
      final response = await _api.dio.get('/api/users/$userId/public-profile');

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера при загрузке профиля пользователя.');
      }

      return PublicUserProfile.fromJson(data);
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось загрузить профиль пользователя.',
      ));
    }
  }

  Future<void> setActiveGroup(String groupId) async {
    try {
      await _api.dio.patch(
        '/api/users/me/active-group',
        data: {
          'groupId': groupId,
        },
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось выбрать активную группу.',
      ));
    }
  }

  Future<void> updateActiveGroup(String groupId) {
    return setActiveGroup(groupId);
  }

  Future<UserProfile> updateProfile({
    required String firstName,
    required String lastName,
    required String userName,
    String? phoneNumber,
  }) async {
    try {
      await _api.dio.patch(
        '/api/users/me',
        data: {
          'firstName': firstName.trim(),
          'lastName': lastName.trim(),
          'userName': userName.trim(),
          'phoneNumber': _normalizePhone(phoneNumber),
        },
      );

      return await getMe();
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось обновить профиль.',
      ));
    }
  }

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    try {
      final refreshToken = await _tokenStorage.getRefreshToken();

      await _api.dio.post(
        '/api/auth/change-password',
        data: {
          'currentPassword': currentPassword,
          'newPassword': newPassword,
          'currentRefreshToken': refreshToken,
        },
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось изменить пароль.',
      ));
    }
  }

  String? _normalizePhone(String? input) {
    if (input == null) return null;

    final trimmed = input.trim();
    if (trimmed.isEmpty) return null;

    final hasLeadingPlus = trimmed.startsWith('+');
    final digitsOnly = trimmed.replaceAll(RegExp(r'[^\d]'), '');

    if (digitsOnly.isEmpty) return null;

    return hasLeadingPlus ? '+$digitsOnly' : digitsOnly;
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