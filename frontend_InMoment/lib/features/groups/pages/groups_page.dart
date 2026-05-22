import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../controllers/active_group_controller.dart';
import '../models/group.dart';
import 'group_management_page.dart';
import 'create_group_page.dart';
import '../../../core/widgets/inmoment_responsive_content.dart';

class GroupsPage extends StatefulWidget {
  const GroupsPage({super.key});

  @override
  State<GroupsPage> createState() => _GroupsPageState();
}

class _GroupsPageState extends State<GroupsPage> {
  final _controller = ActiveGroupController.instance;
  bool _refreshing = false;

  @override
  void initState() {
    super.initState();
    _controller.addListener(_onChanged);
    _controller.load(force: true);
  }

  @override
  void dispose() {
    _controller.removeListener(_onChanged);
    super.dispose();
  }

  void _onChanged() {
    if (mounted) {
      setState(() {});
    }
  }

  Future<void> _refresh() async {
    if (_refreshing || _controller.loading) return;

    setState(() {
      _refreshing = true;
    });

    try {
      await _controller.load(force: true);
    } finally {
      if (mounted) {
        setState(() {
          _refreshing = false;
        });
      }
    }
  }

  Future<void> _setActiveGroup(Group group) async {
    try {
      await _controller.setActiveGroup(group);

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Активная группа: ${group.name}'),
        ),
      );
    } catch (_) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            _controller.error ?? 'Не удалось обновить активную группу',
            ),
          ),
        );
    }
  }

  Future<void> _openGroup(Group group) async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => GroupManagementPage(group: group),
      ),
    );

    if (!mounted) return;
    await _refresh();
  }

  Future<void> _openCreateGroup() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const CreateGroupPage(),
      ),
    );

    if (!mounted) return;
    await _refresh();
  }

  @override
  Widget build(BuildContext context) {
    final groups = _controller.groups;
    final activeGroup = _controller.activeGroup;
    final ownedGroups =
        groups.where((group) => group.isOwner).toList(growable: false);
    final memberGroups = groups
        .where((group) => !group.isOwner)
        .toList(growable: false);

    if (_controller.loading && !_controller.loaded) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: Center(
          child: CircularProgressIndicator(),
        ),
      );
    }

    if (_controller.error != null && groups.isEmpty) {
      return Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(
          title: const Text('Мои группы'),
        ),
        body: InMomentResponsiveContent(
          alignment: Alignment.center,
          child: Center(
            child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  _controller.error!,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 15,
                    height: 1.4,
                  ),
                ),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: _refreshing || _controller.loading ? null : _refresh,
                  child: _refreshing || _controller.loading
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Повторить'),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    }

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Мои группы'),
        actions: [
          IconButton(
            onPressed: _refreshing || _controller.loading ? null : _openCreateGroup,
            tooltip: 'Создать группу',
            icon: const Icon(Icons.group_add_rounded),
          ),
          IconButton(
            onPressed: _refreshing || _controller.loading ? null : _refresh,
            tooltip: 'Обновить',
            icon: _refreshing || _controller.loading
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      body: InMomentResponsiveContent(
        child: RefreshIndicator(
          onRefresh: _refresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
            children: [
              _ActiveGroupSummaryCard(
                activeGroup: activeGroup,
                groupsCount: groups.length,
                ownedGroupsCount: ownedGroups.length,
              ),
              const SizedBox(height: 16),
              if (groups.isEmpty)
                Container(
                  padding: const EdgeInsets.all(20),
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: BorderRadius.circular(24),
                    border: Border.all(color: AppColors.border),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Сейчас у вас нет групп. После принятия приглашения или создания своей группы они появятся здесь.',
                        style: TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 14,
                          height: 1.45,
                        ),
                      ),
                      const SizedBox(height: 16),
                      SizedBox(
                        width: double.infinity,
                        child: FilledButton.icon(
                          onPressed: _openCreateGroup,
                          icon: const Icon(Icons.group_add_rounded),
                          label: const Text('Создать первую группу'),
                        ),
                      ),
                    ],
                  ),
                )
              else ...[
                if (ownedGroups.isNotEmpty) ...[
                  const _SectionTitle(
                    title: 'Собственные группы',
                    subtitle:
                        'Здесь доступны участники, изменение названия, аватара и удаление участников.',
                  ),
                  const SizedBox(height: 10),
                  ...ownedGroups.map(
                    (group) => Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: _GroupCard(
                        group: group,
                        saving: _controller.saving,
                        onSetActive: () => _setActiveGroup(group),
                        onOpen: () => _openGroup(group),
                      ),
                    ),
                  ),
                ],
                if (memberGroups.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  const _SectionTitle(
                    title: 'Остальные группы',
                    subtitle:
                        'Для этих групп доступен просмотр состава участников и самостоятельный выход.',
                  ),
                  const SizedBox(height: 10),
                  ...memberGroups.map(
                    (group) => Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: _GroupCard(
                        group: group,
                        saving: _controller.saving,
                        onSetActive: () => _setActiveGroup(group),
                        onOpen: () => _openGroup(group),
                      ),
                    ),
                  ),
                ],
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _ActiveGroupSummaryCard extends StatelessWidget {
  final Group? activeGroup;
  final int groupsCount;
  final int ownedGroupsCount;

  const _ActiveGroupSummaryCard({
    required this.activeGroup,
    required this.groupsCount,
    required this.ownedGroupsCount,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Активная группа',
            style: TextStyle(
              color: AppColors.textSecondary,
              fontSize: 13,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            activeGroup?.name ?? 'Пока не выбрана',
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 20,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 10),
          Text(
            activeGroup == null
                ? 'Выберите группу, чтобы использовать её как основной контекст главного экрана.'
                : 'Именно эта группа используется как основная для главного экрана и будущего виджета.',
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 13,
              height: 1.4,
            ),
          ),
          const SizedBox(height: 14),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: [
              _MetaBadge(text: 'Всего групп: $groupsCount'),
              _MetaBadge(text: 'Моих групп: $ownedGroupsCount'),
              if (activeGroup?.isOwner == true)
                const _MetaBadge(text: 'Активная = моя', highlighted: true),
            ],
          ),
        ],
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  final String title;
  final String subtitle;

  const _SectionTitle({
    required this.title,
    required this.subtitle,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(left: 2, right: 2),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 16,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            subtitle,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 13,
              height: 1.35,
            ),
          ),
        ],
      ),
    );
  }
}

class _GroupCard extends StatelessWidget {
  final Group group;
  final bool saving;
  final VoidCallback onSetActive;
  final VoidCallback onOpen;

  const _GroupCard({
    required this.group,
    required this.saving,
    required this.onSetActive,
    required this.onOpen,
  });

  @override
  Widget build(BuildContext context) {
    final roleText = group.isOwner ? 'Владелец' : 'Участник';

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(
          color: group.isActiveGroup ? AppColors.accentLight : AppColors.border,
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _GroupAvatar(
            avatarUrl: group.avatarUrl,
            name: group.name,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    group.name,
                    style: const TextStyle(
                      color: AppColors.textPrimary,
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      _MetaBadge(text: roleText),
                      if (group.membersCount != null)
                        _MetaBadge(text: 'Участников: ${group.membersCount}'),
                      if (group.isActiveGroup)
                        const _MetaBadge(
                          text: 'Активная',
                          highlighted: true,
                        ),
                    ],
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(width: 12),
          SizedBox(
            width: 132,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                FilledButton(
                  onPressed: saving || group.isActiveGroup ? null : onSetActive,
                  child: Text(
                    group.isActiveGroup ? 'Выбрана' : 'Сделать активной',
                  ),
                ),
                const SizedBox(height: 8),
                OutlinedButton(
                  onPressed: onOpen,
                  child: Text(group.isOwner ? 'Управлять' : 'Открыть'),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _GroupAvatar extends StatelessWidget {
  final String? avatarUrl;
  final String name;

  const _GroupAvatar({
    required this.avatarUrl,
    required this.name,
  });

  @override
  Widget build(BuildContext context) {
    return CircleAvatar(
      radius: 26,
      backgroundColor: AppColors.accent.withValues(alpha: 0.24),
      backgroundImage: (avatarUrl != null && avatarUrl!.trim().isNotEmpty)
          ? NetworkImage(avatarUrl!)
          : null,
      child: (avatarUrl == null || avatarUrl!.trim().isEmpty)
          ? Text(
              name.isNotEmpty ? name[0].toUpperCase() : 'G',
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w700,
              ),
            )
          : null,
    );
  }
}

class _MetaBadge extends StatelessWidget {
  final String text;
  final bool highlighted;

  const _MetaBadge({
    required this.text,
    this.highlighted = false,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
      decoration: BoxDecoration(
        color: highlighted
            ? AppColors.accentSecondary.withValues(alpha: 0.24)
            : AppColors.card,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(
          color: highlighted ? AppColors.accentLight : AppColors.border,
        ),
      ),
      child: Text(
        text,
        style: const TextStyle(
          color: AppColors.textPrimary,
          fontSize: 12,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}