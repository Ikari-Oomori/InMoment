import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../models/reaction_catalog.dart';

Future<ReactionCatalogItem?> showReactionPopupMenu(
  BuildContext context, {
  required Offset position,
  required int selectedType,
}) async {
  return showDialog<ReactionCatalogItem>(
    context: context,
    barrierColor: Colors.transparent,
    builder: (_) {
      return _ReactionPopupMenu(
        position: position,
        selectedType: selectedType,
      );
    },
  );
}

class _ReactionPopupMenu extends StatelessWidget {
  final Offset position;
  final int selectedType;

  const _ReactionPopupMenu({
    required this.position,
    required this.selectedType,
  });

  @override
  Widget build(BuildContext context) {
    const spacing = 6.0;
    const horizontalPadding = 10.0;

    final media = MediaQuery.of(context);
    final screen = media.size;
    final keyboardInset = media.viewInsets.bottom;
    final popupWidth = (screen.width - 24).clamp(300.0, 620.0).toDouble();
    final left = ((screen.width - popupWidth) / 2).clamp(12.0, screen.width);
    final availableHeight = screen.height - keyboardInset;
    const itemSize = 40.0;
    final popupHeight = itemSize + 18;
    final maxTop = (availableHeight - popupHeight - 18.0).clamp(54.0, screen.height);
    final top = (position.dy - popupHeight - 16).clamp(54.0, maxTop);

    return Stack(
      children: [
        Positioned.fill(
          child: GestureDetector(
            behavior: HitTestBehavior.translucent,
            onTap: () => Navigator.of(context).pop(),
          ),
        ),
        Positioned(
          left: left,
          top: top,
          width: popupWidth,
          child: Material(
            color: Colors.transparent,
            child: Container(
              height: popupHeight,
              padding: const EdgeInsets.symmetric(
                horizontal: horizontalPadding,
                vertical: 9,
              ),
              decoration: BoxDecoration(
                color: AppColors.card.withValues(alpha: 0.98),
                borderRadius: BorderRadius.circular(999),
                border: Border.all(color: AppColors.border),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.28),
                    blurRadius: 18,
                    offset: const Offset(0, 8),
                  ),
                ],
              ),
              child: SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                physics: const BouncingScrollPhysics(),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    for (var index = 0; index < ReactionCatalog.all.length; index++) ...[
                      Builder(
                        builder: (context) {
                          final item = ReactionCatalog.all[index];
                          return InkWell(
                            onTap: () => Navigator.of(context).pop(item),
                            borderRadius: BorderRadius.circular(999),
                            child: AnimatedScale(
                              scale: item.type == selectedType ? 1.10 : 1,
                              duration: const Duration(milliseconds: 120),
                              child: Container(
                                width: itemSize,
                                height: itemSize,
                                alignment: Alignment.center,
                                decoration: BoxDecoration(
                                  color: item.type == selectedType
                                      ? AppColors.accent.withValues(alpha: 0.28)
                                      : AppColors.surface.withValues(alpha: 0.72),
                                  shape: BoxShape.circle,
                                  border: Border.all(
                                    color: item.type == selectedType
                                        ? AppColors.accentSecondary
                                        : AppColors.border,
                                  ),
                                ),
                                child: FittedBox(
                                  fit: BoxFit.scaleDown,
                                  child: Text(
                                    item.emoji,
                                    style: TextStyle(fontSize: itemSize * 0.56),
                                  ),
                                ),
                              ),
                            ),
                          );
                        },
                      ),
                      if (index != ReactionCatalog.all.length - 1)
                        const SizedBox(width: spacing),
                    ],
                  ],
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}