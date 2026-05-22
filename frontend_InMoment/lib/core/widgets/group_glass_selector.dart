import 'package:flutter/material.dart';

import '../../features/groups/models/group.dart';
import '../theme/app_colors.dart';
import 'inmoment_glass_dialog.dart';
import 'inmoment_surface.dart';

class GroupGlassSelector extends StatelessWidget {
  final List<Group> groups;
  final String? selectedGroupId;
  final String hintText;
  final bool enabled;
  final double height;
  final EdgeInsetsGeometry? margin;
  final ValueChanged<Group?>? onChanged;

  const GroupGlassSelector({
    super.key,
    required this.groups,
    required this.selectedGroupId,
    required this.onChanged,
    this.hintText = 'Выберите группу',
    this.enabled = true,
    this.height = 42,
    this.margin,
  });

  Group? get _selectedGroup {
    for (final group in groups) {
      if (group.id == selectedGroupId) {
        return group;
      }
    }
    return null;
  }

  Future<void> _openSelector(BuildContext context) async {
    if (!enabled || groups.isEmpty || onChanged == null) return;

    final selected = await showInMomentGlassBottomSheet<Group>(
      context: context,
      isScrollControlled: false,
      child: _GroupSelectorSheet(
        groups: groups,
        selectedGroupId: selectedGroupId,
      ),
    );

    if (selected != null) {
      onChanged?.call(selected);
    }
  }

  @override
  Widget build(BuildContext context) {
    final selected = _selectedGroup;

    return Padding(
      padding: margin ?? EdgeInsets.zero,
      child: InkWell(
        onTap: enabled ? () => _openSelector(context) : null,
        borderRadius: BorderRadius.circular(18),
        child: InMomentSurface(
          tone: InMomentSurfaceTone.base,
          blur: true,
          borderRadius: BorderRadius.circular(18),
          padding: const EdgeInsets.symmetric(horizontal: 12),
          child: SizedBox(
            height: height,
            child: Row(
              children: [
                _GroupAvatar(
                  name: selected?.name ?? hintText,
                  avatarUrl: selected?.avatarUrl,
                  radius: 13,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    selected?.name ?? hintText,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: selected != null
                          ? AppColors.textPrimary
                          : AppColors.textSecondary,
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                Icon(
                  Icons.keyboard_arrow_down_rounded,
                  size: 20,
                  color: enabled
                      ? AppColors.textSecondary.withValues(alpha: 0.88)
                      : AppColors.textSecondary.withValues(alpha: 0.45),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _GroupSelectorSheet extends StatelessWidget {
  final List<Group> groups;
  final String? selectedGroupId;

  const _GroupSelectorSheet({
    required this.groups,
    required this.selectedGroupId,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Padding(
          padding: EdgeInsets.fromLTRB(18, 4, 18, 0),
          child: Align(
            alignment: Alignment.centerLeft,
            child: Text(
              'Выберите группу',
              style: TextStyle(
                color: AppColors.textPrimary,
                fontSize: 17,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ),
        const SizedBox(height: 10),
        Flexible(
          child: ListView.separated(
            shrinkWrap: true,
            padding: const EdgeInsets.fromLTRB(10, 0, 10, 12),
            itemCount: groups.length,
            separatorBuilder: (_, _) => const SizedBox(height: 6),
            itemBuilder: (context, index) {
              final group = groups[index];
              final selected = group.id == selectedGroupId;

              return InkWell(
                borderRadius: BorderRadius.circular(18),
                onTap: () => Navigator.of(context).pop(group),
                child: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 10,
                  ),
                  decoration: BoxDecoration(
                    color: selected
                        ? AppColors.white.withValues(alpha: 0.08)
                        : Colors.transparent,
                    borderRadius: BorderRadius.circular(18),
                    border: Border.all(
                      color: selected
                          ? AppColors.accentLight.withValues(alpha: 0.36)
                          : AppColors.softStroke(0.05),
                    ),
                  ),
                  child: Row(
                    children: [
                      _GroupAvatar(
                        name: group.name,
                        avatarUrl: group.avatarUrl,
                        radius: 16,
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Text(
                          group.name,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            color: AppColors.textPrimary,
                            fontSize: 15,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      if (selected)
                        Icon(
                          Icons.check_rounded,
                          size: 18,
                          color: AppColors.accentLight.withValues(alpha: 0.95),
                        ),
                    ],
                  ),
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}

class _GroupAvatar extends StatelessWidget {
  final String name;
  final String? avatarUrl;
  final double radius;

  const _GroupAvatar({
    required this.name,
    required this.avatarUrl,
    required this.radius,
  });

  String _initials(String value) {
    final parts = value
        .trim()
        .split(RegExp(r'\s+'))
        .where((x) => x.isNotEmpty)
        .toList();

    if (parts.isEmpty) return 'G';
    if (parts.length == 1) {
      return parts.first.characters.take(1).toString().toUpperCase();
    }

    return (parts.first.characters.take(1).toString() +
            parts.last.characters.take(1).toString())
        .toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    final normalizedUrl = avatarUrl?.trim();
    final hasAvatar = normalizedUrl != null && normalizedUrl.isNotEmpty;

    return Container(
      width: radius * 2,
      height: radius * 2,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        border: Border.all(
          color: AppColors.accentLight.withValues(alpha: 0.40),
          width: 1.6,
        ),
      ),
      child: CircleAvatar(
        radius: radius,
        backgroundColor: AppColors.surfaceElevated.withValues(alpha: 0.82),
        backgroundImage: hasAvatar ? NetworkImage(normalizedUrl) : null,
        child: hasAvatar
            ? null
            : Text(
                _initials(name),
                style: TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: radius * 0.75,
                  fontWeight: FontWeight.w700,
                ),
              ),
      ),
    );
  }
}