import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_error.dart';

class InvitationsApi {
  final _api = ApiClient.create();

  Future<void> inviteByUserId({
    required String groupId,
    required String userId,
  }) async {
    try {
      await _api.dio.post(
        '/api/groups/$groupId/invite',
        data: {
          'invitedUserId': userId.trim(),
        },
      );
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось отправить приглашение.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось отправить приглашение.',
        ),
      );
    }
  }

  Future<void> acceptInvitation(String invitationId) async {
    try {
      await _api.dio.post('/api/invitations/$invitationId/accept');
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось принять приглашение.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось принять приглашение.',
        ),
      );
    }
  }

  Future<void> inviteByUserName({
    required String groupId,
    required String userName,
  }) async {
    try {
      await _api.dio.post(
        '/api/groups/$groupId/invite',
        data: {
          'login': userName.trim(),
        },
      );
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось отправить приглашение.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось отправить приглашение.',
        ),
      );
    }
  }

  Future<void> inviteByEmail({
    required String groupId,
    required String email,
  }) async {
    try {
      await _api.dio.post(
        '/api/groups/$groupId/invite',
        data: {
          'login': email.trim(),
        },
      );
    } on DioException catch (e) {
      throw Exception(
        ApiError.fromDio(
          e,
          fallback: 'Не удалось отправить приглашение.',
        ),
      );
    } catch (e) {
      throw Exception(
        ApiError.normalize(
          e,
          fallback: 'Не удалось отправить приглашение.',
        ),
      );
    }
  }
}