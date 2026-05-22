import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../layout/inmoment_media_frame.dart';

final class InMomentFeedback {
  const InMomentFeedback._();

  static void showSuccess(BuildContext context, String text) {
    _show(
      context,
      text,
      icon: Icons.check_circle_outline_rounded,
    );
  }

  static void showError(BuildContext context, String text) {
    _show(
      context,
      text,
      icon: Icons.error_outline_rounded,
    );
  }

  static void showInfo(BuildContext context, String text) {
    _show(
      context,
      text,
      icon: Icons.info_outline_rounded,
    );
  }

  static void _show(
    BuildContext context,
    String text, {
    required IconData icon,
  }) {
    if (!context.mounted) return;

    final messenger = ScaffoldMessenger.maybeOf(context);
    if (messenger == null) return;

    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          behavior: SnackBarBehavior.floating,
          width: InMomentMediaFrame.resolveSnackBarWidth(
            MediaQuery.sizeOf(context).width,
          ),
          content: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Padding(
                padding: const EdgeInsets.only(top: 1),
                child: Icon(
                  icon,
                  size: 18,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  text,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w600,
                    height: 1.35,
                  ),
                ),
              ),
            ],
          ),
        ),
      );
  }
}