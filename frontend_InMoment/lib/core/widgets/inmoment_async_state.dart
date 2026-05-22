import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import 'inmoment_surface.dart';

class InMomentAsyncState extends StatelessWidget {
  final bool isLoading;
  final String? error;
  final VoidCallback? onRetry;
  final Widget child;
  final String retryLabel;
  final EdgeInsetsGeometry padding;
  final String? loadingLabel;

  const InMomentAsyncState({
    super.key,
    required this.isLoading,
    required this.error,
    required this.child,
    this.onRetry,
    this.retryLabel = 'Повторить',
    this.padding = const EdgeInsets.symmetric(vertical: 24),
    this.loadingLabel,
  });

  @override
  Widget build(BuildContext context) {
    if (isLoading) {
      final label = loadingLabel?.trim();

      return Padding(
        padding: padding,
        child: Center(
          child: InMomentSurface(
            tone: InMomentSurfaceTone.overlay,
            showGlow: false,
            padding: const EdgeInsets.symmetric(
              horizontal: 16,
              vertical: 13,
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                const SizedBox(
                  width: 22,
                  height: 22,
                  child: CircularProgressIndicator(strokeWidth: 2.3),
                ),
                if (label != null && label.isNotEmpty) ...[
                  const SizedBox(width: 12),
                  Flexible(
                    child: Text(
                      label,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        fontWeight: FontWeight.w800,
                        fontSize: 13,
                        height: 1.1,
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ),
      );
    }

    final trimmedError = error?.trim();

    if (trimmedError != null && trimmedError.isNotEmpty) {
      return Padding(
        padding: padding,
        child: InMomentSurface(
          tone: InMomentSurfaceTone.elevated,
          padding: const EdgeInsets.fromLTRB(18, 20, 18, 18),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 42,
                height: 42,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: AppColors.error.withValues(alpha: 0.11),
                  border: Border.all(
                    color: AppColors.error.withValues(alpha: 0.18),
                  ),
                ),
                child: const Icon(
                  Icons.error_outline_rounded,
                  color: AppColors.error,
                  size: 23,
                ),
              ),
              const SizedBox(height: 12),
              Text(
                trimmedError,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: AppColors.textPrimary,
                  height: 1.42,
                  fontWeight: FontWeight.w600,
                ),
              ),
              if (onRetry != null) ...[
                const SizedBox(height: 16),
                FilledButton.icon(
                  onPressed: onRetry,
                  icon: const Icon(
                    Icons.refresh_rounded,
                    size: 18,
                  ),
                  label: Text(retryLabel),
                ),
              ],
            ],
          ),
        ),
      );
    }

    return child;
  }
}