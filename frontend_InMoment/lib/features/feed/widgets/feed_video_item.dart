import 'package:flutter/material.dart';
import 'package:video_player/video_player.dart';

import '../../../core/video/visibility_detector_wrapper.dart';

class FeedVideoItem extends StatefulWidget {
  final String videoUrl;
  final String photoId;

  const FeedVideoItem({
    super.key,
    required this.videoUrl,
    required this.photoId,
  });

  @override
  State<FeedVideoItem> createState() => _FeedVideoItemState();
}

class _FeedVideoItemState extends State<FeedVideoItem>
    with AutomaticKeepAliveClientMixin {
  VideoPlayerController? _controller;

  bool _initialized = false;
  bool _initializing = false;
  bool _failed = false;
  bool _isVisible = false;
  int _initGeneration = 0;

  @override
  bool get wantKeepAlive => false;

  @override
  void didUpdateWidget(covariant FeedVideoItem oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.videoUrl != widget.videoUrl ||
        oldWidget.photoId != widget.photoId) {
      _initGeneration++;
      _disposeController();
      _initialized = false;
      _initializing = false;
      _failed = false;

      if (_isVisible) {
        _ensureInitialized();
      }
    }
  }

  Future<void> _ensureInitialized() async {
    if (_initialized || _initializing) return;

    _initializing = true;
    final generation = ++_initGeneration;

    try {
      final controller =
          VideoPlayerController.networkUrl(Uri.parse(widget.videoUrl));

      await controller.initialize();
      await controller.setLooping(false);
      await controller.setVolume(0);
      await controller.pause();

      if (!mounted || generation != _initGeneration) {
        await controller.dispose();
        return;
      }

      setState(() {
        _controller = controller;
        _initialized = true;
        _failed = false;
      });
    } catch (_) {
      if (!mounted || generation != _initGeneration) return;
      setState(() {
        _failed = true;
        _initialized = false;
      });
    } finally {
      if (mounted && generation == _initGeneration) {
        _initializing = false;
      }
    }
  }

  Future<void> _pause() async {
    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) return;

    if (controller.value.isPlaying) {
      await controller.pause();
    }
  }

  void _disposeController() {
    final controller = _controller;
    _controller = null;

    if (controller != null) {
      try {
        controller.dispose();
      } catch (_) {}
    }
  }

  @override
  void dispose() {
    _initGeneration++;
    _pause();
    _disposeController();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);

    final controller = _controller;
    final isReady = controller?.value.isInitialized == true;

    return VisibilityDetectorWrapper(
      onVisible: () async {
        _isVisible = true;
        await _ensureInitialized();
      },
      onHidden: () async {
        _isVisible = false;
        await _pause();
      },
      child: ClipRRect(
        borderRadius: BorderRadius.circular(18),
        child: ColoredBox(
          color: Colors.black,
          child: Stack(
            fit: StackFit.expand,
            children: [
              if (_failed)
                const _FeedVideoFailed()
              else if (isReady)
                FittedBox(
                  fit: BoxFit.cover,
                  clipBehavior: Clip.hardEdge,
                  child: SizedBox(
                    width: controller!.value.size.width,
                    height: controller.value.size.height,
                    child: VideoPlayer(controller),
                  ),
                )
              else
                const _FeedVideoPlaceholder(),

              const Positioned(
                top: 10,
                right: 10,
                child: _VideoBadge(),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _VideoBadge extends StatelessWidget {
  const _VideoBadge();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 10,
        vertical: 6,
      ),
      decoration: BoxDecoration(
        color: Colors.black.withValues(alpha: 0.60),
        borderRadius: BorderRadius.circular(999),
      ),
      child: const Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            Icons.videocam_rounded,
            size: 14,
            color: Colors.white,
          ),
          SizedBox(width: 4),
          Text(
            'Видео',
            style: TextStyle(
              color: Colors.white,
              fontSize: 11,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

class _FeedVideoPlaceholder extends StatelessWidget {
  const _FeedVideoPlaceholder();

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: SizedBox(
        width: 24,
        height: 24,
        child: CircularProgressIndicator(strokeWidth: 2),
      ),
    );
  }
}

class _FeedVideoFailed extends StatelessWidget {
  const _FeedVideoFailed();

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            Icons.videocam_off_rounded,
            color: Colors.white70,
            size: 28,
          ),
          SizedBox(height: 8),
          Text(
            'Не удалось загрузить видео',
            style: TextStyle(
              color: Colors.white70,
              fontSize: 12,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}