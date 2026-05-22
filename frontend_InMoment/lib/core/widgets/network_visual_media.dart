import 'dart:ui';

import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:video_player/video_player.dart';

import '../theme/app_colors.dart';

bool isVideoContentType(String contentType) {
  return contentType.toLowerCase().startsWith('video/');
}

bool isImageContentType(String contentType) {
  return contentType.toLowerCase().startsWith('image/');
}

int? _cacheExtent(double logicalExtent, double pixelRatio) {
  if (!logicalExtent.isFinite || logicalExtent <= 0) {
    return null;
  }

  final physical = (logicalExtent * pixelRatio).round();
  return physical.clamp(160, 1440).toInt();
}

class NetworkVisualMedia extends StatelessWidget {
  final String url;
  final String contentType;
  final BoxFit fit;

  final bool allowInlineVideo;
  final bool autoplay;
  final bool looping;
  final bool startMuted;
  final bool showControls;
  final bool allowPlaybackSpeedChanging;
  final bool showVideoBadge;

  final String placeholderLabel;

  const NetworkVisualMedia({
    super.key,
    required this.url,
    required this.contentType,
    this.fit = BoxFit.cover,
    this.allowInlineVideo = false,
    this.autoplay = false,
    this.looping = true,
    this.startMuted = true,
    this.showControls = false,
    this.allowPlaybackSpeedChanging = false,
    this.showVideoBadge = true,
    this.placeholderLabel = 'Не удалось загрузить медиа',
  });

  @override
  Widget build(BuildContext context) {
    final trimmedUrl = url.trim();

    if (trimmedUrl.isEmpty) {
      return _MediaPlaceholder(
        icon: Icons.broken_image_outlined,
        label: placeholderLabel,
      );
    }

    if (isVideoContentType(contentType)) {
      if (!allowInlineVideo) {
        return _StaticNetworkVideoPreview(
          url: trimmedUrl,
          fit: fit,
          showBadge: showVideoBadge,
        );
      }

      if (showControls) {
        return _ControlledNetworkVideoPlayer(
          url: trimmedUrl,
          autoplay: autoplay,
          looping: looping,
          startMuted: startMuted,
          allowPlaybackSpeedChanging: allowPlaybackSpeedChanging,
          showBadge: showVideoBadge,
        );
      }

      return _LightweightNetworkVideoPlayer(
        url: trimmedUrl,
        autoplay: autoplay,
        looping: looping,
        startMuted: startMuted,
        showBadge: showVideoBadge,
        fit: fit,
      );
    }

    return LayoutBuilder(
      builder: (context, constraints) {
        final pixelRatio = MediaQuery.devicePixelRatioOf(context);
        final cacheWidth = _cacheExtent(
          constraints.maxWidth,
          pixelRatio,
        );
        final cacheHeight = _cacheExtent(
          constraints.maxHeight,
          pixelRatio,
        );

        return CachedNetworkImage(
          imageUrl: trimmedUrl,
          fit: fit,
          memCacheWidth: cacheWidth,
          memCacheHeight: cacheHeight,
          maxWidthDiskCache: cacheWidth == null ? null : cacheWidth * 2,
          maxHeightDiskCache: cacheHeight == null ? null : cacheHeight * 2,
          filterQuality: FilterQuality.medium,
          fadeInDuration: const Duration(milliseconds: 120),
          fadeOutDuration: const Duration(milliseconds: 80),
          placeholder: (_, _) => const _MediaPlaceholder(
            icon: Icons.photo_outlined,
            label: 'Загрузка…',
            loading: true,
          ),
          errorWidget: (_, _, _) => _MediaPlaceholder(
            icon: Icons.broken_image_outlined,
            label: placeholderLabel,
          ),
        );
      },
    );
  }
}

class _ControlledNetworkVideoPlayer extends StatefulWidget {
  final String url;
  final bool autoplay;
  final bool looping;
  final bool startMuted;
  final bool allowPlaybackSpeedChanging;
  final bool showBadge;

  const _ControlledNetworkVideoPlayer({
    required this.url,
    required this.autoplay,
    required this.looping,
    required this.startMuted,
    required this.allowPlaybackSpeedChanging,
    required this.showBadge,
  });

  @override
  State<_ControlledNetworkVideoPlayer> createState() =>
      _ControlledNetworkVideoPlayerState();
}

class _ControlledNetworkVideoPlayerState
    extends State<_ControlledNetworkVideoPlayer>
    with AutomaticKeepAliveClientMixin {
  VideoPlayerController? _controller;

  bool _failed = false;
  bool _muted = true;
  double _speed = 1.0;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();
    _muted = widget.startMuted;
    _init();
  }

  @override
  void didUpdateWidget(covariant _ControlledNetworkVideoPlayer oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.url != widget.url ||
        oldWidget.autoplay != widget.autoplay ||
        oldWidget.looping != widget.looping ||
        oldWidget.startMuted != widget.startMuted) {
      _muted = widget.startMuted;
      _speed = 1.0;
      _disposeController();
      _init();
    }
  }

  Future<void> _init() async {
    try {
      final controller =
          VideoPlayerController.networkUrl(Uri.parse(widget.url));

      await controller.initialize();
      await controller.setLooping(widget.looping);
      await controller.setVolume(_muted ? 0 : 1);
      await controller.setPlaybackSpeed(_speed);

      controller.addListener(_handleControllerChanged);

      if (widget.autoplay) {
        await controller.play();
      }

      if (!mounted) {
        controller.removeListener(_handleControllerChanged);
        controller.dispose();
        return;
      }

      setState(() {
        _controller = controller;
        _failed = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _failed = true;
      });
    }
  }

  void _handleControllerChanged() {
    if (!mounted) return;
    setState(() {});
  }

  Future<void> _togglePlayback() async {
    final controller = _controller;
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
    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) return;

    final nextMuted = !_muted;
    await controller.setVolume(nextMuted ? 0 : 1);

    if (!mounted) return;
    setState(() {
      _muted = nextMuted;
    });
  }

  Future<void> _seekBy(Duration offset) async {
    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) return;

    final current = controller.value.position;
    final duration = controller.value.duration;

    final targetMs = (current + offset).inMilliseconds.clamp(
          0,
          duration.inMilliseconds,
        );

    await controller.seekTo(Duration(milliseconds: targetMs.toInt()));
  }

  Future<void> _pickSpeed(BuildContext anchorContext) async {
    final selected = await _showSpeedAnchorMenu(
      context: context,
      anchorContext: anchorContext,
      currentSpeed: _speed,
    );

    if (selected == null) return;

    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) return;

    await controller.setPlaybackSpeed(selected);

    if (!mounted) return;
    setState(() {
      _speed = selected;
    });
  }

  void _disposeController() {
    final controller = _controller;
    _controller = null;

    if (controller != null) {
      try {
        controller.removeListener(_handleControllerChanged);
      } catch (_) {}

      try {
        controller.dispose();
      } catch (_) {}
    }
  }

  String _formatPosition(Duration value) {
    final total = value.inSeconds;
    final minutes = total ~/ 60;
    final seconds = total % 60;
    return '$minutes:${seconds.toString().padLeft(2, '0')}';
  }

  @override
  void dispose() {
    _disposeController();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);

    if (_failed) {
      return const _MediaPlaceholder(
        icon: Icons.videocam_off_rounded,
        label: 'Не удалось загрузить видео',
      );
    }

    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) {
      return const _MediaPlaceholder(
        icon: Icons.videocam_rounded,
        label: 'Загрузка видео…',
        loading: true,
      );
    }

    final value = controller.value;
    final duration = value.duration;
    final position = value.position;
    final progress = duration.inMilliseconds <= 0
        ? 0.0
        : (position.inMilliseconds / duration.inMilliseconds).clamp(0.0, 1.0);

    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: _togglePlayback,
      child: Stack(
        fit: StackFit.expand,
        children: [
          Center(
            child: AspectRatio(
              aspectRatio: value.aspectRatio > 0 ? value.aspectRatio : 4 / 5,
              child: VideoPlayer(controller),
            ),
          ),
          DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: [
                  Colors.black.withValues(alpha: 0.18),
                  Colors.transparent,
                  Colors.black.withValues(alpha: 0.72),
                ],
              ),
            ),
          ),
          if (widget.showBadge)
            const Positioned(
              top: 10,
              right: 10,
              child: _VideoBadge(),
            ),
          Center(
            child: AnimatedOpacity(
              opacity: value.isPlaying ? 0 : 1,
              duration: const Duration(milliseconds: 160),
              child: Container(
                width: 66,
                height: 66,
                decoration: BoxDecoration(
                  color: Colors.black.withValues(alpha: 0.42),
                  shape: BoxShape.circle,
                  border: Border.all(
                    color: Colors.white.withValues(alpha: 0.14),
                  ),
                ),
                child: const Icon(
                  Icons.play_arrow_rounded,
                  color: Colors.white,
                  size: 38,
                ),
              ),
            ),
          ),
          Positioned(
            left: 12,
            right: 12,
            bottom: 10,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                SliderTheme(
                  data: SliderTheme.of(context).copyWith(
                    trackHeight: 3,
                    thumbShape: const RoundSliderThumbShape(
                      enabledThumbRadius: 6,
                    ),
                    overlayShape: const RoundSliderOverlayShape(
                      overlayRadius: 14,
                    ),
                  ),
                  child: Slider(
                    value: progress,
                    min: 0,
                    max: 1,
                    onChanged: (next) async {
                      final targetMs =
                          (duration.inMilliseconds * next).round();
                      await controller.seekTo(
                        Duration(milliseconds: targetMs),
                      );
                    },
                  ),
                ),
                Row(
                  children: [
                    _VideoControlButton(
                      icon: value.isPlaying
                          ? Icons.pause_rounded
                          : Icons.play_arrow_rounded,
                      onTap: _togglePlayback,
                    ),
                    const SizedBox(width: 8),
                    _VideoControlButton(
                      icon: _muted
                          ? Icons.volume_off_rounded
                          : Icons.volume_up_rounded,
                      onTap: _toggleMute,
                    ),
                    const SizedBox(width: 10),
                    Text(
                      '${_formatPosition(position)} / ${_formatPosition(duration)}',
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 12,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const Spacer(),
                    _VideoControlButton(
                      icon: Icons.replay_10_rounded,
                      onTap: () => _seekBy(const Duration(seconds: -10)),
                    ),
                    const SizedBox(width: 8),
                    _SpeedButton(
                      label: _speed == 1.0 ? '1x' : '${_speed}x',
                      onTap: widget.allowPlaybackSpeedChanging
                          ? _pickSpeed
                          : null,
                    ),
                    const SizedBox(width: 8),
                    _VideoControlButton(
                      icon: Icons.forward_10_rounded,
                      onTap: () => _seekBy(const Duration(seconds: 10)),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _VideoControlButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onTap;

  const _VideoControlButton({
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(999),
      child: Container(
        width: 34,
        height: 34,
        decoration: BoxDecoration(
          color: Colors.black.withValues(alpha: 0.46),
          shape: BoxShape.circle,
        ),
        child: Icon(
          icon,
          color: Colors.white,
          size: 20,
        ),
      ),
    );
  }
}

class _SpeedButton extends StatelessWidget {
  final String label;
  final void Function(BuildContext anchorContext)? onTap;

  const _SpeedButton({
    required this.label,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap == null ? null : () => onTap!(context),
      borderRadius: BorderRadius.circular(999),
      child: Container(
        height: 34,
        constraints: const BoxConstraints(minWidth: 44),
        padding: const EdgeInsets.symmetric(horizontal: 10),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: Colors.black.withValues(alpha: 0.46),
          borderRadius: BorderRadius.circular(999),
        ),
        child: Text(
          label,
          style: const TextStyle(
            color: Colors.white,
            fontSize: 12,
            fontWeight: FontWeight.w900,
          ),
        ),
      ),
    );
  }
}

class _LightweightNetworkVideoPlayer extends StatefulWidget {
  final String url;
  final bool autoplay;
  final bool looping;
  final bool startMuted;
  final bool showBadge;
  final BoxFit fit;

  const _LightweightNetworkVideoPlayer({
    required this.url,
    required this.autoplay,
    required this.looping,
    required this.startMuted,
    required this.showBadge,
    required this.fit,
  });

  @override
  State<_LightweightNetworkVideoPlayer> createState() =>
      _LightweightNetworkVideoPlayerState();
}

class _LightweightNetworkVideoPlayerState
    extends State<_LightweightNetworkVideoPlayer>
    with AutomaticKeepAliveClientMixin {
  VideoPlayerController? _controller;
  bool _failed = false;
  bool _playing = false;
  bool _muted = true;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();
    _muted = widget.startMuted;
    _init();
  }

  @override
  void didUpdateWidget(covariant _LightweightNetworkVideoPlayer oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.url != widget.url ||
        oldWidget.autoplay != widget.autoplay ||
        oldWidget.looping != widget.looping ||
        oldWidget.startMuted != widget.startMuted) {
      _muted = widget.startMuted;
      _disposeController();
      _init();
    }
  }

  Future<void> _init() async {
    try {
      final controller =
          VideoPlayerController.networkUrl(Uri.parse(widget.url));

      await controller.initialize();
      await controller.setLooping(widget.looping);
      await controller.setVolume(_muted ? 0 : 1);

      controller.addListener(_handleControllerChanged);

      if (widget.autoplay) {
        await controller.play();
      }

      if (!mounted) {
        controller.removeListener(_handleControllerChanged);
        controller.dispose();
        return;
      }

      setState(() {
        _controller = controller;
        _failed = false;
        _playing = controller.value.isPlaying;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _failed = true;
      });
    }
  }

  void _handleControllerChanged() {
    final controller = _controller;
    if (!mounted || controller == null) return;

    final nextPlaying = controller.value.isPlaying;
    if (_playing != nextPlaying) {
      setState(() {
        _playing = nextPlaying;
      });
    }
  }

  Future<void> _togglePlayback() async {
    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) return;

    if (controller.value.isPlaying) {
      await controller.pause();
    } else {
      await controller.play();
    }

    if (!mounted) return;
    setState(() {
      _playing = controller.value.isPlaying;
    });
  }

  Future<void> _toggleMute() async {
    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) return;

    final nextMuted = !_muted;
    await controller.setVolume(nextMuted ? 0 : 1);

    if (!mounted) return;
    setState(() {
      _muted = nextMuted;
    });
  }

  void _disposeController() {
    final controller = _controller;
    _controller = null;

    if (controller != null) {
      try {
        controller.removeListener(_handleControllerChanged);
      } catch (_) {}

      try {
        controller.dispose();
      } catch (_) {}
    }
  }

  @override
  void dispose() {
    _disposeController();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);

    if (_failed) {
      return const _MediaPlaceholder(
        icon: Icons.videocam_off_rounded,
        label: 'Не удалось загрузить видео',
      );
    }

    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) {
      return const _MediaPlaceholder(
        icon: Icons.videocam_rounded,
        label: 'Загрузка видео…',
        loading: true,
      );
    }

    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: _togglePlayback,
      child: Stack(
        fit: StackFit.expand,
        children: [
          _FittedVideoFrame(
            controller: controller,
            fit: widget.fit,
          ),
          Positioned(
            top: 10,
            left: 10,
            child: GestureDetector(
              onTap: _toggleMute,
              child: Container(
                width: 36,
                height: 36,
                decoration: BoxDecoration(
                  color: Colors.black.withValues(alpha: 0.56),
                  shape: BoxShape.circle,
                  border: Border.all(
                    color: Colors.white.withValues(alpha: 0.12),
                  ),
                ),
                child: Icon(
                  _muted ? Icons.volume_off_rounded : Icons.volume_up_rounded,
                  color: Colors.white,
                  size: 18,
                ),
              ),
            ),
          ),
          AnimatedOpacity(
            opacity: _playing ? 0 : 1,
            duration: const Duration(milliseconds: 160),
            child: Center(
              child: IgnorePointer(
                ignoring: _playing,
                child: Container(
                  width: 66,
                  height: 66,
                  decoration: BoxDecoration(
                    color: Colors.black.withValues(alpha: 0.36),
                    shape: BoxShape.circle,
                    border: Border.all(
                      color: Colors.white.withValues(alpha: 0.14),
                    ),
                  ),
                  child: const Icon(
                    Icons.play_arrow_rounded,
                    color: Colors.white,
                    size: 36,
                  ),
                ),
              ),
            ),
          ),
          if (widget.showBadge)
            const Positioned(
              top: 10,
              right: 10,
              child: _VideoBadge(),
            ),
        ],
      ),
    );
  }
}

class _StaticNetworkVideoPreview extends StatefulWidget {
  final String url;
  final BoxFit fit;
  final bool showBadge;

  const _StaticNetworkVideoPreview({
    required this.url,
    required this.fit,
    required this.showBadge,
  });

  @override
  State<_StaticNetworkVideoPreview> createState() =>
      _StaticNetworkVideoPreviewState();
}

class _StaticNetworkVideoPreviewState extends State<_StaticNetworkVideoPreview> {
  VideoPlayerController? _controller;
  bool _failed = false;
  int _generation = 0;

  @override
  void initState() {
    super.initState();
    _initPreview();
  }

  @override
  void didUpdateWidget(covariant _StaticNetworkVideoPreview oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.url != widget.url || oldWidget.fit != widget.fit) {
      _disposeController();
      _initPreview();
    }
  }

  Future<void> _initPreview() async {
    final generation = ++_generation;

    try {
      final controller = VideoPlayerController.networkUrl(
        Uri.parse(widget.url),
        videoPlayerOptions: VideoPlayerOptions(mixWithOthers: true),
      );

      await controller.initialize();
      await controller.setLooping(false);
      await controller.setVolume(0);
      await controller.pause();

      if (controller.value.duration > Duration.zero) {
        await controller.seekTo(Duration.zero);
      }

      if (!mounted || generation != _generation) {
        await controller.dispose();
        return;
      }

      setState(() {
        _controller = controller;
        _failed = false;
      });
    } catch (_) {
      if (!mounted || generation != _generation) return;
      setState(() {
        _failed = true;
      });
    }
  }

  void _disposeController() {
    _generation++;
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
    _disposeController();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final controller = _controller;
    final initialized = controller != null && controller.value.isInitialized;

    return LayoutBuilder(
      builder: (context, constraints) {
        final compact = constraints.maxWidth < 108 || constraints.maxHeight < 108;
        final rawShortest = constraints.biggest.shortestSide;
        final shortest = rawShortest.isFinite && rawShortest > 0 ? rawShortest : 76.0;
        final playSize = compact
            ? (shortest * 0.34).clamp(16.0, 24.0).toDouble()
            : shortest.clamp(48.0, 64.0).toDouble();
        final iconSize = compact ? playSize * 0.70 : playSize * 0.62;

        return Stack(
          fit: StackFit.expand,
          children: [
            if (initialized)
              _FittedVideoFrame(
                controller: controller,
                fit: widget.fit,
              )
            else
              DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [
                      AppColors.surface.withValues(alpha: 0.94),
                      AppColors.card.withValues(alpha: 0.72),
                      Colors.black.withValues(alpha: 0.74),
                    ],
                  ),
                ),
              ),
            DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  colors: [
                    Colors.black.withValues(alpha: compact ? 0.04 : 0.10),
                    Colors.transparent,
                    Colors.black.withValues(alpha: compact ? 0.18 : 0.30),
                  ],
                ),
              ),
            ),
            Center(
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                width: playSize,
                height: playSize,
                decoration: BoxDecoration(
                  color: initialized
                      ? Colors.black.withValues(alpha: compact ? 0.42 : 0.38)
                      : Colors.white.withValues(alpha: compact ? 0.88 : 0.92),
                  shape: BoxShape.circle,
                  border: Border.all(
                    color: initialized
                        ? Colors.white.withValues(alpha: 0.22)
                        : Colors.black.withValues(alpha: 0.08),
                  ),
                  boxShadow: compact
                      ? const []
                      : [
                          BoxShadow(
                            color: Colors.black.withValues(alpha: 0.30),
                            blurRadius: 16,
                            offset: const Offset(0, 8),
                          ),
                        ],
                ),
                child: Icon(
                  _failed
                      ? Icons.videocam_off_rounded
                      : Icons.play_arrow_rounded,
                  color: initialized ? Colors.white : Colors.black,
                  size: iconSize,
                ),
              ),
            ),
            if (widget.showBadge && !compact)
              const Positioned(
                top: 10,
                right: 10,
                child: _VideoBadge(),
              ),
          ],
        );
      },
    );
  }
}

class _FittedVideoFrame extends StatelessWidget {
  final VideoPlayerController controller;
  final BoxFit fit;

  const _FittedVideoFrame({
    required this.controller,
    required this.fit,
  });

  @override
  Widget build(BuildContext context) {
    final size = controller.value.size;
    final width = size.width > 0 ? size.width : 9.0;
    final height = size.height > 0 ? size.height : 16.0;

    return ClipRect(
      child: FittedBox(
        fit: fit,
        clipBehavior: Clip.hardEdge,
        child: SizedBox(
          width: width,
          height: height,
          child: VideoPlayer(controller),
        ),
      ),
    );
  }
}
class _VideoBadge extends StatelessWidget {
  const _VideoBadge();

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: Colors.black.withValues(alpha: 0.62),
        borderRadius: BorderRadius.circular(999),
      ),
      child: const Padding(
        padding: EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.videocam_rounded,
              size: 14,
              color: Colors.white,
            ),
            SizedBox(width: 5),
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
      ),
    );
  }
}

class _MediaPlaceholder extends StatelessWidget {
  final IconData icon;
  final String label;
  final bool loading;

  const _MediaPlaceholder({
    required this.icon,
    required this.label,
    this.loading = false,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      color: AppColors.surface,
      alignment: Alignment.center,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (loading)
            const SizedBox(
              width: 22,
              height: 22,
              child: CircularProgressIndicator(strokeWidth: 2),
            )
          else
            Icon(
              icon,
              color: AppColors.textSecondary,
              size: 34,
            ),
          const SizedBox(height: 10),
          Text(
            label,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 13,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}
Future<double?> _showSpeedAnchorMenu({
  required BuildContext context,
  required BuildContext anchorContext,
  required double currentSpeed,
}) {
  final overlay = Overlay.of(context).context.findRenderObject() as RenderBox;
  final button = anchorContext.findRenderObject() as RenderBox;

  final offset = button.localToGlobal(Offset.zero, ancestor: overlay);
  final rect = offset & button.size;

  const speeds = [0.5, 1.0, 1.25, 1.5, 2.0];

  return showMenu<double>(
    context: context,
    color: Colors.transparent,
    elevation: 0,
    position: RelativeRect.fromLTRB(
      rect.left - 88,
      rect.top - 220,
      overlay.size.width - rect.right,
      overlay.size.height - rect.top,
    ),
    items: [
      PopupMenuItem<double>(
        enabled: false,
        padding: EdgeInsets.zero,
        child: ClipRRect(
          borderRadius: BorderRadius.circular(22),
          child: BackdropFilter(
            filter: ImageFilter.blur(sigmaX: 18, sigmaY: 18),
            child: Container(
              width: 180,
              padding: const EdgeInsets.symmetric(vertical: 8),
              decoration: BoxDecoration(
                color: AppColors.card.withValues(alpha: 0.78),
                borderRadius: BorderRadius.circular(22),
                border: Border.all(
                  color: Colors.white.withValues(alpha: 0.10),
                ),
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: speeds.map((speed) {
                  final selected = speed == currentSpeed;
                  final label = speed == 1.0 ? '1x' : '${speed}x';

                  return InkWell(
                    onTap: () => Navigator.of(context).pop(speed),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 14,
                        vertical: 11,
                      ),
                      child: Row(
                        children: [
                          Expanded(
                            child: Text(
                              label,
                              style: TextStyle(
                                color: AppColors.textPrimary,
                                fontSize: 15,
                                fontWeight:
                                    selected ? FontWeight.w900 : FontWeight.w700,
                              ),
                            ),
                          ),
                          if (selected)
                            const Icon(
                              Icons.check_rounded,
                              color: AppColors.accent,
                              size: 20,
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