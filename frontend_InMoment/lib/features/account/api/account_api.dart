import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_error.dart';
import '../models/account_data_summary.dart';
import '../models/account_deletion_request.dart';

class AccountApi {
  final _api = ApiClient.create();

  Future<AccountDataSummary> getDataSummary() async {
    try {
      final response = await _api.dio.get('/api/account/data-summary');
      final data = response.data;

      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера при загрузке данных аккаунта.');
      }

      return AccountDataSummary.fromJson(data);
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось загрузить данные аккаунта.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить данные аккаунта.',
        ),
      );
    }
  }

  Future<AccountDeletionRequest?> getDeletionRequest() async {
    try {
      final response = await _api.dio.get('/api/account/deletion-request');

      if (response.statusCode == 204) {
        return null;
      }

      final data = response.data;

      if (data == null) {
        return null;
      }

      if (data is! Map<String, dynamic>) {
        return null;
      }

      return AccountDeletionRequest.fromJson(
        Map<String, dynamic>.from(data),
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 204) {
        return null;
      }

      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось загрузить статус запроса на удаление.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить статус запроса на удаление.',
        ),
      );
    }
  }

  Future<AccountDeletionRequest> createDeletionRequest({
    String? note,
  }) async {
    try {
      final response = await _api.dio.post(
        '/api/account/deletion-request',
        data: {
          'note': note?.trim().isEmpty == true ? null : note?.trim(),
        },
      );

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw Exception(
          'Некорректный ответ сервера при создании запроса на удаление.',
        );
      }

      return AccountDeletionRequest.fromJson(
        Map<String, dynamic>.from(data),
      );
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось отправить запрос на удаление аккаунта и данных.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось отправить запрос на удаление аккаунта и данных.',
        ),
      );
    }
  }

  Future<List<AccountDeletionRequest>> getModerationDeletionRequests({
    int limit = 100,
    int? statusCode,
  }) async {
    try {
      final queryParameters = <String, dynamic>{
        'limit': limit,
      };

      if (statusCode != null) {
        queryParameters['status'] = statusCode;
      }

      final response = await _api.dio.get(
        '/api/account/deletion-requests',
        queryParameters: queryParameters,
      );

      final data = response.data;
      if (data is! List) {
        throw Exception(
          'Некорректный ответ сервера при загрузке запросов на удаление.',
        );
      }

      return data
          .whereType<Map<String, dynamic>>()
          .map(AccountDeletionRequest.fromJson)
          .toList();
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось загрузить запросы на удаление аккаунтов.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить запросы на удаление аккаунтов.',
        ),
      );
    }
  }

  Future<AccountDeletionRequest> getModerationDeletionRequestDetails(
    String requestId,
  ) async {
    try {
      final response = await _api.dio.get(
        '/api/account/deletion-requests/$requestId',
      );

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw Exception(
          'Некорректный ответ сервера при загрузке деталей запроса.',
        );
      }

      return AccountDeletionRequest.fromJson(
        Map<String, dynamic>.from(data),
      );
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось загрузить детали запроса на удаление.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить детали запроса на удаление.',
        ),
      );
    }
  }

  Future<AccountDeletionRequest> reviewDeletionRequest({
    required String requestId,
    required int statusCode,
    String? processingNote,
    bool permanentlyDeleteNow = false,
  }) async {
    try {
      final response = await _api.dio.patch(
        '/api/account/deletion-requests/$requestId',
        data: {
          'status': statusCode,
          'processingNote': processingNote?.trim().isEmpty == true
              ? null
              : processingNote?.trim(),
          'permanentlyDeleteNow': permanentlyDeleteNow,
        },
      );

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw Exception(
          'Некорректный ответ сервера при обработке запроса на удаление.',
        );
      }

      return AccountDeletionRequest.fromJson(
        Map<String, dynamic>.from(data),
      );
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось обработать запрос на удаление аккаунта.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось обработать запрос на удаление аккаунта.',
        ),
      );
    }
  }

  Future<void> deactivateAccount() async {
    try {
      await _api.dio.post('/api/account/deactivate');
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось деактивировать аккаунт.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось деактивировать аккаунт.',
        ),
      );
    }
  }

  Future<void> permanentlyDeleteAccount(String confirmation) async {
    try {
      await _api.dio.delete(
        '/api/account/permanent',
        data: {
          'confirmation': confirmation,
        },
      );
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось удалить аккаунт навсегда.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось удалить аккаунт навсегда.',
        ),
      );
    }
  }
}