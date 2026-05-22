import 'dart:ui';

import 'package:flutter/material.dart';

import '../../features/shell/models/app_shell_tab.dart';
import '../theme/app_colors.dart';

class AppBottomNavBar extends StatelessWidget {
  final AppShellTab selectedTab;
  final ValueChanged<AppShellTab> onTabSelected;

  const AppBottomNavBar({
    super.key,
    required this.selectedTab,
    required this.onTabSelected,
  });

  @override
  Widget build(BuildContext context) {
    final width = MediaQuery.of(context).size.width;
    final barWidth = (width * 0.54).clamp(214.0, 318.0);

    return SafeArea(
      top: false,
      minimum: const EdgeInsets.only(bottom: 10),
      child: Center(
        child: ClipRRect(
          borderRadius: BorderRadius.circular(999),
          child: BackdropFilter(
            filter: ImageFilter.blur(sigmaX: 22, sigmaY: 22),
            child: Container(
              width: barWidth,
              height: 62,
              padding: const EdgeInsets.symmetric(horizontal: 7),
              decoration: BoxDecoration(
                color: AppColors.surfaceGlassStrong(0.82),
                borderRadius: BorderRadius.circular(999),
                border: Border.all(
                  color: AppColors.softStroke(0.15),
                ),
                boxShadow: [
                  BoxShadow(
                    color: AppColors.black.withValues(alpha: 0.30),
                    blurRadius: 34,
                    offset: const Offset(0, 16),
                  ),
                  BoxShadow(
                    color: AppColors.accentSecondary.withValues(alpha: 0.10),
                    blurRadius: 34,
                  ),
                ],
              ),
              child: Row(
                children: [
                  _NavItem(
                    tooltip: 'Воспоминания',
                    icon: Icons.calendar_month_outlined,
                    activeIcon: Icons.calendar_month_rounded,
                    selected: selectedTab == AppShellTab.memories,
                    onTap: () => onTabSelected(AppShellTab.memories),
                  ),
                  _NavItem(
                    tooltip: 'Главная',
                    icon: Icons.home_rounded,
                    activeIcon: Icons.home_rounded,
                    selected: selectedTab == AppShellTab.camera,
                    onTap: () => onTabSelected(AppShellTab.camera),
                    isCenter: true,
                  ),
                  _NavItem(
                    tooltip: 'Профиль',
                    icon: Icons.person_outline_rounded,
                    activeIcon: Icons.person_rounded,
                    selected: selectedTab == AppShellTab.profile,
                    onTap: () => onTabSelected(AppShellTab.profile),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  final String tooltip;
  final IconData icon;
  final IconData activeIcon;
  final bool selected;
  final VoidCallback onTap;
  final bool isCenter;

  const _NavItem({
    required this.tooltip,
    required this.icon,
    required this.activeIcon,
    required this.selected,
    required this.onTap,
    this.isCenter = false,
  });

  @override
  Widget build(BuildContext context) {
    final iconColor = selected
        ? AppColors.textPrimary
        : AppColors.textSecondary.withValues(alpha: 0.90);

    return Expanded(
      child: Tooltip(
        message: tooltip,
        child: Material(
          color: Colors.transparent,
          child: InkWell(
            onTap: onTap,
            customBorder: const StadiumBorder(),
            splashColor: AppColors.white.withValues(alpha: 0.055),
            highlightColor: AppColors.white.withValues(alpha: 0.035),
            child: Center(
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 190),
                curve: Curves.easeOutCubic,
                width: isCenter ? 58 : 44,
                height: isCenter ? 43 : 39,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(999),
                  gradient: selected
                      ? LinearGradient(
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                          colors: [
                            AppColors.accentStrong.withValues(
                              alpha: isCenter ? 0.34 : 0.24,
                            ),
                            AppColors.accentSecondary.withValues(
                              alpha: isCenter ? 0.25 : 0.17,
                            ),
                          ],
                        )
                      : null,
                  border: selected
                      ? Border.all(
                          color: AppColors.white.withValues(alpha: 0.11),
                        )
                      : null,
                ),
                child: AnimatedScale(
                  scale: selected ? 1.07 : 1.0,
                  duration: const Duration(milliseconds: 190),
                  curve: Curves.easeOutCubic,
                  child: Icon(
                    selected ? activeIcon : icon,
                    size: isCenter ? 23 : 21,
                    color: iconColor,
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