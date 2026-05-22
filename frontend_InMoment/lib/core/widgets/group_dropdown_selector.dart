import 'dart:ui';

import 'package:flutter/material.dart';

import '../../features/groups/models/group.dart';
import '../theme/app_colors.dart';

class GroupDropdownSelector extends StatelessWidget {
  final List<Group> groups;
  final String? selectedGroupId;
  final String hintText;
  final bool enabled;
  final bool isLoading;
  final double height;
  final double borderRadius;
  final double avatarRadius;
  final double fontSize;
  final EdgeInsetsGeometry padding;
  final ValueChanged<String?>? onChanged;

  const GroupDropdownSelector({
    super.key,
    required this.groups,
    required this.selectedGroupId,
    required this.onChanged,
    this.hintText = 'Группа',
    this.enabled = true,
    this.isLoading = false,
    this.height = 42,
    this.borderRadius = 18,
    this.avatarRadius = 13,
    this.fontSize = 14,
    this.padding = const EdgeInsets.symmetric(horizontal: 12),
  });

  @override
  Widget build(BuildContext context) {
    final hasSelectedGroup = selectedGroupId != null &&
        groups.any((group) => group.id == selectedGroupId);

    final canInteract = enabled && !isLoading && groups.isNotEmpty;

    return Opacity(
      opacity: enabled ? 1 : 0.62,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(borderRadius),
        child: BackdropFilter(
          filter: ImageFilter.blur(sigmaX: 18, sigmaY: 18),
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 180),
            curve: Curves.easeOutCubic,
            height: height,
            padding: padding,
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [
                  AppColors.surfaceElevated.withValues(alpha: 0.38),
                  AppColors.surfaceSoft.withValues(alpha: 0.46),
                ],
              ),
              borderRadius: BorderRadius.circular(borderRadius),
              border: Border.all(
                color: hasSelectedGroup
                    ? AppColors.accentSoft.withValues(alpha: 0.16)
                    : AppColors.softStroke(0.09),
              ),
              boxShadow: [
                BoxShadow(
                  color: AppColors.black.withValues(alpha: 0.08),
                  blurRadius: 14,
                  offset: const Offset(0, 7),
                ),
              ],
            ),
            child: isLoading
                ? const Center(
                    child: SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  )
                : DropdownButtonHideUnderline(
                    child: DropdownButton<String>(
                      value: hasSelectedGroup ? selectedGroupId : null,
                      isExpanded: true,
                      dropdownColor: AppColors.surfaceElevated.withValues(alpha: 0.92),
                      borderRadius: BorderRadius.circular(borderRadius + 8),
                      menuMaxHeight: 360,
                      iconEnabledColor:
                          AppColors.textSecondary.withValues(alpha: 0.88),
                      iconDisabledColor:
                          AppColors.textSecondary.withValues(alpha: 0.38),
                      icon: AnimatedRotation(
                        turns: canInteract ? 0 : 0,
                        duration: const Duration(milliseconds: 160),
                        child: const Icon(
                          Icons.keyboard_arrow_down_rounded,
                          size: 21,
                        ),
                      ),
                      hint: Text(
                        hintText,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          color: AppColors.textSecondary,
                          fontWeight: FontWeight.w700,
                          fontSize: fontSize,
                          height: 1.1,
                        ),
                      ),
                      style: TextStyle(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w700,
                        fontSize: fontSize,
                        height: 1.1,
                      ),
                      selectedItemBuilder: (context) {
                        return groups.map((group) {
                          return _SelectedGroupRow(
                            group: group,
                            avatarRadius: avatarRadius,
                            fontSize: fontSize,
                          );
                        }).toList();
                      },
                      items: groups
                          .map(
                            (group) => DropdownMenuItem<String>(
                              value: group.id,
                              child: _DropdownGroupRow(
                                group: group,
                                selected: group.id == selectedGroupId,
                                avatarRadius: avatarRadius,
                                fontSize: fontSize,
                              ),
                            ),
                          )
                          .toList(),
                      onChanged: canInteract ? onChanged : null,
                    ),
                  ),
          ),
        ),
      ),
    );
  }
}

class _SelectedGroupRow extends StatelessWidget {
  final Group group;
  final double avatarRadius;
  final double fontSize;

  const _SelectedGroupRow({
    required this.group,
    required this.avatarRadius,
    required this.fontSize,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        _GroupDropdownAvatar(
          name: group.name,
          avatarUrl: group.avatarUrl,
          radius: avatarRadius,
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            group.name,
            overflow: TextOverflow.ellipsis,
            maxLines: 1,
            style: TextStyle(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
              fontSize: fontSize,
              height: 1.1,
              letterSpacing: -0.08,
            ),
          ),
        ),
      ],
    );
  }
}

class _DropdownGroupRow extends StatelessWidget {
  final Group group;
  final bool selected;
  final double avatarRadius;
  final double fontSize;

  const _DropdownGroupRow({
    required this.group,
    required this.selected,
    required this.avatarRadius,
    required this.fontSize,
  });

  @override
  Widget build(BuildContext context) {
    final avatarSize = avatarRadius + 2;

    return Container(
      constraints: const BoxConstraints(minHeight: 42),
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: [
          _GroupDropdownAvatar(
            name: group.name,
            avatarUrl: group.avatarUrl,
            radius: avatarSize,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              group.name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                color: AppColors.textPrimary,
                fontWeight: selected ? FontWeight.w800 : FontWeight.w700,
                fontSize: fontSize,
                height: 1.15,
              ),
            ),
          ),
          const SizedBox(width: 8),
          AnimatedOpacity(
            opacity: selected ? 1 : 0,
            duration: const Duration(milliseconds: 140),
            child: Icon(
              Icons.check_rounded,
              size: 18,
              color: AppColors.accentSoft.withValues(alpha: 0.95),
            ),
          ),
        ],
      ),
    );
  }
}

class _GroupDropdownAvatar extends StatelessWidget {
  final String name;
  final String? avatarUrl;
  final double radius;

  const _GroupDropdownAvatar({
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
    final normalized = avatarUrl?.trim();
    final hasAvatar = normalized != null && normalized.isNotEmpty;

    return Container(
      width: radius * 2,
      height: radius * 2,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: hasAvatar
            ? null
            : LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [
                  AppColors.accentStrong.withValues(alpha: 0.34),
                  AppColors.accentSecondary.withValues(alpha: 0.22),
                ],
              ),
        border: Border.all(
          color: AppColors.accentLight.withValues(alpha: 0.38),
          width: 1.6,
        ),
      ),
      child: ClipOval(
        child: hasAvatar
            ? Image.network(
                normalized,
                fit: BoxFit.cover,
                errorBuilder: (_, _, _) {
                  return _InitialsAvatarText(
                    initials: _initials(name),
                    radius: radius,
                  );
                },
              )
            : _InitialsAvatarText(
                initials: _initials(name),
                radius: radius,
              ),
      ),
    );
  }
}

class _InitialsAvatarText extends StatelessWidget {
  final String initials;
  final double radius;

  const _InitialsAvatarText({
    required this.initials,
    required this.radius,
  });

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Text(
        initials,
        style: TextStyle(
          color: AppColors.textPrimary,
          fontWeight: FontWeight.w800,
          fontSize: radius * 0.70,
          height: 1,
        ),
      ),
    );
  }
}