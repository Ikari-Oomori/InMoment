import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_error.dart';
import '../models/user_search_item.dart';

class SearchApi {
  final _api = ApiClient.create();

  Future<List<UserSearchItem>> searchUsers(
    String query, {
    int limit = 10,
  }) async {
    try {
      final response = await _api.dio.get(
        '/api/search/users',
        queryParameters: {
          'q': query.trim(),
          'limit': limit,
        },
      );

      final data = response.data;

      if (data is List) {
        return data
            .whereType<Map>()
            .map(
              (e) => UserSearchItem.fromJson(
                Map<String, dynamic>.from(e),
              ),
            )
            .toList(growable: false);
      }

      return const [];
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Ошибка поиска пользователей.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Ошибка поиска пользователей.',
        ),
      );
    }
  }

  Future<List<UserSearchItem>> searchUsersFlexible(
    String query, {
    int limit = 10,
  }) async {
    final trimmed = query.trim();
    if (trimmed.isEmpty) return const [];

    if (_looksLikeEmail(trimmed) || _looksLikePhone(trimmed)) {
      return _lookupUsersByEmailOrPhone(trimmed);
    }

    return searchUsers(trimmed, limit: limit);
  }

  Future<List<UserSearchItem>> _lookupUsersByEmailOrPhone(String query) async {
    try {
      final isEmail = _looksLikeEmail(query);
      final isPhone = _looksLikePhone(query);

      final response = await _api.dio.post(
        '/api/contacts/import',
        data: {
          'contacts': [
            {
              'displayName': query.trim(),
              'phones': isPhone ? [query.trim()] : <String>[],
              'emails': isEmail ? [query.trim()] : <String>[],
            }
          ],
        },
      );

      final data = response.data;
      if (data is! Map) {
        return const [];
      }

      final map = Map<String, dynamic>.from(data);
      final matches = map['matches'];

      if (matches is! List) {
        return const [];
      }

      return matches
          .whereType<Map>()
          .map(
            (item) => UserSearchItem.fromContactMatchJson(
              Map<String, dynamic>.from(item),
            ),
          )
          .toList(growable: false);
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось выполнить поиск по email или телефону.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось выполнить поиск по email или телефону.',
        ),
      );
    }
  }

  bool _looksLikeEmail(String value) {
    final email = value.trim();
    if (email.isEmpty) return false;

    final regex = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
    return regex.hasMatch(email);
  }

  bool _looksLikePhone(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) return false;

    final normalized = trimmed.replaceAll(RegExp(r'[^\d+]'), '');
    final regex = RegExp(r'^\+?\d{7,15}$');
    return regex.hasMatch(normalized);
  }
}