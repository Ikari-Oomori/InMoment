import 'package:flutter/material.dart';

import '../layout/inmoment_media_frame.dart';
import '../theme/app_colors.dart';
import '../theme/app_theme.dart';
import 'inmoment_surface.dart';

class InMomentPageShell extends StatelessWidget {
  final String title;
  final Widget child;
  final List<Widget>? actions;
  final bool scrollable;
  final EdgeInsetsGeometry contentPadding;
  final bool showSurface;
  final bool canPop;
  final Widget? bottom;
  final Widget? leading;
  final double titleFontSize;

  const InMomentPageShell({
    super.key,
    required this.title,
    required this.child,
    this.actions,
    this.scrollable = true,
    this.contentPadding = const EdgeInsets.fromLTRB(0, 16, 0, 120),
    this.showSurface = true,
    this.canPop = true,
    this.bottom,
    this.leading,
    this.titleFontSize = 17.5,
  });

  @override
  Widget build(BuildContext context) {
    final viewportWidth = MediaQuery.sizeOf(context).width;
    final compactPhone = InMomentMediaFrame.isCompactPhoneWidth(viewportWidth);
    final shellWidth = InMomentMediaFrame.resolveTabletContentWidth(viewportWidth);

    final content = showSurface
        ? InMomentSurface(
            tone: InMomentSurfaceTone.elevated,
            borderRadius: BorderRadius.circular(AppTheme.radiusLg),
            padding: EdgeInsets.fromLTRB(
              compactPhone ? 10 : 15,
              compactPhone ? 12 : 15,
              compactPhone ? 10 : 15,
              compactPhone ? 12 : 15,
            ),
            child: child,
          )
        : child;

    final bodyContent = scrollable
        ? SingleChildScrollView(
            padding: contentPadding,
            physics: const BouncingScrollPhysics(),
            child: content,
          )
        : Padding(
            padding: contentPadding,
            child: content,
          );

    return Scaffold(
      backgroundColor: AppColors.background,
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: AppColors.pageBackgroundGradient,
        ),
        child: SafeArea(
          child: Column(
            children: [
              Center(
                child: SizedBox(
                  width: shellWidth,
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(0, 8, 0, 10),
                    child: Row(
                      children: [
                        SizedBox(
                          width: 48,
                          child: Align(
                            alignment: Alignment.centerLeft,
                            child: leading ??
                                (canPop
                                    ? _ShellIconButton(
                                        icon: Icons.arrow_back_ios_new_rounded,
                                        onTap: () =>
                                            Navigator.of(context).maybePop(),
                                      )
                                    : const SizedBox.shrink()),
                          ),
                        ),
                        Expanded(
                          child: Text(
                            title,
                            textAlign: TextAlign.center,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              color: AppColors.textPrimary,
                              fontSize: titleFontSize,
                              fontWeight: FontWeight.w800,
                              letterSpacing: -0.25,
                              height: 1.08,
                            ),
                          ),
                        ),
                        SizedBox(
                          width: 48,
                          child: Row(
                            mainAxisAlignment: MainAxisAlignment.end,
                            mainAxisSize: MainAxisSize.min,
                            children: actions ?? const [],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              Expanded(
                child: Center(
                  child: SizedBox(
                    width: shellWidth,
                    child: bodyContent,
                  ),
                ),
              ),
              if (bottom != null)
                SafeArea(
                  top: false,
                  child: Center(
                    child: SizedBox(
                      width: shellWidth,
                      child: Padding(
                        padding: EdgeInsets.fromLTRB(
                          compactPhone ? 8 : 16,
                          0,
                          compactPhone ? 8 : 16,
                          12,
                        ),
                        child: bottom!,
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ShellIconButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onTap;

  const _ShellIconButton({
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: onTap,
      child: Container(
        width: 36,
        height: 36,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: Colors.white.withValues(alpha: 0.04),
          borderRadius: BorderRadius.circular(18),
        ),
        child: Icon(
          icon,
          size: 18,
          color: Colors.white.withValues(alpha: 0.85),
        ),
      ),
    );
  }
}