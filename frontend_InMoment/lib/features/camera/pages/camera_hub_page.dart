import 'dart:ui';
import 'dart:async';

import 'package:camera/camera.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:video_compress/video_compress.dart';

import '../../../core/realtime/users_realtime_service.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_compact_icon_button.dart';
import '../../../core/widgets/inmoment_surface.dart';
import '../../../core/widgets/group_dropdown_selector.dart';
import '../../../core/layout/inmoment_media_frame.dart';
import '../../groups/controllers/active_group_controller.dart';
import '../../groups/models/group.dart';
import '../../notifications/controllers/notifications_controller.dart';
import '../../notifications/pages/notifications_page.dart';
import '../../photo/pages/photo_preview_page.dart';
import '../../profile/api/profile_api.dart';


class CameraHubPage extends StatefulWidget {
  final Future<void> Function()? onOpenGroupFeed;
  final VoidCallback? onOpenProfile;

  const CameraHubPage({
    super.key,
    this.onOpenGroupFeed,
    this.onOpenProfile,
  });

  @override
  State<CameraHubPage> createState() => _CameraHubPageState();
}

class _CameraHubPageState extends State<CameraHubPage> {
  final ActiveGroupController _activeGroupController =
      ActiveGroupController.instance;
  final ProfileApi _profileApi = ProfileApi();
  final NotificationsController _notifications =
      NotificationsController.instance;
  final ImagePicker _imagePicker = ImagePicker();

  bool _loading = true;
  bool _refreshing = false;
  bool _pickingPhoto = false;
  bool _capturing = false;
  bool _recordingVideo = false;
  Timer? _videoAutoStopTimer;
  String? _error;

  Offset? _feedSwipeStart;
  bool _openingFeedBySwipe = false;

  List<CameraDescription> _cameras = const [];
  CameraController? _cameraController;
  CameraDescription? _currentCamera;
  bool _cameraInitializing = false;
  bool _cameraReady = false;
  String? _cameraError;
  FlashMode _flashMode = FlashMode.off;

    static const int _maxMediaUploadMb = 200;
    static const int _maxPhotoUploadMb = _maxMediaUploadMb;
    static const int _maxVideoUploadMb = _maxMediaUploadMb;

    static const int _maxPhotoUploadBytes = _maxMediaUploadMb * 1024 * 1024;
    static const int _maxVideoUploadBytes = _maxMediaUploadMb * 1024 * 1024;
    static const Duration _maxVideoDuration = Duration(minutes: 2);

  @override
  void initState() {
    super.initState();
    _activeGroupController.addListener(_onGroupChanged);
    _load();
    _notifications.loadInitial(force: true);
    UsersRealtimeService.instance.ensureConnected();
    _initCamera();
  }

  @override
  void dispose() {
    _activeGroupController.removeListener(_onGroupChanged);
    _videoAutoStopTimer?.cancel();
    _disposeCamera();
    super.dispose();
  }

  void _onGroupChanged() {
    if (!mounted) return;
    setState(() {});
  }

  Future<void> _disposeCamera() async {
    final controller = _cameraController;
    _cameraController = null;
    _cameraReady = false;

    if (controller != null) {
      try {
        await controller.dispose();
      } catch (_) {}
    }
  }

  Future<void> _load({bool silent = false}) async {
    if (!silent) {
      setState(() {
        _loading = true;
        _error = null;
      });
    } else {
      setState(() {
        _refreshing = true;
      });
    }

    try {
      await _activeGroupController.load(force: true);
      await _profileApi.getMe();
      await _notifications.reloadUnreadCount();

      if (!mounted) return;

      setState(() {
        _loading = false;
        _refreshing = false;
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _error = _normalizeError(e);
        _loading = false;
        _refreshing = false;
      });
    }
  }

  Future<String?> _fixVideoForAndroid(String path) async {
    try {
      final result = await VideoCompress.compressVideo(
        path,
        quality: VideoQuality.DefaultQuality, // важно
        includeAudio: true,
        deleteOrigin: false,
      );

      return result?.file?.path;
    } catch (e) {
      return null;
    }
  }

  Future<void> _initCamera() async {
    setState(() {
      _cameraInitializing = true;
      _cameraError = null;
    });

    try {
      final cameras = await availableCameras();

      if (cameras.isEmpty) {
        if (!mounted) return;
        setState(() {
          _cameraInitializing = false;
          _cameraError = 'Камера недоступна';
        });
        return;
      }

      _cameras = cameras;

      final selected = cameras.firstWhere(
        (camera) => camera.lensDirection == CameraLensDirection.back,
        orElse: () => cameras.first,
      );

      await _startCamera(selected);
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _cameraInitializing = false;
        _cameraError = 'Не удалось инициализировать камеру';
      });
    }
  }

  Future<void> _startCamera(CameraDescription description) async {
    await _disposeCamera();

    final controller = CameraController(
      description,
      ResolutionPreset.high,
      enableAudio: true,
      imageFormatGroup: ImageFormatGroup.jpeg,
    );

    try {
      await controller.initialize();

      try {
        await controller.setFlashMode(_flashMode);
      } catch (_) {}

      if (!mounted) {
        await controller.dispose();
        return;
      }

      setState(() {
        _cameraController = controller;
        _currentCamera = description;
        _cameraReady = controller.value.isInitialized;
        _cameraInitializing = false;
        _cameraError = null;
      });
    } catch (_) {
      await controller.dispose();

      if (!mounted) return;
      setState(() {
        _cameraReady = false;
        _cameraInitializing = false;
        _cameraError = 'Не удалось запустить камеру';
      });
    }
  }

  Future<void> _changeActiveGroup(Group? value) async {
    if (value == null) return;

    try {
      await _activeGroupController.setActiveGroup(value);

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Активная группа: ${value.name}')),
      );
    } catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'Не удалось сменить группу: ${_normalizeError(e)}',
          ),
        ),
      );
    }
  }

  Future<void> _openPhotoPreview({
    Uint8List? bytes,
    String? localFilePath,
    required int sizeBytes,
    required String contentType,
    required String fileName,
  }) async {
    final activeGroup = _activeGroupController.activeGroup;
    final groups = _activeGroupController.groups;

    if (groups.isEmpty || activeGroup == null) {
      _showMessage('Сначала нужна хотя бы одна группа');
      return;
    }

    if (!mounted) return;

    final published = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => PhotoPreviewPage(
          groupId: activeGroup.id,
          groupName: activeGroup.name,
          bytes: bytes,
          localFilePath: localFilePath,
          sizeBytes: sizeBytes,
          contentType: contentType,
          fileName: fileName,
        ),
      ),
    );

    if (!mounted) return;

    if (published == true) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Публикация отправлена в группу «${activeGroup.name}»'),
        ),
      );
      await _load(silent: true);
    }
  }

  Future<void> _pickMediaFromGallery(BuildContext anchorContext) async {
    if (_pickingPhoto || _capturing) return;

    final selected = await _showGlassAnchorMenu<String>(
      context: context,
      anchorContext: anchorContext,
      items: const [
        _GlassMenuItemValue(
          value: 'photo',
          icon: Icons.photo_library_outlined,
          text: 'Выбрать фото',
        ),
        _GlassMenuItemValue(
          value: 'video',
          icon: Icons.video_library_outlined,
          text: 'Выбрать видео',
        ),
      ],
    );

    if (selected == 'photo') {
      await _pickPhotoFromGallery();
      return;
    }

    if (selected == 'video') {
      await _pickVideoFromGallery();
    }
  }

  Future<void> _pickVideoFromGallery() async {
    if (_pickingPhoto || _capturing) return;

    final activeGroup = _activeGroupController.activeGroup;
    final groups = _activeGroupController.groups;

    if (groups.isEmpty || activeGroup == null) {
      _showMessage('Сначала нужна хотя бы одна группа');
      return;
    }

    setState(() {
      _pickingPhoto = true;
    });

    try {
      final picked = await _imagePicker.pickVideo(
        source: ImageSource.gallery,
      );

      if (picked == null) return;

      var path = picked.path.trim();
      var fileName = picked.name.trim();

      if (fileName.isEmpty) {
        fileName = path.isNotEmpty ? path.split('/').last : 'gallery_video.mp4';
      }

      var contentType = _resolveContentType(
        fileName,
        mimeType: picked.mimeType,
      );

      if (!_isSupportedContentType(contentType)) {
        contentType = _resolveContentType(
          path,
          mimeType: null,
        );
      }

      if (!_isSupportedContentType(contentType)) {
        contentType = 'video/mp4';

        if (!fileName.toLowerCase().endsWith('.mp4')) {
          fileName = 'gallery_video_${DateTime.now().millisecondsSinceEpoch}.mp4';
        }
      }

      if (!kIsWeb) {
        final fixed = await _fixVideoForAndroid(path);
        if (fixed != null && fixed.trim().isNotEmpty) {
          path = fixed;
          contentType = 'video/mp4';

          if (!fileName.toLowerCase().endsWith('.mp4')) {
            fileName =
                'gallery_video_${DateTime.now().millisecondsSinceEpoch}.mp4';
          }
        }
      }

      final sizeBytes = kIsWeb
          ? await picked.length()
          : await XFile(path).length();

      if (sizeBytes <= 0) {
        _showMessage('Не удалось прочитать видео');
        return;
      }

      if (sizeBytes > _maxVideoUploadBytes) {
        _showMessage('Видео слишком большое. Максимум $_maxVideoUploadMb МБ.');
        return;
      }

      await _openPhotoPreview(
        bytes: kIsWeb ? await picked.readAsBytes() : null,
        localFilePath: kIsWeb ? null : path,
        sizeBytes: sizeBytes,
        contentType: contentType,
        fileName: fileName,
      );
    } catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Ошибка выбора видео: ${_normalizeError(e)}'),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _pickingPhoto = false;
        });
      }
    }
  }

  Future<void> _handleCaptureTap() async {
    if (_recordingVideo) {
      await _stopVideoRecording();
      return;
    }

    await _capturePhoto();
  }

  Future<void> _startVideoRecording() async {
    if (_recordingVideo || _capturing || _pickingPhoto || _cameraInitializing) {
      return;
    }

    final activeGroup = _activeGroupController.activeGroup;
    final groups = _activeGroupController.groups;

    if (groups.isEmpty || activeGroup == null) {
      _showMessage('Сначала нужна хотя бы одна группа');
      return;
    }

    final controller = _cameraController;
    if (controller == null || !controller.value.isInitialized) {
      _showMessage('Камера ещё не готова');
      return;
    }

    try {
      await controller.prepareForVideoRecording();
    } catch (_) {}

    try {
      await controller.startVideoRecording();

      _videoAutoStopTimer?.cancel();
      _videoAutoStopTimer = Timer(
        _maxVideoDuration,
        () async {
          if (_recordingVideo) {
            await _stopVideoRecording();
          }
        },
      );

      if (!mounted) return;
      setState(() {
        _recordingVideo = true;
      });
    } catch (e) {
      if (!mounted) return;
      _showMessage('Не удалось начать запись: ${_normalizeError(e)}');
    }
  }

  Future<void> _stopVideoRecording() async {
    if (!_recordingVideo) return;

    final controller = _cameraController;
    if (controller == null || !controller.value.isInitialized) {
      return;
    }

    try {
      final file = await controller.stopVideoRecording();
      _videoAutoStopTimer?.cancel();

      if (!mounted) return;
      setState(() {
        _recordingVideo = false;
      });

      final initialSizeBytes = await file.length();
      if (initialSizeBytes <= 0) {
        _showMessage('Не удалось получить видео');
        return;
      }

      final fileName =
          file.name.isEmpty ? 'camera_video.mp4' : file.name;

      String path = file.path;

      if (!kIsWeb) {
        final fixed = await _fixVideoForAndroid(path);
        if (fixed != null && fixed.trim().isNotEmpty) {
          path = fixed;
        }
      }

      final finalSizeBytes = kIsWeb
          ? initialSizeBytes
          : await XFile(path).length();

      if (finalSizeBytes <= 0) {
        _showMessage('Не удалось подготовить видео');
        return;
      }

      if (finalSizeBytes > _maxVideoUploadBytes) {
        _showMessage('Видео слишком большое. Максимум $_maxVideoUploadMb МБ.');
        return;
      }

      await _openPhotoPreview(
        bytes: kIsWeb ? await file.readAsBytes() : null,
        localFilePath: kIsWeb ? null : path,
        sizeBytes: finalSizeBytes,
        contentType: 'video/mp4',
        fileName: fileName,
      );
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _recordingVideo = false;
      });

      _showMessage('Ошибка завершения записи: ${_normalizeError(e)}');
    }
  }

  Future<void> _pickPhotoFromGallery() async {
    if (_pickingPhoto || _capturing) return;

    final activeGroup = _activeGroupController.activeGroup;
    final groups = _activeGroupController.groups;

    if (groups.isEmpty || activeGroup == null) {
      _showMessage('Сначала нужна хотя бы одна группа');
      return;
    }

    setState(() {
      _pickingPhoto = true;
    });

    try {
      final file = await _imagePicker.pickImage(
        source: ImageSource.gallery,
        imageQuality: 95,
      );

      if (file == null) {
        if (!mounted) return;
        setState(() {
          _pickingPhoto = false;
        });
        return;
      }

      final bytes = await file.readAsBytes();

      if (bytes.isEmpty) {
        _showMessage('Не удалось прочитать файл');
        return;
      }

      if (bytes.length > _maxPhotoUploadBytes) {
        _showMessage('Файл слишком большой. Максимум $_maxPhotoUploadMb МБ.');
        return;
      }

      final fileName = file.name.isEmpty ? 'gallery_photo' : file.name;
      final contentType = _resolveContentType(
        fileName,
        mimeType: file.mimeType,
      );

      if (!_isSupportedContentType(contentType)) {
        _showMessage('Поддерживаются JPG, PNG, WEBP, HEIC, HEIF');
        return;
      }

      await _openPhotoPreview(
        bytes: bytes,
        localFilePath: null,
        sizeBytes: bytes.length,
        contentType: contentType,
        fileName: fileName,
      );
    } catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Ошибка выбора файла: ${_normalizeError(e)}'),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _pickingPhoto = false;
        });
      }
    }
  }

  Future<void> _capturePhoto() async {
    if (_capturing || _pickingPhoto || _cameraInitializing) return;

    final activeGroup = _activeGroupController.activeGroup;
    final groups = _activeGroupController.groups;

    if (groups.isEmpty || activeGroup == null) {
      _showMessage('Сначала нужна хотя бы одна группа');
      return;
    }

    final controller = _cameraController;
    if (controller == null || !controller.value.isInitialized) {
      _showMessage('Камера ещё не готова');
      return;
    }

    setState(() {
      _capturing = true;
    });

    try {
      final photo = await controller.takePicture();
      final bytes = await photo.readAsBytes();

      if (bytes.isEmpty) {
        _showMessage('Не удалось получить изображение');
        return;
      }

      if (bytes.length > _maxPhotoUploadBytes) {
        _showMessage('Фото слишком большое. Максимум $_maxPhotoUploadMb МБ.');
        return;
      }

      await _openPhotoPreview(
        bytes: bytes,
        localFilePath: null,
        sizeBytes: bytes.length,
        contentType: 'image/jpeg',
        fileName: photo.name.isEmpty ? 'camera_capture.jpg' : photo.name,
      );
    } catch (e) {
      if (!mounted) return;
      _showMessage('Ошибка камеры: ${_normalizeError(e)}');
    } finally {
      if (mounted) {
        setState(() {
          _capturing = false;
        });
      }
    }
  }

  Future<void> _toggleFlashPreview() async {
    final controller = _cameraController;
    if (controller == null || !controller.value.isInitialized) {
      _showMessage('Камера ещё не готова');
      return;
    }

    final nextMode =
        _flashMode == FlashMode.off ? FlashMode.torch : FlashMode.off;

    try {
      await controller.setFlashMode(nextMode);

      if (!mounted) return;
      setState(() {
        _flashMode = nextMode;
      });
    } catch (_) {
      _showMessage('На этой камере вспышка недоступна');
    }
  }

  Future<void> _toggleCameraPreview() async {
    if (_cameras.length < 2) {
      _showMessage('Доступна только одна камера');
      return;
    }

    final current = _currentCamera;
    if (current == null) return;

    CameraDescription? next;
    for (final camera in _cameras) {
      if (camera.lensDirection != current.lensDirection) {
        next = camera;
        break;
      }
    }

    next ??= _cameras.firstWhere(
      (camera) => camera.name != current.name,
      orElse: () => current,
    );

    if (next.name == current.name) {
      _showMessage('Не удалось переключить камеру');
      return;
    }

    setState(() {
      _cameraInitializing = true;
      _cameraReady = false;
    });

    await _startCamera(next);
  }

  Future<void> _openGroupFeed() async {
    final callback = widget.onOpenGroupFeed;
    if (callback == null) {
      _showMessage('Лента группы пока недоступна');
      return;
    }

    await callback();
    await _notifications.reloadUnreadCount();
  }

  Future<void> _openNotifications() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const NotificationsPage(),
      ),
    );

    await _notifications.reloadUnreadCount();
  }

  void _showMessage(String text) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(text)),
    );
  }

  bool _isSupportedContentType(String contentType) {
    return const {
      'image/jpeg',
      'image/png',
      'image/webp',
      'image/heic',
      'image/heif',
      'video/mp4',
      'video/quicktime',
      'video/x-m4v',
      'video/webm',
      'video/3gpp',
    }.contains(contentType);
  }

    String _resolveContentType(
      String fileName, {
      String? mimeType,
    }) {
      final normalizedMime = mimeType?.trim().toLowerCase();

      if (normalizedMime != null &&
          normalizedMime.isNotEmpty &&
          normalizedMime != 'application/octet-stream') {
        final mime = normalizedMime.split(';').first.trim();
        if (_isSupportedContentType(mime)) {
          return mime;
        }

        if (mime == 'image/jpg') {
          return 'image/jpeg';
        }

        if (mime == 'video/mp4v-es' ||
            mime == 'video/mpeg4' ||
            mime == 'video/x-mp4' ||
            mime == 'application/mp4') {
          return 'video/mp4';
        }

        if (mime == 'video/quicktime' || mime == 'video/mov') {
          return 'video/quicktime';
        }

        if (mime == 'video/3gpp' || mime == 'video/3gp') {
          return 'video/3gpp';
        }
      }

      final normalized = fileName.trim().toLowerCase();

      if (normalized.endsWith('.jpg') || normalized.endsWith('.jpeg')) {
        return 'image/jpeg';
      }
      if (normalized.endsWith('.png')) {
        return 'image/png';
      }
      if (normalized.endsWith('.webp')) {
        return 'image/webp';
      }
      if (normalized.endsWith('.heic')) {
        return 'image/heic';
      }
      if (normalized.endsWith('.heif')) {
        return 'image/heif';
      }
      if (normalized.endsWith('.mp4')) {
        return 'video/mp4';
      }
      if (normalized.endsWith('.mov')) {
        return 'video/quicktime';
      }
      if (normalized.endsWith('.m4v')) {
        return 'video/x-m4v';
      }
      if (normalized.endsWith('.webm')) {
        return 'video/webm';
      }
      if (normalized.endsWith('.3gp')) {
        return 'video/3gpp';
      }
      if (normalized.endsWith('.3gpp')) {
        return 'video/3gpp';
      }

    return 'application/octet-stream';
  }

  String _normalizeError(Object error) {
    final text = error.toString().trim();
    const prefix = 'Exception: ';
    if (text.startsWith(prefix)) {
      return text.substring(prefix.length).trim();
    }
    return text;
  }

  @override
  Widget build(BuildContext context) {
    final groups = _activeGroupController.groups;
    final activeGroup = _activeGroupController.activeGroup;

    if (_loading) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: Center(
          child: CircularProgressIndicator(),
        ),
      );
    }

    if (_error != null) {
      return Scaffold(
        backgroundColor: AppColors.background,
        body: SafeArea(
          child: Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: InMomentSurface(
                padding: const EdgeInsets.all(20),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(
                      Icons.error_outline_rounded,
                      color: AppColors.textSecondary,
                      size: 46,
                    ),
                    const SizedBox(height: 14),
                    Text(
                      _error!,
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        color: AppColors.textPrimary,
                        height: 1.4,
                      ),
                    ),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: _load,
                      child: const Text('Повторить'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      );
    }

    return AnimatedBuilder(
      animation: _notifications,
      builder: (context, _) {
        return Scaffold(
          backgroundColor: AppColors.background,
          body: DecoratedBox(
            decoration: const BoxDecoration(
              gradient: AppColors.pageBackgroundGradient,
            ),
            child: SafeArea(
              bottom: false,
              child: Listener(
                behavior: HitTestBehavior.translucent,
                onPointerDown: (event) {
                  _feedSwipeStart = event.position;
                },
                onPointerUp: (event) async {
                  final start = _feedSwipeStart;
                  _feedSwipeStart = null;

                  if (start == null ||
                      _openingFeedBySwipe ||
                      activeGroup == null) {
                    return;
                  }

                  final delta = event.position - start;

                  if (delta.dy < -72 &&
                      delta.dy.abs() > delta.dx.abs() * 1.25) {
                    _openingFeedBySwipe = true;
                    try {
                      await _openGroupFeed();
                    } finally {
                      _openingFeedBySwipe = false;
                    }
                  }
                },
                child: LayoutBuilder(
                  builder: (context, constraints) {
                    const horizontalPadding = 12.0;
                    const topPadding = 12.0;

                    const bottomNavigationReserve = 80.0;

                    const topBarHeight = 58.0;
                    const topBarToClusterGap = 30.0;

                    const actionsHeight = 78.0;
                    const previewToActionsGap = 10.0;
                    const actionsToHintGap = 6.0;
                    const hintHeight = 34.0;
                    const hintToFeedGap = 10.0;
                    const feedHeight = 48.0;
                    const emptyGroupsGap = 12.0;
                    const emptyGroupsHintHeight = 150.0;

                    final hasPublishTarget = groups.isNotEmpty && activeGroup != null;

                    final fixedControlsHeight = previewToActionsGap +
                        actionsHeight +
                        actionsToHintGap +
                        hintHeight +
                        (hasPublishTarget ? hintToFeedGap + feedHeight : 0.0);

                    final clusterControlsHeight = fixedControlsHeight +
                        (groups.isEmpty
                            ? emptyGroupsGap + emptyGroupsHintHeight
                            : 0.0);

                    final contentWidth = InMomentMediaFrame.resolveAdaptiveContentWidth(
                      constraints.maxWidth,
                    );

                    final middleTop =
                        topPadding + topBarHeight + topBarToClusterGap;

                    final middleBottom = bottomNavigationReserve;

                    final middleHeight = constraints.maxHeight -
                        middleTop -
                        middleBottom;

                    final frame = InMomentMediaFrame.resolveHomeSquare(
                      viewportWidth: constraints.maxWidth,
                      viewportHeight: constraints.maxHeight,
                      availableHeight: middleHeight - fixedControlsHeight,
                    );

                    final previewSize = frame.width;

                    final clusterHeight =
                        previewSize + clusterControlsHeight;

                    final cluster = SizedBox(
                      width: contentWidth,
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Center(
                            child: _CameraPreviewStage(
                              width: previewSize,
                              height: previewSize,
                            controller: _cameraController,
                            cameraInitializing: _cameraInitializing,
                            cameraReady: _cameraReady,
                            cameraError: _cameraError,
                            onRetry: _initCamera,
                            onPickFromGallery:
                                  _pickingPhoto || _capturing || _recordingVideo
                                      ? null
                                      : _pickMediaFromGallery,
                            ),
                          ),
                          const SizedBox(height: previewToActionsGap),
                          _CameraActionsRow(
                            onToggleFlash: _cameraInitializing ||
                                    _capturing ||
                                    _recordingVideo
                                ? null
                                : _toggleFlashPreview,
                            onCapture: _cameraInitializing ||
                                    _capturing ||
                                    _pickingPhoto
                                ? null
                                : _handleCaptureTap,
                            onCaptureLongPress: _cameraInitializing ||
                                    _capturing ||
                                    _pickingPhoto ||
                                    _recordingVideo
                                ? null
                                : _startVideoRecording,
                            onToggleCamera: _cameraInitializing ||
                                    _capturing ||
                                    _recordingVideo
                                ? null
                                : _toggleCameraPreview,
                            flashEnabled: _flashMode == FlashMode.torch,
                            recordingVideo: _recordingVideo,
                          ),
                          const SizedBox(height: actionsToHintGap),
                          SizedBox(
                            height: hintHeight,
                            child: Center(
                              child: Text(
                                _recordingVideo
                                    ? 'Идёт запись видео… нажми на кнопку, чтобы остановить'
                                    : 'Нажатие — фото, удержание — видео до 2 минут, до $_maxVideoUploadMb МБ',
                                textAlign: TextAlign.center,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  color: AppColors.textSecondary,
                                  fontSize: 10.8,
                                  fontWeight: FontWeight.w600,
                                  height: 1.2,
                                ),
                              ),
                            ),
                          ),
                          if (hasPublishTarget) ...[
                            const SizedBox(height: hintToFeedGap),
                            _FeedEntryAction(
                              group: activeGroup,
                              onTap: _openGroupFeed,
                            ),
                          ],
                          if (groups.isEmpty) ...[
                            const SizedBox(height: emptyGroupsGap),
                            SizedBox(
                              height: emptyGroupsHintHeight,
                              child: _EmptyGroupsHintCard(
                                onOpenProfile: widget.onOpenProfile,
                              ),
                            ),
                          ],
                        ],
                      ),
                    );

                    return Stack(
                      children: [
                        Positioned(
                          left: horizontalPadding,
                          right: horizontalPadding,
                          top: topPadding,
                          height: topBarHeight,
                          child: Center(
                            child: SizedBox(
                              width: contentWidth,
                              child: _CameraTopBar(
                                groups: groups,
                                activeGroup: activeGroup,
                                savingGroup:
                                    _activeGroupController.saving || _refreshing,
                                unreadCount: _notifications.unreadCount,
                                onNotificationsTap: _openNotifications,
                                onGroupChanged: _changeActiveGroup,
                              ),
                            ),
                          ),
                        ),
                        Positioned(
                          left: horizontalPadding,
                          right: horizontalPadding,
                          top: middleTop,
                          bottom: middleBottom,
                          child: clusterHeight > middleHeight
                              ? SingleChildScrollView(
                                  physics: const ClampingScrollPhysics(),
                                  child: Center(child: cluster),
                                )
                              : Center(child: cluster),
                        ),
                      ],
                    );
                  },
                ),
              ),
            ),
          ),
        );
      },
    );
  }
}

class _CameraTopBar extends StatelessWidget {
  final List<Group> groups;
  final Group? activeGroup;
  final bool savingGroup;
  final int unreadCount;
  final ValueChanged<Group?> onGroupChanged;
  final Future<void> Function() onNotificationsTap;

  const _CameraTopBar({
    required this.groups,
    required this.activeGroup,
    required this.savingGroup,
    required this.unreadCount,
    required this.onNotificationsTap,
    required this.onGroupChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        _NotificationIconButton(
          unreadCount: unreadCount,
          onTap: onNotificationsTap,
        ),
        const SizedBox(width: 10),
        Expanded(
          child: GroupDropdownSelector(
            groups: groups,
            selectedGroupId: activeGroup?.id,
            hintText: 'Группа',
            enabled: groups.isNotEmpty && !savingGroup,
            isLoading: savingGroup,
            height: 42,
            borderRadius: 18,
            avatarRadius: 13,
            fontSize: 14,
            onChanged: (groupId) {
              final selected = groups.cast<Group?>().firstWhere(
                    (group) => group?.id == groupId,
                    orElse: () => null,
                  );
              onGroupChanged(selected);
            },
          ),
        ),
      ],
    );
  }
}

class _NotificationIconButton extends StatelessWidget {
  final int unreadCount;
  final Future<void> Function() onTap;

  const _NotificationIconButton({
    required this.unreadCount,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final badgeText = unreadCount > 99 ? '99+' : '$unreadCount';

    return Stack(
      clipBehavior: Clip.none,
      children: [
        InMomentCompactIconButton(
          icon: Icons.notifications_none_rounded,
          onTap: onTap,
          translucent: true,
        ),
        if (unreadCount > 0)
          Positioned(
            right: -2,
            top: -2,
            child: Container(
              constraints: const BoxConstraints(
                minWidth: 18,
                minHeight: 18,
              ),
              padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 2),
              decoration: BoxDecoration(
                color: AppColors.accentSecondary,
                borderRadius: BorderRadius.circular(999),
                border: Border.all(
                  color: AppColors.background,
                  width: 1.4,
                ),
              ),
              child: Text(
                badgeText,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: 10,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
          ),
      ],
    );
  }
}

class _CameraPreviewStage extends StatelessWidget {
  final double height;
  final double width;
  final CameraController? controller;
  final bool cameraInitializing;
  final bool cameraReady;
  final String? cameraError;
  final Future<void> Function() onRetry;
  final Future<void> Function(BuildContext anchorContext)? onPickFromGallery;

  const _CameraPreviewStage({
    required this.height,
    required this.width,
    required this.controller,
    required this.cameraInitializing,
    required this.cameraReady,
    required this.cameraError,
    required this.onRetry,
    required this.onPickFromGallery,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: width,
      height: height,
      child: Stack(
        children: [
          Positioned.fill(
            child: ClipRRect(
              borderRadius: BorderRadius.circular(38),
              child: DecoratedBox(
                decoration: BoxDecoration(
                  color: AppColors.surfaceSoft,
                  borderRadius: BorderRadius.circular(38),
                ),
                child: _buildBody(context),
              ),
            ),
          ),
          Positioned(
            top: 12,
            left: 12,
            child: _CameraOverlayGalleryButton(
              onTap: onPickFromGallery,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBody(BuildContext context) {
    if (cameraInitializing) {
      return const Center(
        child: CircularProgressIndicator(),
      );
    }

    if (cameraError != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(22),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(
                Icons.camera_alt_outlined,
                color: AppColors.textSecondary,
                size: 34,
              ),
              const SizedBox(height: 12),
              Text(
                cameraError!,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: AppColors.textSecondary,
                  height: 1.35,
                ),
              ),
              const SizedBox(height: 14),
              OutlinedButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh_rounded),
                label: const Text('Повторить'),
              ),
            ],
          ),
        ),
      );
    }

    final camera = controller;
    if (camera == null || !cameraReady || !camera.value.isInitialized) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Text(
            'Камера не готова',
            textAlign: TextAlign.center,
            style: TextStyle(
              color: AppColors.textSecondary,
              height: 1.35,
            ),
          ),
        ),
      );
    }

    final previewSize = camera.value.previewSize;
    if (previewSize == null) {
      return CameraPreview(camera);
    }

    final previewWidth = previewSize.height;
    final previewHeight = previewSize.width;

    return Stack(
      fit: StackFit.expand,
      children: [
        ClipRect(
          child: FittedBox(
            fit: BoxFit.cover,
            clipBehavior: Clip.hardEdge,
            child: SizedBox(
              width: previewWidth,
              height: previewHeight,
              child: CameraPreview(camera),
            ),
          ),
        ),
        DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: [
                Colors.black.withValues(alpha: 0.08),
                Colors.transparent,
                Colors.black.withValues(alpha: 0.14),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _CameraOverlayGalleryButton extends StatelessWidget {
  final Future<void> Function(BuildContext anchorContext)? onTap;

  const _CameraOverlayGalleryButton({
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final enabled = onTap != null;

    return Opacity(
      opacity: enabled ? 1 : 0.54,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: enabled ? () => onTap!(context) : null,
          borderRadius: BorderRadius.circular(999),
          splashColor: Colors.white.withValues(alpha: 0.05),
          highlightColor: Colors.white.withValues(alpha: 0.03),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(999),
            child: BackdropFilter(
              filter: ImageFilter.blur(sigmaX: 16, sigmaY: 16),
              child: Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 14,
                  vertical: 10,
                ),
                decoration: BoxDecoration(
                  color: AppColors.surfaceGlassStrong(0.52),
                  borderRadius: BorderRadius.circular(999),
                  border: Border.all(
                    color: AppColors.softStroke(0.16),
                  ),
                  boxShadow: [
                    BoxShadow(
                      color: AppColors.shadow(0.18),
                      blurRadius: 18,
                      offset: const Offset(0, 8),
                    ),
                  ],
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: const [
                    Icon(
                      Icons.photo_library_outlined,
                      color: AppColors.textPrimary,
                      size: 18,
                    ),
                    SizedBox(width: 8),
                    Text(
                      'Галерея',
                      style: TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 13,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _CameraActionsRow extends StatelessWidget {
  final VoidCallback? onToggleFlash;
  final VoidCallback? onCapture;
  final VoidCallback? onCaptureLongPress;
  final VoidCallback? onToggleCamera;
  final bool flashEnabled;
  final bool recordingVideo;

  const _CameraActionsRow({
    required this.onToggleFlash,
    required this.onCapture,
    required this.onCaptureLongPress,
    required this.onToggleCamera,
    required this.flashEnabled,
    required this.recordingVideo,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 78,
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          _RoundActionButton(
            icon: flashEnabled
                ? Icons.flash_on_rounded
                : Icons.flash_off_rounded,
            onTap: onToggleFlash,
          ),
          const SizedBox(width: 16),
          _CaptureButton(
            busy: onCapture == null && !recordingVideo,
            recording: recordingVideo,
            onTap: onCapture,
            onLongPress: onCaptureLongPress,
          ),
          const SizedBox(width: 16),
          _RoundActionButton(
            icon: Icons.flip_camera_ios_rounded,
            onTap: onToggleCamera,
          ),
        ],
      ),
    );
  }
}

class _RoundActionButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onTap;

  const _RoundActionButton({
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final enabled = onTap != null;

    return Opacity(
      opacity: enabled ? 1 : 0.48,
      child: Material(
        color: Colors.transparent,
        shape: const CircleBorder(),
        child: InkWell(
          onTap: onTap,
          customBorder: const CircleBorder(),
          splashColor: Colors.white.withValues(alpha: 0.05),
          highlightColor: Colors.white.withValues(alpha: 0.03),
          child: Container(
            width: 48,
            height: 48,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: AppColors.surfaceGlassStrong(0.46),
              border: Border.all(
                color: AppColors.softStroke(enabled ? 0.18 : 0.08),
              ),
              boxShadow: enabled
                  ? [
                      BoxShadow(
                        color: AppColors.shadow(0.18),
                        blurRadius: 18,
                        offset: const Offset(0, 8),
                      ),
                    ]
                  : null,
            ),
            child: Icon(
              icon,
              size: 22,
              color: enabled
                  ? AppColors.textPrimary
                  : AppColors.textSecondary.withValues(alpha: 0.64),
            ),
          ),
        ),
      ),
    );
  }
}

class _FeedEntryAction extends StatelessWidget {
  final Group? group;
  final VoidCallback? onTap;

  const _FeedEntryAction({
    required this.group,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final avatarUrl = group?.avatarUrl;
    final groupName = group?.name.trim();

    final enabled = onTap != null;

    return Opacity(
      opacity: enabled ? 1 : 0.62,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(999),
          splashColor: Colors.white.withValues(alpha: 0.04),
          highlightColor: Colors.white.withValues(alpha: 0.02),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  width: 34,
                  height: 34,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    border: Border.all(
                      color: AppColors.accentLight.withValues(alpha: 0.40),
                      width: 1.6,
                    ),
                  ),
                  child: CircleAvatar(
                    radius: 17,
                    backgroundColor: AppColors.surfaceGlassStrong(0.42),
                    backgroundImage:
                        avatarUrl != null && avatarUrl.trim().isNotEmpty
                            ? NetworkImage(avatarUrl)
                            : null,
                    child: avatarUrl == null || avatarUrl.trim().isEmpty
                        ? const Icon(
                            Icons.groups_rounded,
                            color: AppColors.textSecondary,
                            size: 17,
                          )
                        : null,
                  ),
                ),
                const SizedBox(width: 9),
                Flexible(
                  child: Text(
                    groupName == null || groupName.isEmpty
                        ? 'Лента группы'
                        : 'Лента: $groupName',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: enabled
                          ? AppColors.textPrimary
                          : AppColors.textSecondary.withValues(alpha: 0.64),
                      fontSize: 15,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
                const SizedBox(width: 5),
                Icon(
                  Icons.keyboard_arrow_down_rounded,
                  size: 22,
                  color: enabled
                      ? AppColors.textPrimary
                      : AppColors.textSecondary.withValues(alpha: 0.44),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _CaptureButton extends StatelessWidget {
  final bool busy;
  final bool recording;
  final VoidCallback? onTap;
  final VoidCallback? onLongPress;

  const _CaptureButton({
    required this.busy,
    required this.recording,
    required this.onTap,
    required this.onLongPress,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      onLongPress: onLongPress,
      child: AnimatedScale(
        scale: recording ? 1.04 : 1.0,
        duration: const Duration(milliseconds: 180),
        curve: Curves.easeOutCubic,
        child: SizedBox(
          width: 74,
          height: 74,
          child: Center(
            child: busy
                ? Container(
                    width: 70,
                    height: 70,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: AppColors.surfaceGlassStrong(0.42),
                      border: Border.all(
                        color: AppColors.softStroke(0.16),
                        width: 2,
                      ),
                    ),
                    child: const Center(
                      child: SizedBox(
                        width: 24,
                        height: 24,
                        child: CircularProgressIndicator(strokeWidth: 2.4),
                      ),
                    ),
                  )
                : AnimatedContainer(
                    duration: const Duration(milliseconds: 180),
                    curve: Curves.easeOutCubic,
                    width: 68,
                    height: 68,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: recording
                          ? Colors.redAccent.withValues(alpha: 0.10)
                          : Colors.white.withValues(alpha: 0.05),
                      border: Border.all(
                        color: recording
                            ? Colors.redAccent
                            : AppColors.accentLight,
                        width: recording ? 4.4 : 4,
                      ),
                      boxShadow: [
                        BoxShadow(
                          color: recording
                              ? Colors.redAccent.withValues(alpha: 0.22)
                              : AppColors.accentSecondary.withValues(alpha: 0.14),
                          blurRadius: 24,
                          offset: const Offset(0, 10),
                        ),
                      ],
                    ),
                    child: Center(
                      child: AnimatedContainer(
                        duration: const Duration(milliseconds: 180),
                        curve: Curves.easeOutCubic,
                        width: recording ? 24 : 52,
                        height: recording ? 24 : 52,
                        decoration: BoxDecoration(
                          shape:
                              recording ? BoxShape.rectangle : BoxShape.circle,
                          borderRadius:
                              recording ? BorderRadius.circular(8) : null,
                          color: recording ? Colors.redAccent : Colors.white,
                          border: Border.all(
                            color: Colors.black.withValues(alpha: 0.82),
                            width: 2.2,
                          ),
                        ),
                      ),
                    ),
                  ),
          ),
        ),
      ),
    );
  }
}

class _EmptyGroupsHintCard extends StatelessWidget {
  final VoidCallback? onOpenProfile;

  const _EmptyGroupsHintCard({
    this.onOpenProfile,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: InMomentSurfaceTone.base,
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
      borderRadius: BorderRadius.circular(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Пока нет группы',
            style: TextStyle(
              color: AppColors.textPrimary,
              fontSize: 14,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 6),
          const Text(
            'Создай группу или выбери активную в профиле, чтобы публиковать фото.',
            style: TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12,
              height: 1.35,
            ),
          ),
          if (onOpenProfile != null) ...[
            const SizedBox(height: 12),
            FilledButton(
              onPressed: onOpenProfile,
              style: FilledButton.styleFrom(
                minimumSize: const Size(0, 38),
                padding: const EdgeInsets.symmetric(horizontal: 16),
              ),
              child: const Text('Открыть профиль'),
            ),
          ],
        ],
      ),
    );
  }
}
class _GlassMenuItemValue<T> {
  final T value;
  final IconData icon;
  final String text;

  const _GlassMenuItemValue({
    required this.value,
    required this.icon,
    required this.text,
  });
}

Future<T?> _showGlassAnchorMenu<T>({
  required BuildContext context,
  required BuildContext anchorContext,
  required List<_GlassMenuItemValue<T>> items,
}) {
  final overlay = Overlay.of(context).context.findRenderObject() as RenderBox;
  final button = anchorContext.findRenderObject() as RenderBox;

  final offset = button.localToGlobal(Offset.zero, ancestor: overlay);
  final rect = offset & button.size;

  return showMenu<T>(
    context: context,
    color: Colors.transparent,
    elevation: 0,
    position: RelativeRect.fromLTRB(
      rect.left,
      rect.bottom + 8,
      overlay.size.width - rect.right,
      overlay.size.height - rect.bottom,
    ),
    items: [
      PopupMenuItem<T>(
        enabled: false,
        padding: EdgeInsets.zero,
        child: ClipRRect(
          borderRadius: BorderRadius.circular(22),
          child: BackdropFilter(
            filter: ImageFilter.blur(sigmaX: 18, sigmaY: 18),
            child: Container(
              width: 210,
              padding: const EdgeInsets.symmetric(vertical: 8),
              decoration: BoxDecoration(
                color: const Color(0xFF211329).withValues(alpha: 0.78),
                borderRadius: BorderRadius.circular(22),
                border: Border.all(
                  color: Colors.white.withValues(alpha: 0.10),
                ),
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: items.map((item) {
                  return InkWell(
                    onTap: () => Navigator.of(context).pop(item.value),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 14,
                        vertical: 12,
                      ),
                      child: Row(
                        children: [
                          Icon(
                            item.icon,
                            color: AppColors.textPrimary,
                            size: 20,
                          ),
                          const SizedBox(width: 12),
                          Text(
                            item.text,
                            style: const TextStyle(
                              color: AppColors.textPrimary,
                              fontSize: 15,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ],
                      ),
                    ),
                  );
                }).toList(),
              ),
            ),
          ),
        ),
      ),
    ],
  );
}