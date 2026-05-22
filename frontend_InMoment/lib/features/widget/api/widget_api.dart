import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../models/widget_snapshot.dart';

class WidgetApi {
  final _api = ApiClient.create();

  Future<WidgetSnapshot> getWidgetSnapshot() async {
    try {
      final response = await _api.dio.get('/api/users/me/widget');

      final data = response.data;
      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера для виджета.');
      }

      return WidgetSnapshot.fromJson(data);
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось загрузить данные виджета.',
      ));
    }
  }

  String _extractErrorMessage(
    DioException error, {
    required String fallback,
  }) {
    final responseData = error.response?.data;

    if (responseData is Map<String, dynamic>) {
      final title = responseData['title'];
      if (title is String && title.trim().isNotEmpty) {
        return title.trim();
      }

      final detail = responseData['detail'];
      if (detail is String && detail.trim().isNotEmpty) {
        return detail.trim();
      }
    }

    if (error.message != null && error.message!.trim().isNotEmpty) {
      return error.message!.trim();
    }

    return fallback;
  }
}