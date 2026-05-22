import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../core/api/api_client.dart';

class OnboardingApi {
  final _api = ApiClient.create();

  String get _platformLabel {
    if (kIsWeb) return 'web';
    switch (defaultTargetPlatform) {
      case TargetPlatform.android:
        return 'android';
      case TargetPlatform.iOS:
        return 'ios';
      case TargetPlatform.macOS:
        return 'macos';
      case TargetPlatform.windows:
        return 'windows';
      case TargetPlatform.linux:
        return 'linux';
      default:
        return 'flutter';
    }
  }

  Future<void> register({
    required String email,
    required String password,
    required String firstName,
    required String lastName,
    required String userName,
  }) async {
    try {
      await _api.dio.post(
        '/api/auth/register',
        data: {
          'email': email,
          'password': password,
          'firstName': firstName,
          'lastName': lastName,
          'userName': userName,
        },
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось зарегистрировать пользователя.',
      ));
    }
  }

  Future<LoginTokens> login({
    required String email,
    required String password,
  }) async {
    try {
      final response = await _api.dio.post(
        '/api/auth/login',
        data: {
          'email': email,
          'password': password,
          'deviceName': _platformLabel,
          'platform': _platformLabel,
        },
      );

      final data = response.data as Map<String, dynamic>;
      final accessToken = data['accessToken'] as String?;
      final refreshToken = data['refreshToken'] as String?;

      if (accessToken == null || accessToken.isEmpty) {
        throw Exception('accessToken отсутствует в ответе login.');
      }

      if (refreshToken == null || refreshToken.isEmpty) {
        throw Exception('refreshToken отсутствует в ответе login.');
      }

      return LoginTokens(
        accessToken: accessToken,
        refreshToken: refreshToken,
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось выполнить вход после регистрации.',
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

class LoginTokens {
  final String accessToken;
  final String refreshToken;

  const LoginTokens({
    required this.accessToken,
    required this.refreshToken,
  });
}