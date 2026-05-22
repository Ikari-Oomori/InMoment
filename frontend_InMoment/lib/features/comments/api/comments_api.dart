import '../../../core/api/api_client.dart';
import '../models/comment_item.dart';

class CommentsApi {
  final _api = ApiClient.create();

  Future<List<CommentItem>> getComments(String photoId) async {
    final response = await _api.dio.get('/api/photos/$photoId/comments/paged');

    final data = response.data;

    if (data is List) {
      return data
          .map((e) => CommentItem.fromJson(e as Map<String, dynamic>))
          .toList();
    }

    if (data is Map<String, dynamic>) {
      if (data['items'] is List) {
        final items = data['items'] as List;
        return items
            .map((e) => CommentItem.fromJson(e as Map<String, dynamic>))
            .toList();
      }

      if (data['data'] is List) {
        final items = data['data'] as List;
        return items
            .map((e) => CommentItem.fromJson(e as Map<String, dynamic>))
            .toList();
      }
    }

    throw Exception('Unexpected comments response format');
  }

  Future<void> createComment({
    required String photoId,
    required String text,
    String? gifUrl,
  }) async {
    await _api.dio.post(
      '/api/photos/$photoId/comments',
      data: {
        'text': text,
        if (gifUrl != null && gifUrl.trim().isNotEmpty)
          'gifUrl': gifUrl.trim(),
      },
    );
  }

  Future<void> replyToComment({
    required String photoId,
    required String parentCommentId,
    required String text,
    String? gifUrl,
  }) async {
    await _api.dio.post(
      '/api/photos/$photoId/comments/reply',
      data: {
        'parentCommentId': parentCommentId,
        'text': text,
        if (gifUrl != null && gifUrl.trim().isNotEmpty)
          'gifUrl': gifUrl.trim(),
      },
    );
  }

  Future<void> editComment({
    required String commentId,
    required String text,
  }) async {
    await _api.dio.patch(
      '/api/comments/$commentId',
      data: {
        'text': text,
      },
    );
  }

  Future<void> deleteComment({
    required String commentId,
  }) async {
    await _api.dio.delete('/api/comments/$commentId');
  }

  Future<void> setCommentReaction({
    required String commentId,
    required int type,
  }) async {
    await _api.dio.post(
      '/api/comments/$commentId/reactions',
      data: {
        'type': type,
      },
    );
  }

  Future<void> removeCommentReaction({
    required String commentId,
  }) async {
    await _api.dio.delete('/api/comments/$commentId/reactions');
  }
}