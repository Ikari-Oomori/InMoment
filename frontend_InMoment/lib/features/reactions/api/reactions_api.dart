import '../../../core/api/api_client.dart';

class ReactionsApi {
  final _api = ApiClient.create();

  Future<void> setReaction({
    required String photoId,
    required int type,
  }) async {
    await _api.dio.post(
      '/api/photos/$photoId/reactions',
      data: {
        'type': type,
      },
    );
  }

  Future<void> removeReaction({
    required String photoId,
  }) async {
    await _api.dio.delete('/api/photos/$photoId/reactions');
  }

  Future<Map<String, dynamic>> getSummary({
    required String photoId,
  }) async {
    final response = await _api.dio.get('/api/photos/$photoId/reactions');
    final data = response.data;

    if (data is Map<String, dynamic>) {
      return data;
    }

    throw Exception('Unexpected reactions summary response format');
  }
}