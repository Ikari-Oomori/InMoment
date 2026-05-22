import '../../../core/api/api_client.dart';
import '../models/feed_item.dart';

class FeedApi {
  final _api = ApiClient.create();

  Future<List<FeedItem>> getGroupFeed(String groupId) async {
    final response = await _api.dio.get('/api/groups/$groupId/feed/paged');

    final data = response.data;

    if (data is List) {
      return data
          .map((e) => FeedItem.fromJson(e as Map<String, dynamic>))
          .where((item) => item.url.trim().isNotEmpty)
          .toList();
    }

    if (data is Map<String, dynamic>) {
      if (data['items'] is List) {
        final items = data['items'] as List;
        return items
            .map((e) => FeedItem.fromJson(e as Map<String, dynamic>))
            .where((item) => item.url.trim().isNotEmpty)
            .toList();
      }

      if (data['data'] is List) {
        final items = data['data'] as List;
        return items
            .map((e) => FeedItem.fromJson(e as Map<String, dynamic>))
            .where((item) => item.url.trim().isNotEmpty)
            .toList();
      }
    }

    throw Exception('Unexpected feed response format');
  }
}