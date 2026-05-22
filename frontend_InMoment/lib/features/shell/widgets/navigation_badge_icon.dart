import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';

class NavigationBadgeIcon extends StatelessWidget {
  final IconData icon;
  final int count;
  final bool selected;

  const NavigationBadgeIcon({
    super.key,
    required this.icon,
    required this.count,
    required this.selected,
  });

  @override
  Widget build(BuildContext context) {
    return Stack(
      clipBehavior: Clip.none,
      children: [
        Icon(icon),
        if (count > 0)
          Positioned(
            right: -8,
            top: -6,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 2),
              constraints: const BoxConstraints(minWidth: 16),
              decoration: BoxDecoration(
                color: selected
                    ? AppColors.accentSecondary
                    : AppColors.accentSecondary,
                borderRadius: BorderRadius.circular(999),
              ),
              child: Text(
                count > 99 ? '99+' : '$count',
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: 10,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ),
      ],
    );
  }
}