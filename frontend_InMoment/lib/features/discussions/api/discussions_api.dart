import '../../../core/api/api_client.dart';
import '../models/discussion_item.dart';

class DiscussionsApi {
  final _api = ApiClient.create();

  Future<List<DiscussionItem>> getGroupDiscussions(String groupId) async {
    final response = await _api.dio.get('/api/groups/$groupId/discussions');

    final data = response.data;

    if (data is List) {
      return data
          .map((e) => DiscussionItem.fromJson(e as Map<String, dynamic>))
          .toList();
    }

    if (data is Map<String, dynamic>) {
      if (data['items'] is List) {
        final items = data['items'] as List;
        return items
            .map((e) => DiscussionItem.fromJson(e as Map<String, dynamic>))
            .toList();
      }

      if (data['data'] is List) {
        final items = data['data'] as List;
        return items
            .map((e) => DiscussionItem.fromJson(e as Map<String, dynamic>))
            .toList();
      }
    }

    throw Exception('Unexpected discussions response format');
  }
}