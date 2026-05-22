import 'dart:async';

import 'package:dio/dio.dart';

final class ApiError {
  const ApiError._();

  static String normalize(
    Object error, {
    String fallback = 'Произошла ошибка. Попробуйте ещё раз.',
  }) {
    if (error is DioException) {
      return fromDio(error, fallback: fallback);
    }

    if (error is TimeoutException) {
      return 'Сервер отвечает слишком долго. Проверьте подключение и попробуйте ещё раз.';
    }

    final raw = error.toString().trim();
    if (raw.isEmpty) return fallback;

    final cleaned = raw
        .replaceFirst('Exception: ', '')
        .replaceFirst('DioException: ', '')
        .trim();

    final lower = cleaned.toLowerCase();

    if (lower.contains('timeoutexception') ||
        lower.contains('future not completed') ||
        lower.contains('receive timeout') ||
        lower.contains('connection timeout') ||
        lower.contains('send timeout')) {
      return 'Сервер отвечает слишком долго. Проверьте подключение и попробуйте ещё раз.';
    }

    if (lower.contains('socketexception') ||
        lower.contains('failed host lookup') ||
        lower.contains('connection refused') ||
        lower.contains('network is unreachable') ||
        lower.contains('xmlhttprequest error')) {
      return 'Не удалось подключиться к серверу. Проверьте интернет и попробуйте ещё раз.';
    }

    if (lower.contains('accessToken отсутствует') ||
        lower.contains('refreshtoken отсутствует')) {
      return 'Сервер вернул неполный ответ авторизации. Попробуйте войти ещё раз.';
    }

    if (_looksTechnical(cleaned)) {
      return fallback;
    }

    return cleaned;
  }

  static String fromDio(
    DioException error, {
    required String fallback,
  }) {
    switch (error.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
        return 'Сервер отвечает слишком долго. Проверьте подключение и попробуйте ещё раз.';

      case DioExceptionType.connectionError:
        return 'Не удалось подключиться к серверу. Проверьте интернет и попробуйте ещё раз.';

      case DioExceptionType.badCertificate:
        return 'Не удалось установить безопасное соединение с сервером.';

      case DioExceptionType.cancel:
        return 'Запрос был отменён. Попробуйте ещё раз.';

      case DioExceptionType.badResponse:
        final message = _extractResponseMessage(error.response?.data);
        if (message != null && message.isNotEmpty) {
          return message;
        }

        final statusCode = error.response?.statusCode;
        if (statusCode == 400) {
          return 'Проверьте введённые данные и попробуйте ещё раз.';
        }
        if (statusCode == 401) {
          return 'Неверные данные для входа или сессия истекла.';
        }
        if (statusCode == 403) {
          return 'У вас нет доступа к этому действию.';
        }
        if (statusCode == 404) {
          return 'Запрошенные данные не найдены.';
        }
        if (statusCode == 409) {
          return 'Действие нельзя выполнить из-за конфликта данных.';
        }
        if (statusCode != null && statusCode >= 500) {
          return 'На сервере произошла ошибка. Попробуйте позже.';
        }

        return fallback;

      case DioExceptionType.unknown:
        final responseMessage = _extractResponseMessage(error.response?.data);
        if (responseMessage != null && responseMessage.isNotEmpty) {
          return responseMessage;
        }

        if (_isSocketLikeError(error.error)) {
          return 'Не удалось подключиться к серверу. Проверьте интернет и попробуйте ещё раз.';
        }

        final message = error.message?.trim();
        if (message != null && message.isNotEmpty) {
          return normalize(message, fallback: fallback);
        }

        return fallback;
    }
  }

  static bool _isSocketLikeError(Object? error) {
    if (error == null) return false;

    final text = error.toString().toLowerCase();

    return text.contains('socketexception') ||
        text.contains('failed host lookup') ||
        text.contains('connection refused') ||
        text.contains('connection reset') ||
        text.contains('network is unreachable') ||
        text.contains('xmlhttprequest error');
  }

  static bool _looksTechnical(String value) {
    final lower = value.toLowerCase();

    return lower.contains('exception') ||
        lower.contains('stack trace') ||
        lower.contains('future') ||
        lower.contains('dart:') ||
        lower.contains('package:dio') ||
        lower.contains('xmlhttprequest') ||
        lower.contains('null check operator') ||
        lower.contains('type ') ||
        lower.contains('was not a subtype');
  }

  static String? _extractResponseMessage(dynamic data) {
    if (data is Map<String, dynamic>) {
      final errorsMessage = _extractValidationErrors(data['errors']);
      if (errorsMessage != null && errorsMessage.isNotEmpty) {
        return errorsMessage;
      }

      final message = data['message'];
      if (message is String && message.trim().isNotEmpty) {
        return message.trim();
      }

      final detail = data['detail'];
      if (detail is String && detail.trim().isNotEmpty) {
        return detail.trim();
      }

      final title = data['title'];
      if (title is String && title.trim().isNotEmpty) {
        return title.trim();
      }
    }

    if (data is String && data.trim().isNotEmpty) {
      return data.trim();
    }

    return null;
  }

  static String? _extractValidationErrors(dynamic errors) {
    if (errors is! Map) return null;

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

    if (messages.isEmpty) return null;
    return messages.join('\n');
  }
}