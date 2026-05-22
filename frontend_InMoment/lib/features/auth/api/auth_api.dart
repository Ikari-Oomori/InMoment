import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_error.dart';

class AuthApi {
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

  Future<LoginTokens> login({
    required String email,
    required String password,
  }) async {
    try {
      final response = await _api.dio.post(
        '/api/auth/login',
        data: {
          'email': email.trim(),
          'password': password,
          'deviceName': _platformLabel,
          'platform': _platformLabel,
        },
      );

      final data = response.data as Map<String, dynamic>;
      final accessToken = data['accessToken'] as String?;
      final refreshToken = data['refreshToken'] as String?;

      if (accessToken == null || accessToken.isEmpty) {
        throw Exception('Сервер вернул неполный ответ авторизации.');
      }

      if (refreshToken == null || refreshToken.isEmpty) {
        throw Exception('Сервер вернул неполный ответ авторизации.');
      }

      return LoginTokens(
        accessToken: accessToken,
        refreshToken: refreshToken,
      );
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось выполнить вход. Попробуйте ещё раз.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось выполнить вход. Попробуйте ещё раз.',
        ),
      );
    }
  }

  Future<void> forgotPassword({
    required String email,
  }) async {
    try {
      await _api.dio.post(
        '/api/auth/forgot-password',
        data: {
          'email': email.trim(),
        },
      );
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback:
              'Не удалось отправить запрос на восстановление пароля. Попробуйте ещё раз.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback:
              'Не удалось отправить запрос на восстановление пароля. Попробуйте ещё раз.',
        ),
      );
    }
  }

  Future<void> resetPassword({
    required String token,
    required String newPassword,
  }) async {
    try {
      await _api.dio.post(
        '/api/auth/reset-password',
        data: {
          'token': token.trim(),
          'newPassword': newPassword,
        },
      );
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось сбросить пароль. Попробуйте ещё раз.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось сбросить пароль. Попробуйте ещё раз.',
        ),
      );
    }
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