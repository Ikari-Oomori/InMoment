import 'dart:async';
import 'dart:io';

Future<bool> platformFileExists(String path) async {
  return File(path).exists();
}

Future<int?> platformFileLength(String path) async {
  final file = File(path);
  if (!await file.exists()) return null;
  return file.length();
}

Future<Stream<List<int>>> platformFileOpenRead(String path) async {
  final file = File(path);

  if (!await file.exists()) {
    throw Exception('Файл для загрузки не найден.');
  }

  return file.openRead();
}

Future<void> platformDeleteFileIfExists(String path) async {
  final file = File(path);
  if (await file.exists()) {
    await file.delete();
  }
}