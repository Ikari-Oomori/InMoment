import '../../../core/api/api_client.dart';
import '../models/gif_search_item.dart';

class GifsApi {
  final _api = ApiClient.create();

  Future<List<GifSearchItem>> search({
    required String query,
    int limit = 18,
  }) async {
    final response = await _api.dio.get(
      '/api/gifs/search',
      queryParameters: {
        'query': query.trim().isEmpty ? 'cute' : query.trim(),
        'limit': limit,
      },
    );

    final data = response.data;
    if (data is! List) return const [];

    return data
        .map((e) => GifSearchItem.fromJson(e as Map<String, dynamic>))
        .where((e) => e.previewUrl.isNotEmpty && e.gifUrl.isNotEmpty)
        .toList();
  }
}