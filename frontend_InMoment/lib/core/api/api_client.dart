import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../../app/app.dart';
import '../config/env.dart';
import '../controllers/app_session_controller.dart';
import '../navigation/app_navigator.dart';
import '../storage/token_storage.dart';

class ApiClient {
  ApiClient._internal()
      : dio = Dio(
          BaseOptions(
            baseUrl: Env.baseUrl,
            connectTimeout: Env.connectTimeout,
            receiveTimeout: Env.receiveTimeout,
            sendTimeout: Env.sendTimeout,
            responseType: ResponseType.json,
            headers: const {
              'Content-Type': 'application/json',
            },
          ),
        ),
        _refreshDio = Dio(
          BaseOptions(
            baseUrl: Env.baseUrl,
            connectTimeout: Env.connectTimeout,
            receiveTimeout: Env.receiveTimeout,
            sendTimeout: Env.sendTimeout,
            responseType: ResponseType.json,
            headers: const {
              'Content-Type': 'application/json',
            },
          ),
        ) {
    _configureInterceptors();
  }

  static final ApiClient _instance = ApiClient._internal();

  factory ApiClient.create() => _instance;

  final Dio dio;
  final Dio _refreshDio;

  final TokenStorage _tokenStorage = const TokenStorage();

  Future<String?>? _refreshOperation;
  Future<void>? _forcedLogoutOperation;
  bool _terminalAuthFailure = false;

  static const int _maxTransientRetries = 1;
  static const String _retryAttemptHeader = 'x-inmoment-retry-attempt';

  void _configureInterceptors() {
    dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) async {
          final path = options.path.toLowerCase();

          if (_terminalAuthFailure && !_isAllowedDuringTerminalFailure(path)) {
            handler.reject(
              DioException(
                requestOptions: options,
                error: 'Terminal auth failure. Session is already invalidated.',
                type: DioExceptionType.cancel,
              ),
            );
            return;
          }

          final token = await _tokenStorage.getAccessToken();

          if (token != null &&
              token.isNotEmpty &&
              !_isAuthRefreshRequest(path)) {
            options.headers['Authorization'] = 'Bearer $token';
          }

          handler.next(options);
        },
        onResponse: (response, handler) {
          final path = response.requestOptions.path.toLowerCase();

          if (_isTerminalResettingSuccessPath(path)) {
            _terminalAuthFailure = false;
            _forcedLogoutOperation = null;
          }

          handler.next(response);
        },
        onError: (error, handler) async {
          if (_terminalAuthFailure) {
            handler.next(error);
            return;
          }

          if (!_shouldAttemptRefresh(error)) {
            if (_shouldRetryTransient(error)) {
              try {
                final retryResponse = await _retryTransient(error);
                handler.resolve(retryResponse);
                return;
              } catch (_) {
                handler.next(error);
                return;
              }
            }

            handler.next(error);
            return;
          }

          try {
            _refreshOperation ??= _refreshAccessToken();

            final freshAccessToken = await _refreshOperation;

            if (freshAccessToken == null || freshAccessToken.isEmpty) {
              await _forceLogoutOnce();
              handler.next(error);
              return;
            }

            final requestOptions = error.requestOptions;

            final retryResponse = await dio.fetch<dynamic>(
              requestOptions.copyWith(
                headers: {
                  ...requestOptions.headers,
                  'Authorization': 'Bearer $freshAccessToken',
                },
              ),
            );

            handler.resolve(retryResponse);
          } catch (_) {
            await _forceLogoutOnce();
            handler.next(error);
          } finally {
            _refreshOperation = null;
          }
        },
      ),
    );

    if (kDebugMode) {
      dio.interceptors.add(
        LogInterceptor(
          requestBody: true,
          responseBody: true,
        ),
      );
    }
  }

  bool _shouldRetryTransient(DioException error) {
    final requestOptions = error.requestOptions;
    final method = requestOptions.method.toUpperCase();

    if (method != 'GET' && method != 'HEAD' && method != 'OPTIONS') {
      return false;
    }

    if (requestOptions.cancelToken?.isCancelled == true) {
      return false;
    }

    final attempt = _retryAttempt(requestOptions);
    if (attempt >= _maxTransientRetries) {
      return false;
    }

    switch (error.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.connectionError:
      case DioExceptionType.unknown:
        return true;
      case DioExceptionType.badResponse:
        final statusCode = error.response?.statusCode;
        return statusCode == 502 || statusCode == 503 || statusCode == 504;
      case DioExceptionType.sendTimeout:
      case DioExceptionType.badCertificate:
      case DioExceptionType.cancel:
        return false;
    }
  }

  int _retryAttempt(RequestOptions requestOptions) {
    final raw = requestOptions.headers[_retryAttemptHeader];
    if (raw is int) return raw;
    if (raw is String) return int.tryParse(raw) ?? 0;
    return 0;
  }

  Future<Response<dynamic>> _retryTransient(DioException error) async {
    final requestOptions = error.requestOptions;
    final nextAttempt = _retryAttempt(requestOptions) + 1;

    await Future<void>.delayed(Duration(milliseconds: 250 * nextAttempt));

    return dio.fetch<dynamic>(
      requestOptions.copyWith(
        headers: {
          ...requestOptions.headers,
          _retryAttemptHeader: nextAttempt,
        },
      ),
    );
  }

  bool _shouldAttemptRefresh(DioException error) {
    final statusCode = error.response?.statusCode;
    final path = error.requestOptions.path.toLowerCase();

    if (statusCode != 401) {
      return false;
    }

    if (_isPublicAuthPath(path)) {
      return false;
    }

    if (_isAuthRefreshRequest(path)) {
      return false;
    }

    return true;
  }

  bool _isAllowedDuringTerminalFailure(String path) {
    return _isPublicAuthPath(path);
  }

  bool _isPublicAuthPath(String path) {
    return path.contains('/api/auth/login') ||
        path.contains('/api/auth/register') ||
        path.contains('/api/auth/forgot-password') ||
        path.contains('/api/auth/reset-password') ||
        path.contains('/api/auth/username-availability');
  }

  bool _isAuthRefreshRequest(String path) {
    return path.contains('/api/auth/refresh');
  }

  bool _isTerminalResettingSuccessPath(String path) {
    return path.contains('/api/auth/login') ||
        path.contains('/api/auth/register') ||
        path.contains('/api/auth/reset-password');
  }

  Future<String?> _refreshAccessToken() async {
    if (_terminalAuthFailure) {
      return null;
    }

    final tokens = await _tokenStorage.getTokens();

    if (tokens == null || !tokens.isValid) {
      _terminalAuthFailure = true;
      return null;
    }

    try {
      final response = await _refreshDio.post(
        '/api/auth/refresh',
        data: {
          'refreshToken': tokens.refreshToken,
        },
      );

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        _terminalAuthFailure = true;
        return null;
      }

      final newAccessToken = data['accessToken'] as String?;
      final newRefreshToken = data['refreshToken'] as String?;

      if (newAccessToken == null ||
          newAccessToken.isEmpty ||
          newRefreshToken == null ||
          newRefreshToken.isEmpty) {
        _terminalAuthFailure = true;
        return null;
      }

      await _tokenStorage.saveTokens(
        accessToken: newAccessToken,
        refreshToken: newRefreshToken,
      );

      _terminalAuthFailure = false;
      _forcedLogoutOperation = null;
      return newAccessToken;
    } on DioException catch (e) {
      final path = e.requestOptions.path.toLowerCase();

      if (_isAuthRefreshRequest(path)) {
        final statusCode = e.response?.statusCode;
        if (statusCode == 401 || statusCode == 403) {
          _terminalAuthFailure = true;
          return null;
        }
      }

      rethrow;
    } catch (_) {
      rethrow;
    }
  }

  Future<void> _forceLogoutOnce() async {
    _terminalAuthFailure = true;
    _forcedLogoutOperation ??= _forceLogout();
    await _forcedLogoutOperation;
  }

  static Future<void> _forceLogout() async {
    await AppSessionController.instance.logout();

    final navigator = appNavigatorKey.currentState;
    if (navigator == null) {
      return;
    }

    navigator.pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const AppBootstrap()),
      (route) => false,
    );
  }
}