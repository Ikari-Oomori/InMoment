import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_error.dart';
import '../../../core/storage/token_storage.dart';
import '../models/session_item.dart';

class SessionsApi {
  final _api = ApiClient.create();
  final _tokenStorage = const TokenStorage();

  Future<Map<String, String>> _refreshHeader() async {
    final refreshToken = await _tokenStorage.getRefreshToken();

    return {
      if (refreshToken != null && refreshToken.isNotEmpty)
        'X-Refresh-Token': refreshToken,
    };
  }

  Future<List<SessionItem>> getSessions() async {
    try {
      final response = await _api.dio.get(
        '/api/sessions',
        options: Options(
          headers: await _refreshHeader(),
        ),
      );

      final data = response.data;

      if (data is List) {
        return data
            .whereType<Map>()
            .map((item) => SessionItem.fromJson(Map<String, dynamic>.from(item)))
            .toList(growable: false);
      }

      throw Exception('Некорректный ответ сервера при загрузке сессий.');
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось загрузить активные сессии.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить активные сессии.',
        ),
      );
    }
  }

  Future<void> revokeSession(String sessionId) async {
    try {
      await _api.dio.delete('/api/sessions/$sessionId');
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось завершить выбранную сессию.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось завершить выбранную сессию.',
        ),
      );
    }
  }

  Future<int> revokeOtherSessions() async {
    try {
      final response = await _api.dio.delete(
        '/api/sessions/others',
        options: Options(
          headers: await _refreshHeader(),
        ),
      );

      final data = response.data;
      if (data is Map<String, dynamic>) {
        return (data['revokedCount'] as num?)?.toInt() ?? 0;
      }

      return 0;
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось завершить все остальные сессии.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось завершить все остальные сессии.',
        ),
      );
    }
  }
}