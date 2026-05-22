import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../models/reaction_catalog.dart';

class ReactionCounterPill extends StatelessWidget {
  final int? topReactionType;
  final List<int> reactionTypes;
  final String value;
  final bool selected;
  final bool loading;
  final bool heartOnly;
  final VoidCallback onTap;
  final ValueChanged<Offset>? onOpenPicker;

  const ReactionCounterPill({
    super.key,
    required this.value,
    required this.onTap,
    this.onOpenPicker,
    this.topReactionType,
    this.reactionTypes = const [],
    this.selected = false,
    this.loading = false,
    this.heartOnly = false,
  });

  List<int> get _visibleTypes {
    if (heartOnly) return const [1];

    final result = <int>[];

    for (final type in reactionTypes) {
      if (type == ReactionCatalog.none) continue;
      if (!ReactionCatalog.isSupported(type)) continue;
      if (result.contains(type)) continue;
      result.add(type);
      if (result.length == 3) break;
    }

    final top = topReactionType;
    if (result.isEmpty && top != null && ReactionCatalog.isSupported(top)) {
      result.add(top);
    }

    if (result.isEmpty) result.add(ReactionCatalog.primary.type);
    return result;
  }

  void _openFromContext(BuildContext context) {
    if (loading || onOpenPicker == null) return;

    final box = context.findRenderObject() as RenderBox?;
    if (box == null || !box.hasSize) return;

    final center = box.localToGlobal(
      Offset(box.size.width / 2, box.size.height / 2),
    );

    onOpenPicker!(center);
  }

  @override
  Widget build(BuildContext context) {
    final visibleTypes = _visibleTypes;

    return Builder(
      builder: (pillContext) {
        return Material(
          color: selected
              ? AppColors.accent.withValues(alpha: 0.22)
              : AppColors.surface.withValues(alpha: 0.94),
          borderRadius: BorderRadius.circular(999),
          child: InkWell(
            onTap: loading ? null : onTap,
            onLongPress: loading ? null : () => _openFromContext(pillContext),
            onSecondaryTapDown: loading || onOpenPicker == null
                ? null
                : (details) => onOpenPicker!(details.globalPosition),
            borderRadius: BorderRadius.circular(999),
            child: AnimatedContainer(
              duration: const Duration(milliseconds: 160),
              curve: Curves.easeOut,
              padding: EdgeInsets.symmetric(
                horizontal: heartOnly ? 9 : 10,
                vertical: heartOnly ? 7 : 8,
              ),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(999),
                border: Border.all(
                  color: selected ? AppColors.accentSecondary : AppColors.border,
                ),
              ),
              child: loading
                  ? SizedBox(
                      width: heartOnly ? 14 : 16,
                      height: heartOnly ? 14 : 16,
                      child: const CircularProgressIndicator(strokeWidth: 2),
                    )
                  : Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        _ReactionStack(
                          reactionTypes: visibleTypes,
                          selected: selected,
                          heartOnly: heartOnly,
                        ),
                        const SizedBox(width: 5),
                        Text(
                          value,
                          style: TextStyle(
                            color: selected
                                ? AppColors.textPrimary
                                : AppColors.textSecondary,
                            fontSize: heartOnly ? 11 : 12,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ],
                    ),
            ),
          ),
        );
      },
    );
  }
}

class _ReactionStack extends StatelessWidget {
  final List<int> reactionTypes;
  final bool selected;
  final bool heartOnly;

  const _ReactionStack({
    required this.reactionTypes,
    required this.selected,
    required this.heartOnly,
  });

  @override
  Widget build(BuildContext context) {
    if (heartOnly || reactionTypes.length == 1) {
      return AnimatedScale(
        scale: selected ? 1.08 : 1,
        duration: const Duration(milliseconds: 140),
        curve: Curves.easeOut,
        child: Text(
          ReactionCatalog.emojiOf(reactionTypes.first),
          style: TextStyle(fontSize: heartOnly ? 13 : 15),
        ),
      );
    }

    return SizedBox(
      width: 14.0 + (reactionTypes.length - 1) * 12.0,
      height: 18,
      child: Stack(
        clipBehavior: Clip.none,
        children: [
          for (var i = 0; i < reactionTypes.length; i++)
            Positioned(
              left: i * 11.5,
              top: 0,
              child: Container(
                width: 18,
                height: 18,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: AppColors.card,
                  shape: BoxShape.circle,
                  border: Border.all(
                    color: AppColors.background.withValues(alpha: 0.65),
                    width: 1,
                  ),
                ),
                child: Text(
                  ReactionCatalog.emojiOf(reactionTypes[i]),
                  style: const TextStyle(fontSize: 12),
                ),
              ),
            ),
        ],
      ),
    );
  }
}