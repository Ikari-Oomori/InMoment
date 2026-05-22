import 'dart:ui';

import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../theme/app_theme.dart';

enum InMomentSurfaceTone {
  base,
  elevated,
  overlay,
  danger,
  accent,
}

class InMomentSurface extends StatefulWidget {
  final Widget child;
  final EdgeInsetsGeometry? padding;
  final BorderRadius? borderRadius;
  final InMomentSurfaceTone tone;
  final bool blur;
  final double? width;
  final double? height;
  final VoidCallback? onTap;
  final bool showGlow;

  const InMomentSurface({
    super.key,
    required this.child,
    this.padding,
    this.borderRadius,
    this.tone = InMomentSurfaceTone.base,
    this.blur = true,
    this.width,
    this.height,
    this.onTap,
    this.showGlow = true,
  });

  @override
  State<InMomentSurface> createState() => _InMomentSurfaceState();
}

class _InMomentSurfaceState extends State<InMomentSurface> {
  bool _pressed = false;

  Color _backgroundColor() {
    switch (widget.tone) {
      case InMomentSurfaceTone.base:
        return AppColors.surfaceGlass(0.24);
      case InMomentSurfaceTone.elevated:
        return AppColors.surfaceGlassStrong(0.30);
      case InMomentSurfaceTone.overlay:
        return AppColors.surfaceGlass(0.18);
      case InMomentSurfaceTone.danger:
        return const Color(0xFF40232B).withValues(alpha: 0.24);
      case InMomentSurfaceTone.accent:
        return AppColors.accentSecondary.withValues(alpha: 0.18);
    }
  }

  Color _borderColor() {
    switch (widget.tone) {
      case InMomentSurfaceTone.base:
        return AppColors.softStroke(0.13);
      case InMomentSurfaceTone.elevated:
        return AppColors.purpleStroke(0.16);
      case InMomentSurfaceTone.overlay:
        return AppColors.softStroke(0.12);
      case InMomentSurfaceTone.danger:
        return AppColors.error.withValues(alpha: 0.16);
      case InMomentSurfaceTone.accent:
        return AppColors.accentSoft.withValues(alpha: 0.22);
    }
  }

  Gradient? _gradient() {
    switch (widget.tone) {
      case InMomentSurfaceTone.base:
      case InMomentSurfaceTone.elevated:
        return AppColors.glassGradient;
      case InMomentSurfaceTone.accent:
        return LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [
            AppColors.accentStrong.withValues(alpha: 0.22),
            AppColors.accentSecondary.withValues(alpha: 0.16),
            AppColors.surfaceElevated.withValues(alpha: 0.48),
          ],
        );
      case InMomentSurfaceTone.overlay:
      case InMomentSurfaceTone.danger:
        return null;
    }
  }

  List<BoxShadow>? _boxShadow() {
    if (!widget.showGlow) return null;

    return [
      BoxShadow(
        color: AppColors.shadow(_pressed ? 0.10 : 0.06),
        blurRadius: _pressed ? 12 : 14,
        offset: Offset(0, _pressed ? 4 : 7),
      ),
      if (widget.tone == InMomentSurfaceTone.accent ||
          widget.tone == InMomentSurfaceTone.elevated)
        BoxShadow(
          color: AppColors.accentSecondary.withValues(
            alpha: _pressed ? 0.035 : 0.025,
          ),
          blurRadius: _pressed ? 16 : 20,
        ),
    ];
  }

  Color _pressedOverlayColor() {
    if (!_pressed || widget.onTap == null) {
      return Colors.transparent;
    }

    if (widget.tone == InMomentSurfaceTone.danger) {
      return AppColors.error.withValues(alpha: 0.045);
    }

    return AppColors.black.withValues(alpha: 0.055);
  }

  void _setPressed(bool value) {
    if (_pressed == value || !mounted) return;

    setState(() {
      _pressed = value;
    });
  }

  @override
  Widget build(BuildContext context) {
    final radius = widget.borderRadius ?? BorderRadius.circular(AppTheme.radiusMd);

    final decorated = AnimatedContainer(
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOutCubic,
      width: widget.width,
      height: widget.height,
      padding: widget.padding,
      decoration: BoxDecoration(
        color: _backgroundColor(),
        gradient: _gradient(),
        borderRadius: radius,
        border: Border.all(
          color: _borderColor(),
        ),
        boxShadow: _boxShadow(),
      ),
      foregroundDecoration: BoxDecoration(
        color: _pressedOverlayColor(),
        borderRadius: radius,
      ),
      child: widget.child,
    );

    final clipped = ClipRRect(
      borderRadius: radius,
      child: widget.blur
          ? BackdropFilter(
              filter: ImageFilter.blur(sigmaX: 20, sigmaY: 20),
              child: decorated,
            )
          : decorated,
    );

    final scaled = AnimatedScale(
      scale: _pressed && widget.onTap != null ? 0.988 : 1.0,
      duration: const Duration(milliseconds: 140),
      curve: Curves.easeOutCubic,
      child: clipped,
    );

    if (widget.onTap == null) {
      return scaled;
    }

    return Material(
      color: Colors.transparent,
      borderRadius: radius,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: widget.onTap,
        onHighlightChanged: _setPressed,
        borderRadius: radius,
        splashColor: AppColors.white.withValues(alpha: 0.055),
        highlightColor: Colors.transparent,
        child: scaled,
      ),
    );
  }
}