import 'dart:ui';

import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../theme/app_theme.dart';
import '../layout/inmoment_media_frame.dart';

Future<bool?> showInMomentConfirmDialog({
  required BuildContext context,
  required String title,
  required String message,
  String cancelText = 'Отмена',
  String confirmText = 'Подтвердить',
  bool danger = false,
}) {
  return showDialog<bool>(
    context: context,
    barrierColor: Colors.black.withValues(alpha: 0.46),
    builder: (context) => InMomentGlassDialog(
      title: title,
      message: message,
      cancelText: cancelText,
      confirmText: confirmText,
      danger: danger,
    ),
  );
}

Future<T?> showInMomentGlassBottomSheet<T>({
  required BuildContext context,
  required Widget child,
  bool isScrollControlled = true,
  bool useSafeArea = true,
}) {
  return showModalBottomSheet<T>(
    context: context,
    isScrollControlled: isScrollControlled,
    isDismissible: true,
    enableDrag: true,
    useSafeArea: useSafeArea,
    backgroundColor: Colors.transparent,
    barrierColor: AppColors.black.withValues(alpha: 0.46),
    builder: (_) => InMomentGlassBottomSheet(child: child),
  );
}

class InMomentGlassBottomSheet extends StatelessWidget {
  final Widget child;
  final double maxHeightFactor;
  final EdgeInsetsGeometry padding;

  const InMomentGlassBottomSheet({
    super.key,
    required this.child,
    this.maxHeightFactor = 0.90,
    this.padding = const EdgeInsets.fromLTRB(0, 8, 0, 0),
  });

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    final bottomInset = media.viewInsets.bottom;

    return SafeArea(
      top: false,
      child: Padding(
        padding: EdgeInsets.fromLTRB(12, 0, 12, 12 + bottomInset),
        child: Center(
          child: SizedBox(
            width: InMomentMediaFrame.resolveBottomSheetWidth(
              media.size.width,
            ),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(30),
              child: BackdropFilter(
                filter: ImageFilter.blur(sigmaX: 22, sigmaY: 22),
                child: Container(
                  padding: padding,
                  constraints: BoxConstraints(
                    maxHeight: media.size.height * maxHeightFactor,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.surfaceGlassStrong(0.54),
                    borderRadius: BorderRadius.circular(30),
                    border: Border.all(color: AppColors.softStroke(0.13)),
                    boxShadow: [
                      BoxShadow(
                        color: AppColors.black.withValues(alpha: 0.24),
                        blurRadius: 30,
                        offset: const Offset(0, 18),
                      ),
                    ],
                  ),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Container(
                        width: 42,
                        height: 4,
                        decoration: BoxDecoration(
                          color: AppColors.textSecondary.withValues(alpha: 0.32),
                          borderRadius: BorderRadius.circular(999),
                        ),
                      ),
                      const SizedBox(height: 8),
                      Flexible(child: child),
                    ],
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

class InMomentGlassDialog extends StatelessWidget {
  final String title;
  final String? message;
  final Widget? content;
  final String cancelText;
  final String confirmText;
  final bool danger;
  final VoidCallback? onCancel;
  final VoidCallback? onConfirm;

  const InMomentGlassDialog({
    super.key,
    required this.title,
    this.message,
    this.content,
    this.cancelText = 'Отмена',
    this.confirmText = 'Подтвердить',
    this.danger = false,
    this.onCancel,
    this.onConfirm,
  });

  @override
  Widget build(BuildContext context) {
    final confirmColor = danger ? AppColors.error : AppColors.accentSecondary;

    final dialogWidth = InMomentMediaFrame.resolveDialogWidth(
      MediaQuery.sizeOf(context).width,
    );

    return Dialog(
      insetPadding: const EdgeInsets.symmetric(horizontal: 22, vertical: 24),
        backgroundColor: Colors.transparent,
        elevation: 0,
        child: SizedBox(
          width: dialogWidth,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(28),
            child: BackdropFilter(
              filter: ImageFilter.blur(sigmaX: 22, sigmaY: 22),
              child: Container(
                padding: const EdgeInsets.fromLTRB(20, 20, 20, 18),
                decoration: BoxDecoration(
                  color: AppColors.surfaceGlassStrong(0.46),
                  borderRadius: BorderRadius.circular(28),
                  border: Border.all(color: AppColors.softStroke(0.12)),
                  boxShadow: [
                    BoxShadow(
                      color: AppColors.black.withValues(alpha: 0.22),
                      blurRadius: 24,
                      offset: const Offset(0, 14),
                    ),
                  ],
                ),
                child: SingleChildScrollView(
                  keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        title,
                        style: const TextStyle(
                          color: AppColors.textPrimary,
                          fontSize: 20,
                          fontWeight: FontWeight.w800,
                          height: 1.08,
                          letterSpacing: -0.35,
                        ),
                      ),
                      if (message != null && message!.trim().isNotEmpty) ...[
                        const SizedBox(height: 14),
                        Text(
                          message!.trim(),
                          style: const TextStyle(
                            color: AppColors.textSecondary,
                            fontSize: 13.5,
                            fontWeight: FontWeight.w500,
                            height: 1.38,
                          ),
                        ),
                      ],
                      if (content != null) ...[
                        const SizedBox(height: 16),
                        content!,
                      ],
                      const SizedBox(height: 20),
                      Row(
                        children: [
                          Expanded(
                            child: _DialogButton(
                              text: cancelText,
                              outlined: true,
                              onTap: onCancel ?? () => Navigator.of(context).pop(false),
                            ),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: _DialogButton(
                              text: confirmText,
                              color: confirmColor,
                              onTap: onConfirm ?? () => Navigator.of(context).pop(true),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
            ),
          ),
        ),
      ),
    );
  }
}

class _DialogButton extends StatelessWidget {
  final String text;
  final bool outlined;
  final Color? color;
  final VoidCallback? onTap;

  const _DialogButton({
    required this.text,
    this.outlined = false,
    this.color,
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppTheme.radiusSm),
        child: Ink(
          height: 48,
          decoration: BoxDecoration(
            color: outlined
                ? AppColors.white.withValues(alpha: 0.035)
                : (color ?? AppColors.accentSecondary).withValues(alpha: 0.72),
            borderRadius: BorderRadius.circular(AppTheme.radiusSm),
            border: Border.all(
              color: outlined
                  ? AppColors.softStroke(0.16)
                  : AppColors.white.withValues(alpha: 0.08),
            ),
          ),
          child: Center(
            child: Text(
              text,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontSize: 14.5,
                fontWeight: FontWeight.w700,
                height: 1.1,
              ),
            ),
          ),
        ),
      ),
    );
  }
}