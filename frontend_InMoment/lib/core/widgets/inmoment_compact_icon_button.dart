import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

class InMomentCompactIconButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onTap;
  final bool selected;
  final bool translucent;
  final double size;
  final double iconSize;

  const InMomentCompactIconButton({
    super.key,
    required this.icon,
    this.onTap,
    this.selected = false,
    this.translucent = true,
    this.size = 42,
    this.iconSize = 22,
  });

  @override
  Widget build(BuildContext context) {
    final bg = translucent
        ? AppColors.surfaceGlassStrong(selected ? 0.58 : 0.42)
        : Colors.transparent;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        splashColor: AppColors.white.withValues(alpha: 0.06),
        highlightColor: AppColors.white.withValues(alpha: 0.035),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 170),
          curve: Curves.easeOutCubic,
          width: size,
          height: size,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            color: bg,
            gradient: selected
                ? LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [
                      AppColors.accentStrong.withValues(alpha: 0.30),
                      AppColors.accentSecondary.withValues(alpha: 0.18),
                    ],
                  )
                : null,
            border: Border.all(
              color: selected
                  ? AppColors.accentSoft.withValues(alpha: 0.24)
                  : AppColors.softStroke(translucent ? 0.12 : 0.0),
            ),
            boxShadow: [
              BoxShadow(
                color: AppColors.shadow(selected ? 0.18 : 0.10),
                blurRadius: selected ? 18 : 12,
                offset: const Offset(0, 8),
              ),
            ],
          ),
          child: Icon(
            icon,
            size: iconSize,
            color: selected
                ? AppColors.textPrimary
                : AppColors.textPrimary.withValues(alpha: 0.92),
          ),
        ),
      ),
    );
  }
}