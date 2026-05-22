import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../models/report_item.dart';
import '../models/report_reason_option.dart';

class ReportsApi {
  final _api = ApiClient.create();

  Future<void> createReport({
    required ReportTargetType targetType,
    required String targetId,
    required ReportReasonOption reason,
    String? description,
  }) async {
    try {
      await _api.dio.post(
        '/api/reports',
        data: {
          'targetType': targetType.value,
          'targetId': targetId,
          'reason': reason.value,
          'description': _normalizeDescription(description),
        },
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось отправить жалобу.',
      ));
    }
  }

  Future<List<ReportItem>> getMyReports({int limit = 50}) async {
    try {
      final response = await _api.dio.get(
        '/api/reports/my',
        queryParameters: {'limit': limit},
      );

      final data = response.data;
      if (data is! List) {
        throw Exception('Некорректный ответ сервера при загрузке жалоб.');
      }

      return data
          .whereType<Map<String, dynamic>>()
          .map(ReportItem.fromJson)
          .toList();
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось загрузить мои жалобы.',
      ));
    }
  }

  Future<List<ReportItem>> getAllReports({
    int limit = 100,
    int? status,
    int? targetType,
    int? reason,
  }) async {
    try {
      final queryParameters = <String, dynamic>{
        'limit': limit,
      };

      if (status != null) {
        queryParameters['status'] = status;
      }
      if (targetType != null) {
        queryParameters['targetType'] = targetType;
      }
      if (reason != null) {
        queryParameters['reason'] = reason;
      }

      final response = await _api.dio.get(
        '/api/reports',
        queryParameters: queryParameters,
      );

      final data = response.data;
      if (data is! List) {
        throw Exception('Некорректный ответ сервера при загрузке списка жалоб.');
      }

      return data
          .whereType<Map<String, dynamic>>()
          .map(ReportItem.fromJson)
          .toList();
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось загрузить список жалоб.',
      ));
    }
  }

  Future<ReportItem> getReportDetails(String reportId) async {
    try {
      final response = await _api.dio.get('/api/reports/$reportId');
      final data = response.data;

      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера при загрузке жалобы.');
      }

      return ReportItem.fromJson(data);
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось загрузить детали жалобы.',
      ));
    }
  }

  Future<void> reviewReport({
    required String reportId,
    required ReportStatusOption status,
    required ReviewReportActionOption action,
  }) async {
    try {
      await _api.dio.patch(
        '/api/reports/$reportId',
        data: {
          'status': status.value,
          'action': action.value,
        },
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось применить решение по жалобе.',
      ));
    }
  }

  Future<void> appealReport({
    required String reportId,
    required String text,
  }) async {
    try {
      await _api.dio.post(
        '/api/reports/$reportId/appeal',
        data: {
          'text': text.trim(),
        },
      );
    } on DioException catch (e) {
      throw Exception(_extractErrorMessage(
        e,
        fallback: 'Не удалось отправить апелляцию.',
      ));
    }
  }

  String? _normalizeDescription(String? value) {
    if (value == null) return null;
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  String _extractErrorMessage(
    DioException error, {
    required String fallback,
  }) {
    final responseData = error.response?.data;

    if (responseData is Map<String, dynamic>) {
      final title = responseData['title'];
      if (title is String && title.trim().isNotEmpty) return title.trim();

      final detail = responseData['detail'];
      if (detail is String && detail.trim().isNotEmpty) return detail.trim();

      final message = responseData['message'];
      if (message is String && message.trim().isNotEmpty) return message.trim();

      final errors = responseData['errors'];
      if (errors is Map) {
        final messages = <String>[];

        for (final entry in errors.entries) {
          final value = entry.value;
          if (value is List) {
            for (final item in value) {
              if (item is String && item.trim().isNotEmpty) {
                messages.add(item.trim());
              }
            }
          }
        }

        if (messages.isNotEmpty) {
          return messages.join('\n');
        }
      }
    }

    return fallback;
  }
}