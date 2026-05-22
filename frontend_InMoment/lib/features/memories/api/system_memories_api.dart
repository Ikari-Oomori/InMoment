import '../../../core/api/api_client.dart';
import '../models/system_memory.dart';

class SystemMemoriesApi {
  final _dio = ApiClient.create().dio;

  Future<List<SystemMemory>> list() async {
    final response = await _dio.get('/api/system-memories');
    final data = response.data;

    if (data is List) {
      return data
          .whereType<Map>()
          .map((item) => SystemMemory.fromJson(
                item.map((key, value) => MapEntry(key.toString(), value)),
              ))
          .toList();
    }

    return const [];
  }

  Future<SystemMemory> getById(String id) async {
    final response = await _dio.get('/api/system-memories/$id');
    final data = response.data;

    if (data is Map) {
      return SystemMemory.fromJson(
        data.map((key, value) => MapEntry(key.toString(), value)),
      );
    }

    throw Exception('Не удалось загрузить воспоминание');
  }

  Future<void> markViewed(String id) async {
    await _dio.post('/api/system-memories/$id/viewed');
  }
}
