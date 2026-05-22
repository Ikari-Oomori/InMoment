import 'dart:ui';
import 'dart:math' as math;

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:video_player/video_player.dart';

import '../../../core/api/api_error.dart';
import '../../../core/widgets/inmoment_feedback.dart';
import '../../../core/platform/platform_file.dart';
import '../../../core/platform/video_player_controller_factory.dart';
import '../../../core/layout/inmoment_media_frame.dart';
import '../../mentions/widgets/mention_text_field.dart';
import '../../widget/services/widget_sync_service.dart';
import '../api/publish_api.dart';

class PhotoPreviewPage extends StatefulWidget {
  final String groupId;
  final String groupName;
  final Uint8List? bytes;
  final String? localFilePath;
  final int sizeBytes;
  final String contentType;
  final String fileName;

  const PhotoPreviewPage({
    super.key,
    required this.groupId,
    required this.groupName,
    required this.contentType,
    required this.fileName,
    required this.sizeBytes,
    this.bytes,
    this.localFilePath,
  });

  @override
  State<PhotoPreviewPage> createState() => _PhotoPreviewPageState();
}

class _PhotoPreviewPageState extends State<PhotoPreviewPage> {
  static const Color _bg = Color(0xFF09040D);
  static const Color _panel = Color(0xFF100915);
  static const Color _panelGlass = Color(0x24100915);
  static const Color _stroke = Color(0xFF4B3458);
  static const Color _strokeSoft = Color(0xFF3C2A47);
  static const Color _text = Color(0xFFE9D9F0);
  static const Color _textSoft = Color(0xFFB79CC4);
  static const Color _accentSoft = Color(0xFFB38BC6);
  //static const Color _error = Color(0xFFE08BA2);

  final _captionController = TextEditingController();
  final _captionFocusNode = FocusNode();
  final _api = PublishApi();

  CancelToken? _uploadCancelToken;
  VideoPlayerController? _videoController;
  VoidCallback? _trimPlaybackListener;
  int _videoPreviewGeneration = 0;

  bool _preparingVideo = false;
  String? _videoPreviewError;
  bool _publishing = false;
  double _uploadProgress = 0;
  bool get _trimSaving => false;
  bool _trimPlaying = false;
  bool _isMuted = true;

  double _trimStartValue = 0.0;
  double _trimEndValue = 0.0;

  String? _trimmedVideoPath;
  Duration? _videoDuration;

  _DragThumb? _activeDragThumb;
  bool _showTrimBubble = false;
  double _bubbleValueSeconds = 0.0;

  static const int _maxCaptionLength = 300;
  static const Duration _maxVideoDuration = Duration(minutes: 2);

  bool get _isVideo => widget.contentType.toLowerCase().startsWith('video/');

  bool get _hasLocalVideo =>
      !kIsWeb &&
      widget.localFilePath != null &&
      widget.localFilePath!.trim().isNotEmpty;

  bool get _hasTrimmedVersion =>
      _trimmedVideoPath != null && _trimmedVideoPath!.trim().isNotEmpty;

  bool get _videoRequiresTrim {
    if (!_isVideo) return false;
    if (_hasTrimmedVersion) return false;
    final duration = _videoDuration;
    if (duration == null) return false;
    return duration > _maxVideoDuration;
  }

  bool get _shouldSendVideoTrim {
    if (!_isVideo) return false;

    final total = _totalVideoSeconds;
    if (total <= 0) return false;

    final start = _trimStartValue.clamp(0.0, total).toDouble();
    final end = _trimEndValue.clamp(start, total).toDouble();

    if (end <= start) return false;

    const toleranceSeconds = 0.50;
    final trimsFromStart = start > toleranceSeconds;
    final trimsFromEnd = (total - end).abs() > toleranceSeconds;

    return _videoRequiresTrim || trimsFromStart || trimsFromEnd;
  }

  int? get _trimStartMsForPublish {
    if (!_shouldSendVideoTrim) return null;
    return (_trimStartValue * 1000).round();
  }

  int? get _trimEndMsForPublish {
    if (!_shouldSendVideoTrim) return null;
    return (_trimEndValue * 1000).round();
  }

  bool get _canPublish =>
      !_publishing &&
      !_trimSaving &&
      (!_isVideo || !_videoRequiresTrim);

  @override
  void initState() {
    super.initState();
    _captionController.addListener(_handleComposerStateChanged);
    _captionFocusNode.addListener(_handleComposerStateChanged);

    if (_isVideo) {
      _prepareVideoPreview();
    }
  }

  void _handleComposerStateChanged() {
    if (!mounted) return;
    setState(() {});
  }

  Future<void> _prepareVideoPreview({String? overridePath}) async {
    if (!mounted) return;

    final generation = ++_videoPreviewGeneration;

    setState(() {
      _preparingVideo = true;
      _videoPreviewError = null;
      _trimPlaying = false;
    });

    try {
      late final VideoPlayerController controller;

      final localPath = overridePath ??
          (_hasTrimmedVersion
              ? _trimmedVideoPath
              : (_hasLocalVideo ? widget.localFilePath : null));

      if (!kIsWeb && localPath != null && localPath.trim().isNotEmpty) {
        controller = createLocalVideoController(localPath);
      } else if (widget.bytes != null) {
        controller = VideoPlayerController.networkUrl(
          Uri.dataFromBytes(
            widget.bytes!,
            mimeType: widget.contentType,
          ),
        );
      } else {
        throw Exception('Нет источника для предпросмотра видео');
      }

      await controller.initialize();
      await controller.setLooping(false);
      await controller.setVolume(_isMuted ? 0 : 1);

      if (!mounted || generation != _videoPreviewGeneration) {
        await controller.dispose();
        return;
      }

      _detachTrimPlaybackListener();

      final oldController = _videoController;
      _videoController = controller;
      await oldController?.dispose();

      final duration = controller.value.duration;
      final totalSeconds = math.max(0.0, duration.inMilliseconds / 1000.0);

      double nextStart = _trimStartValue;
      double nextEnd = _trimEndValue;

      if (totalSeconds <= 0) {
        nextStart = 0;
        nextEnd = 0;
      } else if (!_hasTrimmedVersion) {
        final defaultEnd = math.min(
          totalSeconds,
          _maxVideoDuration.inMilliseconds / 1000.0,
        );

        if (nextEnd <= 0 || nextEnd > totalSeconds) {
          nextEnd = defaultEnd;
        }

        if (nextStart < 0 || nextStart >= nextEnd) {
          nextStart = 0;
        }

        if (nextEnd - nextStart > _maxVideoDuration.inSeconds) {
          nextStart = 0;
          nextEnd = defaultEnd;
        }
      } else {
        nextStart = 0;
        nextEnd = totalSeconds;
      }

      if (!mounted || generation != _videoPreviewGeneration) {
        await controller.dispose();
        return;
      }

      setState(() {
        _videoDuration = duration;
        _trimStartValue = nextStart.clamp(0.0, totalSeconds).toDouble();
        _trimEndValue = nextEnd.clamp(_trimStartValue, totalSeconds).toDouble();
        _preparingVideo = false;
      });
    } catch (_) {
      if (!mounted || generation != _videoPreviewGeneration) return;
      setState(() {
        _preparingVideo = false;
        _videoPreviewError = 'Не удалось открыть видео';
      });
    }
  }

  void _detachTrimPlaybackListener() {
    final controller = _videoController;
    final listener = _trimPlaybackListener;
    if (controller != null && listener != null) {
      controller.removeListener(listener);
    }
    _trimPlaybackListener = null;
  }

  Future<void> _pauseVideo() async {
    final controller = _videoController;
    if (controller == null || !controller.value.isInitialized) return;

    if (controller.value.isPlaying) {
      await controller.pause();
    }

    if (!mounted) return;
    setState(() {
      _trimPlaying = false;
    });
  }

  Future<void> _toggleVideoPlayback() async {
    final controller = _videoController;
    if (controller == null || !controller.value.isInitialized) return;

    if (controller.value.isPlaying) {
      await controller.pause();
    } else {
      await controller.play();
    }

    if (!mounted) return;
    setState(() {});
  }

  Future<void> _toggleMute() async {
    final controller = _videoController;
    if (controller == null || !controller.value.isInitialized) return;

    final nextMuted = !_isMuted;
    await controller.setVolume(nextMuted ? 0 : 1);

    if (!mounted) return;
    setState(() {
      _isMuted = nextMuted;
    });
  }

  Future<void> _toggleTrimPlayback() async {
    final controller = _videoController;
    if (controller == null ||
        !controller.value.isInitialized ||
        _trimSaving ||
        _totalVideoSeconds <= 0) {
      return;
    }

    final start = _trimStartValue.clamp(0.0, _totalVideoSeconds).toDouble();
    final end = _trimEndValue.clamp(start, _totalVideoSeconds).toDouble();

    if (end <= start) return;

    if (controller.value.isPlaying && _trimPlaying) {
      await controller.pause();
      _detachTrimPlaybackListener();
      if (!mounted) return;
      setState(() {
        _trimPlaying = false;
      });
      return;
    }

    _detachTrimPlaybackListener();

    await controller.pause();
    await controller.seekTo(Duration(milliseconds: (start * 1000).round()));

    late final VoidCallback listener;
    listener = () {
      final position = controller.value.position;
      final endDuration = Duration(milliseconds: (end * 1000).round());

      if (position >= endDuration && controller.value.isPlaying) {
        controller.pause();
        controller.seekTo(Duration(milliseconds: (start * 1000).round()));
        _detachTrimPlaybackListener();
        if (mounted) {
          setState(() {
            _trimPlaying = false;
          });
        }
      }
    };

    _trimPlaybackListener = listener;
    controller.addListener(listener);

    await controller.play();

    if (!mounted) return;
    setState(() {
      _trimPlaying = true;
    });
  }

  Future<void> _cancelPublishing() async {
    final token = _uploadCancelToken;
    if (token != null && !token.isCancelled) {
      token.cancel('cancelled by user');
    }

    if (!mounted) return;

    setState(() {
      _publishing = false;
      _uploadCancelToken = null;
    });

    InMomentFeedback.showInfo(context, 'Публикация отменена');
  }

  Future<void> _publish() async {
    if (_publishing || _trimSaving) return;

    if (_isVideo) {
      final selectedDuration = _selectedTrimDuration();

      if (selectedDuration <= Duration.zero ||
          selectedDuration > _maxVideoDuration) {
        InMomentFeedback.showError(
          context,
          'Фрагмент должен быть не длиннее 2 минут',
        );
        return;
      }
    } else if (!_canPublish) {
      return;
    }

    FocusScope.of(context).unfocus();

    final caption = _captionController.text.trim();
    final cancelToken = CancelToken();

    final localFilePathToUpload = widget.localFilePath;

    int sizeBytesToUpload = widget.sizeBytes;

    if (!kIsWeb &&
        localFilePathToUpload != null &&
        localFilePathToUpload.trim().isNotEmpty) {
      final fileLength = await platformFileLength(localFilePathToUpload);
      if (fileLength != null) {
        sizeBytesToUpload = fileLength;
      }
    }

    setState(() {
      _publishing = true;
      _uploadProgress = 0;
      _uploadCancelToken = cancelToken;
    });

    try {
      final presign = await _api.presign(
        groupId: widget.groupId,
        contentType: widget.contentType,
      );

      if (!mounted) return;

      final shouldUploadBytes =
      kIsWeb || localFilePathToUpload == null || localFilePathToUpload.trim().isEmpty;

      await _api.uploadToStorage(
        uploadUrl: presign.uploadUrl,
        contentType: widget.contentType,
        cancelToken: cancelToken,
        contentLength: sizeBytesToUpload,
        bytes: shouldUploadBytes ? widget.bytes : null,
        localFilePath: shouldUploadBytes ? null : localFilePathToUpload,
        onProgress: (value) {
          if (!mounted) return;
          setState(() {
            _uploadProgress = value;
          });
        },
      );

      if (!mounted) return;

      await _api.createPhoto(
        groupId: widget.groupId,
        storageKey: presign.storageKey,
        contentType: widget.contentType,
        sizeBytes: sizeBytesToUpload,
        caption: caption.isEmpty ? null : caption,
        trimStartMs: _trimStartMsForPublish,
        trimEndMs: _trimEndMsForPublish,
      );

      await WidgetSyncService.instance.syncFromBackend();

      if (!mounted) return;
      Navigator.of(context).pop(true);
    } catch (e) {
      if (!mounted) return;

      final message = ApiError.normalize(
        e,
        fallback: 'Не удалось опубликовать материал. Попробуйте ещё раз.',
      );

      if (message == 'Запрос был отменён.' || message == 'Загрузка отменена.') {
        InMomentFeedback.showInfo(context, 'Загрузка отменена');
      } else {
        InMomentFeedback.showError(context, 'Ошибка публикации: $message');
      }
    } finally {
      if (mounted) {
        setState(() {
          _publishing = false;
          _uploadProgress = 0;
          _uploadCancelToken = null;
        });
      }
    }
  }

  String _formatSize(int bytes) {
    if (bytes < 1024) return '$bytes Б';
    if (bytes < 1024 * 1024) {
      return '${(bytes / 1024).toStringAsFixed(1)} КБ';
    }
    return '${(bytes / (1024 * 1024)).toStringAsFixed(1)} МБ';
  }

  Duration _selectedTrimDuration() {
    final milliseconds = ((_trimEndValue - _trimStartValue) * 1000).round();
    return Duration(milliseconds: milliseconds.clamp(0, 24 * 60 * 60 * 1000));
  }

  String _formatDuration(Duration duration) {
    final totalSeconds = duration.inSeconds;
    final minutes = totalSeconds ~/ 60;
    final seconds = totalSeconds % 60;
    final mm = minutes.toString().padLeft(2, '0');
    final ss = seconds.toString().padLeft(2, '0');
    return '$mm:$ss';
  }

  double get _totalVideoSeconds {
    final duration = _videoDuration;
    if (duration == null) return 0;
    return duration.inMilliseconds / 1000;
  }

  @override
  void dispose() {
    _videoPreviewGeneration++;
    _uploadCancelToken?.cancel();
    _detachTrimPlaybackListener();
    _videoController?.dispose();
    _captionController.removeListener(_handleComposerStateChanged);
    _captionFocusNode.removeListener(_handleComposerStateChanged);
    _captionController.dispose();
    _captionFocusNode.dispose();

    final trimmedPath = _trimmedVideoPath;
    if (trimmedPath != null && trimmedPath.trim().isNotEmpty) {
      try {
        platformDeleteFileIfExists(trimmedPath);
      } catch (_) {}
    }

    super.dispose();
  }

 @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    final contentWidth = InMomentMediaFrame.resolveTabletContentWidth(
      media.size.width,
    );
    final previewFrame = InMomentMediaFrame.resolveShellFrame(
      viewportWidth: media.size.width,
      viewportHeight: media.size.height,
      availableHeight: media.size.height * 0.58,
    );

    final previewWidth = previewFrame.width;
    final previewHeight = previewFrame.height;
    final imagePreviewHeight = previewFrame.height;

    return Scaffold(
      resizeToAvoidBottomInset: true,
      backgroundColor: _bg,
      body: SafeArea(
        child: Center(
          child: SizedBox(
            width: contentWidth,
            child: Column(
              children: [
                Expanded(
                  child: ListView(
                    padding: const EdgeInsets.fromLTRB(6, 6, 6, 14),
                    physics: const ClampingScrollPhysics(),
                    children: [
                      _buildHeader(title: _isVideo ? 'ВИДЕО' : 'ФОТО'),
                      const SizedBox(height: 10),
                      _buildTopPills(),
                      const SizedBox(height: 12),

                      if (_isVideo) ...[
                        Center(
                          child: SizedBox(
                            width: previewWidth,
                            height: previewHeight,
                            child: _buildPreviewStage(),
                          ),
                        ),
                        const SizedBox(height: 14),
                        _buildTimelineSection(),
                      ] else ...[
                        Center(
                          child: SizedBox(
                            width: previewWidth,
                            height: imagePreviewHeight,
                            child: _buildImagePreviewStage(),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(14, 0, 14, 14),
                  child: _buildBottomComposer(),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildHeader({String title = 'ВИДЕО'}) {
    return SizedBox(
      height: 42,
      child: Row(
        children: [
          _TopCircleButton(
            onTap: _publishing ? _cancelPublishing : () => Navigator.pop(context),
            icon: Icons.arrow_back_ios_new_rounded,
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Center(
              child: Text(
                title,
                style: const TextStyle(
                  color: _text,
                  fontSize: 15,
                  fontWeight: FontWeight.w800,
                  letterSpacing: 0.8,
                ),
              ),
            ),
          ),
          const SizedBox(width: 8),
          if (_isVideo)
            _TopMutePill(
              muted: _isMuted,
              onTap: _toggleMute,
            )
          else
            const SizedBox(width: 104),
        ],
      ),
    );
  }

  Widget _buildTopPills() {
    return Row(
      children: [
        Expanded(
          child: _TopInfoPill(
             icon: _isVideo ? Icons.videocam_rounded : Icons.image_rounded,
            text: _formatSize(widget.sizeBytes),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _TopInfoPill(
            icon: Icons.group_rounded,
            text: widget.groupName,
          ),
        ),
      ],
    );
  }

  Widget _buildPreviewStage() {
    if (_preparingVideo) {
      return Container(
        width: double.infinity,
        decoration: BoxDecoration(
          color: _panelGlass,
          borderRadius: BorderRadius.circular(28),
          border: Border.all(color: _stroke.withValues(alpha: 0.58)),
        ),
        alignment: Alignment.center,
        child: const CircularProgressIndicator(),
      );
    }

    if (_videoPreviewError != null) {
      return Container(
        width: double.infinity,
        decoration: BoxDecoration(
          color: _panelGlass,
          borderRadius: BorderRadius.circular(28),
          border: Border.all(color: _stroke.withValues(alpha: 0.58)),
        ),
        alignment: Alignment.center,
        padding: const EdgeInsets.all(20),
        child: Text(
          _videoPreviewError!,
          textAlign: TextAlign.center,
          style: const TextStyle(color: _textSoft, height: 1.35),
        ),
      );
    }

    final controller = _videoController;
    if (controller == null || !controller.value.isInitialized) {
      return Container(
        width: double.infinity,
        decoration: BoxDecoration(
          color: _panelGlass,
          borderRadius: BorderRadius.circular(28),
          border: Border.all(color: _stroke.withValues(alpha: 0.58)),
        ),
        alignment: Alignment.center,
        child: const Text(
          'Видео пока не готово',
          style: TextStyle(color: _textSoft),
        ),
      );
    }

    final processingLabel = _shouldSendVideoTrim
        ? 'После публикации сервер подготовит фрагмент и сохранит звук'
        : 'Видео будет опубликовано без дополнительной обрезки';

    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: _panelGlass,
        borderRadius: BorderRadius.circular(28),
        border: Border.all(color: _stroke.withValues(alpha: 0.44)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.06),
            blurRadius: 10,
            offset: const Offset(0, 5),
          ),
        ],
      ),
      padding: const EdgeInsets.all(5),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(22),
        child: Stack(
          fit: StackFit.expand,
          children: [
            const DecoratedBox(
              decoration: BoxDecoration(
                color: Color(0xFF08040C),
              ),
            ),
            FittedBox(
              fit: BoxFit.cover,
              clipBehavior: Clip.hardEdge,
              child: SizedBox(
                width: controller.value.size.width,
                height: controller.value.size.height,
                child: VideoPlayer(controller),
              ),
            ),
            Positioned.fill(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: [
                      Colors.black.withValues(alpha: 0.12),
                      Colors.transparent,
                      Colors.black.withValues(alpha: 0.46),
                    ],
                  ),
                ),
              ),
            ),
            Center(
              child: GestureDetector(
                onTap: _toggleVideoPlayback,
                child: Container(
                  width: 66,
                  height: 66,
                  decoration: BoxDecoration(
                    color: Colors.black.withValues(alpha: 0.32),
                    shape: BoxShape.circle,
                    border: Border.all(
                      color: Colors.white.withValues(alpha: 0.16),
                    ),
                  ),
                  child: Icon(
                    controller.value.isPlaying
                        ? Icons.pause_rounded
                        : Icons.play_arrow_rounded,
                    size: 34,
                    color: Colors.white,
                  ),
                ),
              ),
            ),
            Positioned(
              left: 12,
              right: 12,
              bottom: 12,
              child: ClipRRect(
                borderRadius: BorderRadius.circular(18),
                child: BackdropFilter(
                  filter: ImageFilter.blur(sigmaX: 14, sigmaY: 14),
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 10,
                    ),
                    decoration: BoxDecoration(
                      color: const Color(0xFF120A18).withValues(alpha: 0.72),
                      borderRadius: BorderRadius.circular(18),
                      border: Border.all(
                        color: Colors.white.withValues(alpha: 0.08),
                      ),
                    ),
                    child: Row(
                      children: [
                        Icon(
                          _shouldSendVideoTrim
                              ? Icons.auto_fix_high_rounded
                              : Icons.check_circle_rounded,
                          color: _accentSoft,
                          size: 17,
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            processingLabel,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: _text,
                              fontSize: 12,
                              height: 1.22,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildImagePreviewStage() {
    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: _panelGlass,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: _stroke.withValues(alpha: 0.32)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.045),
            blurRadius: 10,
            offset: const Offset(0, 5),
          ),
        ],
      ),
      padding: const EdgeInsets.all(4),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(20),
        child: Image.memory(
          widget.bytes!,
          fit: BoxFit.cover,
          width: double.infinity,
          height: double.infinity,
        ),
      ),
    );
  }

  Widget _buildTimelineSection() {
    final totalLabel =
        _videoDuration == null ? '--:--' : _formatDuration(_videoDuration!);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            _SideSeekButton(
              icon: Icons.replay_10_rounded,
              onTap: () async {
                final controller = _videoController;
                if (controller == null || !controller.value.isInitialized) return;

                final current = controller.value.position;
                final target = Duration(
                  milliseconds: math.max(0, current.inMilliseconds - 10000),
                );
                await controller.seekTo(target);
                if (!mounted) return;
                setState(() {});
              },
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _TimelineTrack(
                totalSeconds: _totalVideoSeconds,
                startSeconds: _trimStartValue,
                endSeconds: _trimEndValue,
                onSeekPressed: _trimSaving ? null : _toggleTrimPlayback,
                onStartChanged: (value, isDragging) {
                  setState(() {
                    _trimStartValue = value.clamp(0.0, _trimEndValue).toDouble();
                    _trimmedVideoPath = null;
                    _activeDragThumb = _DragThumb.start;
                    _bubbleValueSeconds = _trimStartValue;
                    _showTrimBubble = isDragging;
                  });
                },
                onEndChanged: (value, isDragging) {
                  setState(() {
                    _trimEndValue =
                        value.clamp(_trimStartValue, _totalVideoSeconds).toDouble();
                    _trimmedVideoPath = null;
                    _activeDragThumb = _DragThumb.end;
                    _bubbleValueSeconds = _trimEndValue;
                    _showTrimBubble = isDragging;
                  });
                },
                onInteractionStart: () {
                  _pauseVideo();
                },
                onInteractionEnd: () async {
                  final controller = _videoController;

                  setState(() {
                    _showTrimBubble = false;
                    _activeDragThumb = null;
                  });

                  if (controller == null || !controller.value.isInitialized) return;

                  await controller.seekTo(
                    Duration(milliseconds: (_trimStartValue * 1000).round()),
                  );

                  if (!mounted) return;
                  setState(() {});
                },
                bubbleText: _formatDuration(
                  Duration(milliseconds: (_bubbleValueSeconds * 1000).round()),
                ),
                showBubble: _showTrimBubble,
                activeThumb: _activeDragThumb,
              ),
            ),
            const SizedBox(width: 12),
            _SideSeekButton(
              icon: Icons.forward_10_rounded,
              onTap: () async {
                final controller = _videoController;
                if (controller == null || !controller.value.isInitialized) return;

                final current = controller.value.position;
                final total = controller.value.duration;
                final target = Duration(
                  milliseconds: math.min(
                    total.inMilliseconds,
                    current.inMilliseconds + 10000,
                  ),
                );
                await controller.seekTo(target);
                if (!mounted) return;
                setState(() {});
              },
            ),
          ],
        ),
        const SizedBox(height: 10),
        Row(
          children: [
            Text(
              '0:00',
              style: const TextStyle(
                color: _textSoft,
                fontSize: 12,
                fontWeight: FontWeight.w500,
              ),
            ),
            const Spacer(),
            Text(
              totalLabel,
              style: const TextStyle(
                color: _textSoft,
                fontSize: 12,
                fontWeight: FontWeight.w500,
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        const Text(
          'Сохрани фрагмент до 2 минут',
          style: TextStyle(
            color: _textSoft,
            fontSize: 12,
            fontWeight: FontWeight.w600,
          ),
        ),
      ],
    );
  }

  Widget _buildBottomComposer() {
    final media = MediaQuery.of(context);
    final selectedDuration = _selectedTrimDuration();

    final actionEnabled = _publishing
        ? false
        : (_isVideo
            ? selectedDuration <= _maxVideoDuration && !_trimSaving
            : _canPublish);

    final focused = _captionFocusNode.hasFocus;

    final maxComposerHeight = math.min(
      media.size.height * (focused ? 0.34 : 0.18),
      280.0,
    );

    return Material(
      color: Colors.transparent,
      child: AnimatedSize(
        duration: const Duration(milliseconds: 180),
        curve: Curves.easeOutCubic,
        alignment: Alignment.bottomCenter,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Expanded(
              child: ClipRRect(
                borderRadius: BorderRadius.circular(24),
                child: BackdropFilter(
                  filter: ImageFilter.blur(sigmaX: 18, sigmaY: 18),
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 180),
                    curve: Curves.easeOutCubic,
                    constraints: BoxConstraints(
                      minHeight: 58,
                      maxHeight: math.max(96, maxComposerHeight),
                    ),
                    decoration: BoxDecoration(
                      color: _panel.withValues(alpha: focused ? 0.18 : 0.12),
                      borderRadius: BorderRadius.circular(24),
                      border: Border.all(
                        color: focused ? _accentSoft.withValues(alpha: 0.54) : _strokeSoft.withValues(alpha: 0.34),
                        width: 1.0,
                      ),
                    ),
                    padding: const EdgeInsets.fromLTRB(16, 13, 16, 13),
                    child: MentionTextField(
                      controller: _captionController,
                      focusNode: _captionFocusNode,
                      enabled: !_publishing && !_trimSaving,
                      minLines: 1,
                      maxLines: focused ? 6 : 3,
                      maxLength: _maxCaptionLength,
                      groupId: widget.groupId,
                      style: const TextStyle(
                        color: _text,
                        fontSize: 15,
                        fontWeight: FontWeight.w500,
                        height: 1.3,
                        backgroundColor: Colors.transparent,
                      ),
                      decoration: InputDecoration(
                        hintText: _isVideo ? 'Подпись к видео' : 'Подпись к фото',
                        hintStyle: const TextStyle(
                          color: _textSoft,
                          fontSize: 15,
                          fontWeight: FontWeight.w500,
                          backgroundColor: Colors.transparent,
                        ),
                        filled: false,
                        fillColor: Colors.transparent,
                        hoverColor: Colors.transparent,
                        focusColor: Colors.transparent,
                        border: InputBorder.none,
                        enabledBorder: InputBorder.none,
                        focusedBorder: InputBorder.none,
                        disabledBorder: InputBorder.none,
                        counterText: '',
                        isCollapsed: true,
                        contentPadding: EdgeInsets.zero,
                      ),
                    ),
                  ),
                ),
              ),
            ),
            const SizedBox(width: 12),
            Padding(
              padding: const EdgeInsets.only(bottom: 2),
              child: _PublishProgressButton(
                publishing: _publishing,
                progress: _uploadProgress,
                onTap: actionEnabled ? _publish : null,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

enum _DragThumb {
  start,
  end,
}

class _TopCircleButton extends StatelessWidget {
  final VoidCallback? onTap;
  final IconData icon;

  const _TopCircleButton({
    required this.onTap,
    required this.icon,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(999),
      child: Container(
        width: 38,
        height: 38,
        decoration: BoxDecoration(
          color: const Color(0xFF1B141F).withValues(alpha: 0.52),
          shape: BoxShape.circle,
          border: Border.all(color: const Color(0xFF3C3143).withValues(alpha: 0.54)),
        ),
        child: Icon(
          icon,
          color: const Color(0xFFE8D8F0),
          size: 17,
        ),
      ),
    );
  }
}

class _TopMutePill extends StatelessWidget {
  final bool muted;
  final VoidCallback? onTap;

  const _TopMutePill({
    required this.muted,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(999),
      child: Container(
        height: 38,
        padding: const EdgeInsets.symmetric(horizontal: 12),
        decoration: BoxDecoration(
          color: const Color(0xFF1B141F).withValues(alpha: 0.52),
          borderRadius: BorderRadius.circular(999),
          border: Border.all(color: const Color(0xFF3C3143).withValues(alpha: 0.54)),
        ),
        child: Row(
          children: [
            Icon(
              muted ? Icons.volume_off_rounded : Icons.volume_up_rounded,
              color: const Color(0xFFE8D8F0),
              size: 16,
            ),
            const SizedBox(width: 6),
            Text(
              muted ? 'без звука' : 'со звуком',
              style: const TextStyle(
                color: Color(0xFFE8D8F0),
                fontWeight: FontWeight.w700,
                fontSize: 11.5,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TopInfoPill extends StatelessWidget {
  final IconData icon;
  final String text;

  const _TopInfoPill({
    required this.icon,
    required this.text,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 34,
      padding: const EdgeInsets.symmetric(horizontal: 10),
      decoration: BoxDecoration(
        color: const Color(0xFF271635).withValues(alpha: 0.50),
        borderRadius: BorderRadius.circular(15),
        border: Border.all(color: const Color(0xFF4F345E).withValues(alpha: 0.46)),
      ),
      child: Row(
        children: [
          Icon(
            icon,
            size: 14,
            color: const Color(0xFFD9BEE5),
          ),
          const SizedBox(width: 6),
          Expanded(
            child: Text(
              text,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: Color(0xFFE8D8F0),
                fontWeight: FontWeight.w700,
                fontSize: 11.5,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _SideSeekButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onTap;

  const _SideSeekButton({
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final enabled = onTap != null;

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(999),
      child: Container(
        width: 52,
        height: 52,
        decoration: BoxDecoration(
          color: const Color(0xFF100915),
          shape: BoxShape.circle,
          border: Border.all(
            color: Colors.white.withValues(alpha: 0.92),
            width: 2,
          ),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.18),
              blurRadius: 12,
              offset: const Offset(0, 5),
            ),
          ],
        ),
        child: Icon(
          icon,
          color: enabled ? Colors.white : Colors.white54,
          size: 24,
        ),
      ),
    );
  }
}

class _TimelineTrack extends StatelessWidget {
  final double totalSeconds;
  final double startSeconds;
  final double endSeconds;
  final VoidCallback? onSeekPressed;
  final void Function(double value, bool isDragging) onStartChanged;
  final void Function(double value, bool isDragging) onEndChanged;
  final VoidCallback onInteractionStart;
  final VoidCallback onInteractionEnd;
  final String bubbleText;
  final bool showBubble;
  final _DragThumb? activeThumb;

  const _TimelineTrack({
    required this.totalSeconds,
    required this.startSeconds,
    required this.endSeconds,
    required this.onSeekPressed,
    required this.onStartChanged,
    required this.onEndChanged,
    required this.onInteractionStart,
    required this.onInteractionEnd,
    required this.bubbleText,
    required this.showBubble,
    required this.activeThumb,
  });

  double _clamp(double value) {
    if (totalSeconds <= 0) return 0.0;
    return value.clamp(0.0, totalSeconds).toDouble();
  }

  @override
  Widget build(BuildContext context) {
    const double trackHeight = 64;
    const double playAreaWidth = 56;
    const double horizontalPadding = 10;
    const double handleTouchWidth = 26;

    return LayoutBuilder(
      builder: (context, constraints) {
        final width = constraints.maxWidth;
        final usableWidth = math.max(
          1.0,
          width - playAreaWidth - horizontalPadding * 2,
        );

        final safeStart = _clamp(startSeconds);
        final safeEnd = _clamp(endSeconds <= safeStart ? safeStart : endSeconds);

        final startX =
            playAreaWidth +
            horizontalPadding +
            (safeStart / math.max(totalSeconds, 1)) * usableWidth;

        final endX =
            playAreaWidth +
            horizontalPadding +
            (safeEnd / math.max(totalSeconds, 1)) * usableWidth;

        double dxToSeconds(double localDx) {
          final relative =
              ((localDx - playAreaWidth - horizontalPadding) / usableWidth)
                  .clamp(0.0, 1.0);
          return relative * totalSeconds;
        }

        final bubbleLeft = (activeThumb == _DragThumb.end ? endX : startX)
            .clamp(30.0, width - 30.0) - 30.0;

        return SizedBox(
          height: trackHeight + (showBubble ? 36 : 0),
          child: Stack(
            clipBehavior: Clip.none,
            children: [
              Positioned(
                top: showBubble ? 36 : 0,
                left: 0,
                right: 0,
                child: Container(
                  height: trackHeight,
                  decoration: BoxDecoration(
                    color: const Color(0xFFE9DBF2),
                    borderRadius: BorderRadius.circular(22),
                  ),
                  child: Stack(
                    children: [
                      Positioned(
                        left: 8,
                        top: 8,
                        bottom: 8,
                        width: playAreaWidth - 8,
                        child: InkWell(
                          onTap: onSeekPressed,
                          borderRadius: BorderRadius.circular(16),
                          child: Container(
                            decoration: BoxDecoration(
                              color: const Color(0xFFD8C2E6),
                              borderRadius: BorderRadius.circular(16),
                            ),
                            child: Icon(
                              Icons.play_arrow_rounded,
                              color: onSeekPressed == null
                                  ? Colors.white54
                                  : Colors.white,
                              size: 28,
                            ),
                          ),
                        ),
                      ),
                      Positioned(
                        left: playAreaWidth,
                        right: horizontalPadding,
                        top: 9,
                        bottom: 9,
                        child: Container(
                          decoration: BoxDecoration(
                            color: const Color(0xFFC9A8DD),
                            borderRadius: BorderRadius.circular(18),
                          ),
                        ),
                      ),
                      Positioned(
                        left: playAreaWidth,
                        width: math.max(0.0, startX - playAreaWidth),
                        top: 9,
                        bottom: 9,
                        child: Container(
                          decoration: BoxDecoration(
                            color: Colors.white.withValues(alpha: 0.26),
                            borderRadius: BorderRadius.circular(18),
                          ),
                        ),
                      ),
                      Positioned(
                        left: endX,
                        right: horizontalPadding,
                        top: 9,
                        bottom: 9,
                        child: Container(
                          decoration: BoxDecoration(
                            color: Colors.white.withValues(alpha: 0.26),
                            borderRadius: BorderRadius.circular(18),
                          ),
                        ),
                      ),
                      Positioned(
                        left: startX,
                        width: math.max(14, endX - startX),
                        top: 9,
                        bottom: 9,
                        child: IgnorePointer(
                          child: Container(
                            decoration: BoxDecoration(
                              borderRadius: BorderRadius.circular(18),
                              border: Border.all(
                                color: Colors.white,
                                width: 2.2,
                              ),
                            ),
                          ),
                        ),
                      ),
                      Positioned(
                        left: startX - handleTouchWidth / 2,
                        top: 7,
                        bottom: 7,
                        width: handleTouchWidth,
                        child: GestureDetector(
                          behavior: HitTestBehavior.translucent,
                          onHorizontalDragStart: (_) => onInteractionStart(),
                          onHorizontalDragUpdate: (details) {
                            final box = context.findRenderObject() as RenderBox?;
                            if (box == null) return;
                            final local = box.globalToLocal(details.globalPosition);
                            onStartChanged(dxToSeconds(local.dx), true);
                          },
                          onHorizontalDragEnd: (_) => onInteractionEnd(),
                          child: Center(
                            child: Container(
                              width: 10,
                              height: 40,
                              decoration: BoxDecoration(
                                color: const Color(0xFFF8F1FC),
                                borderRadius: BorderRadius.circular(999),
                                boxShadow: [
                                  BoxShadow(
                                    color: Colors.black.withValues(alpha: 0.10),
                                    blurRadius: 6,
                                    offset: const Offset(0, 2),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ),
                      ),
                      Positioned(
                        left: endX - handleTouchWidth / 2,
                        top: 7,
                        bottom: 7,
                        width: handleTouchWidth,
                        child: GestureDetector(
                          behavior: HitTestBehavior.translucent,
                          onHorizontalDragStart: (_) => onInteractionStart(),
                          onHorizontalDragUpdate: (details) {
                            final box = context.findRenderObject() as RenderBox?;
                            if (box == null) return;
                            final local = box.globalToLocal(details.globalPosition);
                            onEndChanged(dxToSeconds(local.dx), true);
                          },
                          onHorizontalDragEnd: (_) => onInteractionEnd(),
                          child: Center(
                            child: Container(
                              width: 10,
                              height: 40,
                              decoration: BoxDecoration(
                                color: const Color(0xFFF8F1FC),
                                borderRadius: BorderRadius.circular(999),
                                boxShadow: [
                                  BoxShadow(
                                    color: Colors.black.withValues(alpha: 0.10),
                                    blurRadius: 6,
                                    offset: const Offset(0, 2),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              if (showBubble)
                Positioned(
                  top: 0,
                  left: bubbleLeft,
                  child: Container(
                    constraints: const BoxConstraints(minWidth: 60),
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 7,
                    ),
                    decoration: BoxDecoration(
                      color: const Color(0xFF24152F),
                      borderRadius: BorderRadius.circular(999),
                      border: Border.all(color: const Color(0xFF4B3458)),
                    ),
                    child: Text(
                      bubbleText,
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        color: Color(0xFFE9D9F0),
                        fontSize: 11.5,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ),
            ],
          ),
        );
      },
    );
  }
}

class _PublishProgressButton extends StatelessWidget {
  final bool publishing;
  final double progress;
  final VoidCallback? onTap;

  const _PublishProgressButton({
    required this.publishing,
    required this.progress,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final safeProgress = progress.clamp(0.0, 1.0).toDouble();

    return GestureDetector(
      onTap: publishing ? null : onTap,
      child: SizedBox(
        width: 62,
        height: 62,
        child: Stack(
          alignment: Alignment.center,
          children: [
            if (publishing)
              SizedBox(
                width: 62,
                height: 62,
                child: CircularProgressIndicator(
                  value: safeProgress > 0 && safeProgress < 1
                      ? safeProgress
                      : null,
                  strokeWidth: 3,
                ),
              ),
            Container(
              width: 54,
              height: 54,
              decoration: BoxDecoration(
                color: publishing
                    ? const Color(0xFF6B4A78)
                    : const Color(0xFFB98BD0),
                shape: BoxShape.circle,
              ),
              child: Icon(
                publishing ? Icons.hourglass_top_rounded : Icons.send_rounded,
                color: Colors.white,
                size: 28,
              ),
            ),
          ],
        ),
      ),
    );
  }
}