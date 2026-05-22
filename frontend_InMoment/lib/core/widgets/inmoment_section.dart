import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../theme/app_theme.dart';
import 'inmoment_surface.dart';

class InMomentSection extends StatelessWidget {
  final String? title;
  final String? subtitle;
  final Widget child;
  final EdgeInsetsGeometry? contentPadding;
  final InMomentSurfaceTone tone;
  final Widget? trailing;

  const InMomentSection({
    super.key,
    this.title,
    this.subtitle,
    required this.child,
    this.contentPadding,
    this.tone = InMomentSurfaceTone.base,
    this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    final hasTitle = title != null && title!.trim().isNotEmpty;
    final hasSubtitle = subtitle != null && subtitle!.trim().isNotEmpty;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (hasTitle || hasSubtitle || trailing != null) ...[
          Padding(
            padding: const EdgeInsets.fromLTRB(4, 0, 4, 9),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                if (hasTitle || hasSubtitle)
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        if (hasTitle)
                          Text(
                            title!.trim(),
                            style: const TextStyle(
                              color: AppColors.textPrimary,
                              fontSize: 15.0,
                              fontWeight: FontWeight.w700,
                              height: 1.12,
                              letterSpacing: -0.18,
                            ),
                          ),
                        if (hasSubtitle) ...[
                          const SizedBox(height: 4),
                          Text(
                            subtitle!.trim(),
                            style: const TextStyle(
                              color: AppColors.textSecondary,
                              fontSize: 12.2,
                              height: 1.32,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ],
                      ],
                    ),
                  )
                else
                  const Spacer(),
                if (trailing != null) ...[
                  const SizedBox(width: 12),
                  trailing!,
                ],
              ],
            ),
          ),
        ],
        InMomentSurface(
          tone: tone,
          borderRadius: BorderRadius.circular(AppTheme.radiusMd),
          padding: contentPadding ?? const EdgeInsets.fromLTRB(14, 13, 14, 14),
          child: child,
        ),
      ],
    );
  }
}