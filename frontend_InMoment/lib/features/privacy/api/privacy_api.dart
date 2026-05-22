import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_error.dart';
import '../models/privacy_settings.dart';

class PrivacyApi {
  final _api = ApiClient.create();

  Future<PrivacySettingsModel> getPrivacy() async {
    try {
      final response = await _api.dio.get('/api/privacy');
      final data = response.data;

      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера при загрузке приватности.');
      }

      return PrivacySettingsModel.fromJson(data);
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось загрузить настройки приватности.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить настройки приватности.',
        ),
      );
    }
  }

  Future<void> updatePrivacy(PrivacySettingsModel value) async {
    try {
      await _api.dio.patch(
        '/api/privacy',
        data: {
          'allowFriendRequestsFrom': value.allowFriendRequestsFrom.code,
          'allowGroupInvitesFrom': value.allowGroupInvitesFrom.code,
          'discoverableByContacts': value.discoverableByContacts,
          'discoverableBySearch': value.discoverableBySearch,
        },
      );
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось сохранить настройки приватности.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось сохранить настройки приватности.',
        ),
      );
    }
  }
}