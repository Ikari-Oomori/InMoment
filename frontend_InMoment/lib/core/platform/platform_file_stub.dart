import 'dart:async';

Future<bool> platformFileExists(String path) async => false;

Future<int?> platformFileLength(String path) async => null;

Future<Stream<List<int>>> platformFileOpenRead(String path) async {
  throw UnsupportedError('Local file streams are not supported on this platform.');
}

Future<void> platformDeleteFileIfExists(String path) async {}