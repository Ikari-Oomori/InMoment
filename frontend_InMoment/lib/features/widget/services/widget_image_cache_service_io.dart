import 'dart:async';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:path_provider/path_provider.dart';
import 'package:video_compress/video_compress.dart';

import '../../../core/config/env.dart';

class WidgetImageCacheResult {
  final String? cachedPath;
  final String? sourceUrl;

  const WidgetImageCacheResult({
    required this.cachedPath,
    required this.sourceUrl,
  });
}

class WidgetImageCacheService {
  WidgetImageCacheService._();

  static final WidgetImageCacheService instance = WidgetImageCacheService._();

  final Dio _dio = Dio();

  Future<WidgetImageCacheResult> cachePreview({
    required String? imageUrl,
  }) async {
    final safeUrl = _normalizeUrlForAndroidDevice(imageUrl?.trim());
    if (safeUrl == null || safeUrl.isEmpty) {
      await clearPreviewCache();

      return const WidgetImageCacheResult(
        cachedPath: null,
        sourceUrl: null,
      );
    }

    try {
      final dir = await getTemporaryDirectory();
      final targetFile = File('${dir.path}/widget_latest_preview_v2.png');

      final response = await _dio.get<List<int>>(
        safeUrl,
        options: Options(
          responseType: ResponseType.bytes,
          receiveTimeout: const Duration(seconds: 20),
          sendTimeout: const Duration(seconds: 20),
          headers: const {
            'Accept': 'image/*',
          },
        ),
      );

      final bytes = response.data;
      if (bytes == null || bytes.isEmpty) {
        return WidgetImageCacheResult(
          cachedPath: await _existingPathOrNull(targetFile),
          sourceUrl: safeUrl,
        );
      }

      final preview = await _buildSquareCoverPreview(Uint8List.fromList(bytes));
      if (preview == null || preview.isEmpty) {
        return WidgetImageCacheResult(
          cachedPath: await _existingPathOrNull(targetFile),
          sourceUrl: safeUrl,
        );
      }

      await targetFile.writeAsBytes(
        preview,
        flush: true,
      );

      return WidgetImageCacheResult(
        cachedPath: targetFile.path,
        sourceUrl: safeUrl,
      );
    } catch (e) {
      if (kDebugMode) {
        debugPrint('[WidgetImageCacheService] Не удалось закешировать медиа для виджета: $e');
      }

      try {
        final dir = await getTemporaryDirectory();
        final targetFile = File('${dir.path}/widget_latest_preview_v2.png');

        return WidgetImageCacheResult(
          cachedPath: await _existingPathOrNull(targetFile),
          sourceUrl: safeUrl,
        );
      } catch (_) {
        return WidgetImageCacheResult(
          cachedPath: null,
          sourceUrl: safeUrl,
        );
      }
    }
  }

  Future<WidgetImageCacheResult> cacheVideoPreview({
    required String? videoUrl,
  }) async {
    final safeUrl = _normalizeUrlForAndroidDevice(videoUrl?.trim());
    if (safeUrl == null || safeUrl.isEmpty) {
      await clearPreviewCache();

      return const WidgetImageCacheResult(
        cachedPath: null,
        sourceUrl: null,
      );
    }

    File? sourceFile;

    try {
      final dir = await getTemporaryDirectory();
      sourceFile = File('${dir.path}/widget_latest_video_source.tmp');
      final targetFile = File('${dir.path}/widget_latest_preview_v2.png');

      await _dio.download(
        safeUrl,
        sourceFile.path,
        options: Options(
          receiveTimeout: const Duration(seconds: 24),
          sendTimeout: const Duration(seconds: 20),
          headers: const {
            'Accept': 'video/*,*/*',
          },
        ),
      );

      if (!await sourceFile.exists() || await sourceFile.length() <= 0) {
        return WidgetImageCacheResult(
          cachedPath: await _existingPathOrNull(targetFile),
          sourceUrl: safeUrl,
        );
      }

      final thumbnail = await VideoCompress.getByteThumbnail(
        sourceFile.path,
        quality: 74,
        position: 1200,
      );

      if (thumbnail == null || thumbnail.isEmpty) {
        return WidgetImageCacheResult(
          cachedPath: await _existingPathOrNull(targetFile),
          sourceUrl: safeUrl,
        );
      }

      final preview = await _buildSquareCoverPreview(thumbnail);
      if (preview == null || preview.isEmpty) {
        return WidgetImageCacheResult(
          cachedPath: await _existingPathOrNull(targetFile),
          sourceUrl: safeUrl,
        );
      }

      await targetFile.writeAsBytes(preview, flush: true);

      return WidgetImageCacheResult(
        cachedPath: targetFile.path,
        sourceUrl: safeUrl,
      );
    } catch (e) {
      if (kDebugMode) {
        debugPrint('[WidgetImageCacheService] Не удалось подготовить превью видео для виджета: $e');
      }

      try {
        final dir = await getTemporaryDirectory();
        final targetFile = File('${dir.path}/widget_latest_preview_v2.png');

        return WidgetImageCacheResult(
          cachedPath: await _existingPathOrNull(targetFile),
          sourceUrl: safeUrl,
        );
      } catch (_) {
        return WidgetImageCacheResult(
          cachedPath: null,
          sourceUrl: safeUrl,
        );
      }
    } finally {
      try {
        if (sourceFile != null && await sourceFile.exists()) {
          await sourceFile.delete();
        }
      } catch (_) {}
    }
  }

  String? _normalizeUrlForAndroidDevice(String? rawUrl) {
    final value = rawUrl?.trim();
    if (value == null || value.isEmpty) return null;

    final uri = Uri.tryParse(value);
    if (uri == null || !uri.hasScheme || !uri.hasAuthority) {
      return value;
    }

    final host = uri.host.toLowerCase();
    final pointsToLocalhost = host == 'localhost' || host == '127.0.0.1' || host == '0.0.0.0';
    if (!pointsToLocalhost) {
      return value;
    }

    final apiUri = Uri.tryParse(Env.baseUrl);
    if (apiUri == null || apiUri.host.trim().isEmpty) {
      return value;
    }

    return uri.replace(host: apiUri.host).toString();
  }

  Future<String?> _existingPathOrNull(File file) async {
    final exists = await file.exists();
    return exists ? file.path : null;
  }

  Future<Uint8List?> _buildSquareCoverPreview(Uint8List bytes) async {
    try {
      final source = await _decodeImage(bytes);
      const outputSize = 720.0;
      final sourceWidth = source.width.toDouble();
      final sourceHeight = source.height.toDouble();

      if (sourceWidth <= 0 || sourceHeight <= 0) {
        source.dispose();
        return null;
      }

      final sourceAspect = sourceWidth / sourceHeight;
      const targetAspect = 1.0;

      late final ui.Rect src;
      if (sourceAspect > targetAspect) {
        final cropWidth = sourceHeight * targetAspect;
        final left = (sourceWidth - cropWidth) / 2;
        src = ui.Rect.fromLTWH(left, 0, cropWidth, sourceHeight);
      } else {
        final cropHeight = sourceWidth / targetAspect;
        final top = (sourceHeight - cropHeight) / 2;
        src = ui.Rect.fromLTWH(0, top, sourceWidth, cropHeight);
      }

      final recorder = ui.PictureRecorder();
      final canvas = ui.Canvas(recorder);
      final paint = ui.Paint()
        ..isAntiAlias = true
        ..filterQuality = ui.FilterQuality.high;

      final dst = const ui.Rect.fromLTWH(0, 0, outputSize, outputSize);
      final roundedDst = ui.RRect.fromRectAndRadius(
        dst,
        const ui.Radius.circular(80),
      );

      canvas.save();
      canvas.clipRRect(roundedDst);
      canvas.drawImageRect(source, src, dst, paint);
      canvas.restore();

      final picture = recorder.endRecording();
      final image = await picture.toImage(outputSize.toInt(), outputSize.toInt());
      final byteData = await image.toByteData(format: ui.ImageByteFormat.png);

      source.dispose();
      image.dispose();
      picture.dispose();

      return byteData?.buffer.asUint8List();
    } catch (_) {
      return null;
    }
  }

  Future<ui.Image> _decodeImage(Uint8List bytes) {
    final completer = Completer<ui.Image>();

    ui.decodeImageFromList(bytes, (image) {
      if (!completer.isCompleted) {
        completer.complete(image);
      }
    });

    return completer.future;
  }

  Future<void> clearPreviewCache() async {
    try {
      final dir = await getTemporaryDirectory();
      final files = [
        File('${dir.path}/widget_latest_preview.png'),
        File('${dir.path}/widget_latest_preview_v2.png'),
        File('${dir.path}/widget_latest_preview_v3.png'),
        File('${dir.path}/widget_latest_video_source.tmp'),
      ];

      for (final file in files) {
        if (await file.exists()) {
          await file.delete();
        }
      }
    } catch (_) {
      // cache cleanup should not break app flow
    }
  }
}