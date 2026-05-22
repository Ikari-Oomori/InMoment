import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../theme/app_theme.dart';
import 'inmoment_surface.dart';

class InMomentActionTile extends StatelessWidget {
  final IconData? icon;
  final String title;
  final String? subtitle;
  final VoidCallback? onTap;
  final Widget? trailing;
  final bool danger;
  final bool compact;

  const InMomentActionTile({
    super.key,
    this.icon,
    required this.title,
    this.subtitle,
    this.onTap,
    this.trailing,
    this.danger = false,
    this.compact = false,
  });

  @override
  Widget build(BuildContext context) {
    final titleColor = danger ? AppColors.error : AppColors.textPrimary;
    final iconColor = danger ? AppColors.error : AppColors.textSecondary;

    return InMomentSurface(
      tone: danger ? InMomentSurfaceTone.danger : InMomentSurfaceTone.base,
      blur: true,
      borderRadius: BorderRadius.circular(compact ? 18 : AppTheme.radiusSm),
      onTap: onTap,
      padding: EdgeInsets.symmetric(
        horizontal: compact ? 12 : 14,
        vertical: compact ? 10 : 12,
      ),
      child: Row(
        children: [
          if (icon != null) ...[
            Container(
              width: compact ? 32 : 36,
              height: compact ? 32 : 36,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: danger
                    ? AppColors.error.withValues(alpha: 0.11)
                    : AppColors.white.withValues(alpha: 0.055),
                border: Border.all(
                  color: danger
                      ? AppColors.error.withValues(alpha: 0.14)
                      : AppColors.softStroke(0.08),
                ),
              ),
              child: Icon(
                icon,
                size: compact ? 16 : 18,
                color: iconColor,
              ),
            ),
            const SizedBox(width: 12),
          ],
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: TextStyle(
                    color: titleColor,
                    fontSize: compact ? 13.0 : 13.8,
                    fontWeight: FontWeight.w600,
                    height: 1.18,
                    letterSpacing: -0.05,
                  ),
                ),
                if (subtitle != null && subtitle!.trim().isNotEmpty) ...[
                  const SizedBox(height: 4),
                  Text(
                    subtitle!,
                    style: const TextStyle(
                      color: AppColors.textSecondary,
                      fontSize: 12,
                      height: 1.30,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: 10),
          trailing ??
              Icon(
                Icons.chevron_right_rounded,
                color: danger ? AppColors.error : AppColors.textSecondary,
                size: 22,
              ),
        ],
      ),
    );
  }
}