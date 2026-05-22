import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';

class OnboardingStepScaffold extends StatelessWidget {
  final int step;
  final int total;
  final Widget body;
  final Widget bottomAction;
  final Widget? topVisual;
  final String? title;
  final String? subtitle;

  const OnboardingStepScaffold({
    super.key,
    required this.step,
    required this.total,
    required this.body,
    required this.bottomAction,
    this.topVisual,
    this.title,
    this.subtitle,
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) {
            final bottomInset = MediaQuery.of(context).viewInsets.bottom;

            return AnimatedPadding(
              duration: const Duration(milliseconds: 180),
              curve: Curves.easeOut,
              padding: EdgeInsets.only(bottom: bottomInset),
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(20, 10, 20, 20),
                child: Center(
                  child: ConstrainedBox(
                    constraints: BoxConstraints(
                      maxWidth: 460,
                      minHeight: constraints.maxHeight - 30,
                    ),
                    child: IntrinsicHeight(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          OnboardingStepHeader(
                            step: step,
                            total: total,
                            showBackButton: step > 1 && Navigator.of(context).canPop(),
                          ),
                          const SizedBox(height: 24),
                          if (topVisual != null) ...[
                            topVisual!,
                            const SizedBox(height: 20),
                          ],
                          if (title != null) ...[
                            Text(
                              title!,
                              style: const TextStyle(
                                color: AppColors.textPrimary,
                                fontSize: 28,
                                fontWeight: FontWeight.w800,
                              ),
                            ),
                            const SizedBox(height: 10),
                          ],
                          if (subtitle != null) ...[
                            Text(
                              subtitle!,
                              style: const TextStyle(
                                color: AppColors.textSecondary,
                                fontSize: 14,
                                height: 1.45,
                              ),
                            ),
                            const SizedBox(height: 22),
                          ],
                          body,
                          const Spacer(),
                          const SizedBox(height: 16),
                          bottomAction,
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            );
          },
        ),
      ),
    );
  }
}

class OnboardingStepHeader extends StatelessWidget {
  final int step;
  final int total;
  final bool showBackButton;

  const OnboardingStepHeader({
    super.key,
    required this.step,
    required this.total,
    this.showBackButton = true,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SizedBox(
          height: 42,
          child: Align(
            alignment: Alignment.centerLeft,
            child: showBackButton
                ? OnboardingBackButton(
                    onPressed: () => Navigator.of(context).maybePop(),
                  )
                : const SizedBox.shrink(),
          ),
        ),
        const SizedBox(height: 10),
        OnboardingStepProgress(
          step: step,
          total: total,
        ),
      ],
    );
  }
}

class OnboardingBackButton extends StatelessWidget {
  final VoidCallback onPressed;

  const OnboardingBackButton({
    super.key,
    required this.onPressed,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        borderRadius: BorderRadius.circular(999),
        onTap: onPressed,
        child: Container(
          width: 40,
          height: 40,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: AppColors.surfaceGlass(0.30),
            borderRadius: BorderRadius.circular(999),
            border: Border.all(
              color: AppColors.border.withValues(alpha: 0.75),
            ),
          ),
          child: const Icon(
            Icons.arrow_back_ios_new_rounded,
            size: 18,
            color: AppColors.textPrimary,
          ),
        ),
      ),
    );
  }
}

class OnboardingStepProgress extends StatelessWidget {
  final int step;
  final int total;

  const OnboardingStepProgress({
    super.key,
    required this.step,
    required this.total,
  });

  @override
  Widget build(BuildContext context) {
    final progress = (step / total).clamp(0.0, 1.0).toDouble();

    return ClipRRect(
      borderRadius: BorderRadius.circular(999),
      child: TweenAnimationBuilder<double>(
        tween: Tween<double>(end: progress),
        duration: const Duration(milliseconds: 320),
        curve: Curves.easeOutCubic,
        builder: (context, value, _) {
          return LinearProgressIndicator(
            value: value,
            minHeight: 8,
            backgroundColor: AppColors.surface,
            valueColor: const AlwaysStoppedAnimation<Color>(
              AppColors.accentLight,
            ),
          );
        },
      ),
    );
  }
}