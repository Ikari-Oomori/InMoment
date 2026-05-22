import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../api/feed_api.dart';
import '../models/feed_item.dart';
import '../../../core/api/api_error.dart';
import '../../../core/realtime/group_realtime_service.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_compact_icon_button.dart';
import '../../../core/widgets/inmoment_section.dart';
import '../../../core/widgets/app_bottom_nav_bar.dart';
import '../../../core/widgets/group_dropdown_selector.dart';
import '../../../core/widgets/network_visual_media.dart';
import '../../../core/layout/inmoment_media_frame.dart';
import '../../shell/models/app_shell_tab.dart';
import '../../shell/pages/app_shell_page.dart';
import '../../groups/controllers/active_group_controller.dart';
import '../../groups/models/group.dart';
import '../../groups/pages/group_management_page.dart';
import '../../invitations/pages/invite_to_group_page.dart';
import '../../photo/pages/photo_details_page.dart';
import '../../reactions/api/reactions_api.dart';
import '../../reactions/models/reaction_catalog.dart';
import '../../reactions/models/reaction_summary_utils.dart';
import '../../reactions/widgets/reaction_counter_pill.dart';
import '../../reactions/widgets/reaction_picker_sheet.dart';

class FeedPage extends StatefulWidget {
  const FeedPage({super.key});

  @override
  State<FeedPage> createState() => _FeedPageState();
}

class _FeedPageState extends State<FeedPage> {
  final ActiveGroupController _activeGroupController =
      ActiveGroupController.instance;
  final FeedApi _feedApi = FeedApi();
  final GroupRealtimeService _realtime = GroupRealtimeService.instance;
  final ReactionsApi _reactionsApi = ReactionsApi();
  final Set<String> _updatingReactionPhotoIds = <String>{};

  bool _loading = true;
  bool _feedLoading = false;
  bool _refreshing = false;

  String? _error;
  String? _feedError;

  List<Group> _groups = [];
  Group? _selectedGroup;
  List<FeedItem> _feed = [];
  List<String> _groupIdsSnapshot = const [];

    int _topReactionType(FeedItem item) {
    return ReactionSummaryUtils.topReactionTypeFromPairs(
      item.reactions,
      typeSelector: (reaction) => reaction.type,
      countSelector: (reaction) => reaction.count,
    );
  }

  Future<void> _togglePrimaryReaction(FeedItem item) async {
    if (_updatingReactionPhotoIds.contains(item.photoId)) return;

    setState(() {
      _updatingReactionPhotoIds.add(item.photoId);
    });

    try {
      if (item.myReaction == 0) {
        await _reactionsApi.setReaction(
          photoId: item.photoId,
          type: ReactionCatalog.primary.type,
        );
      } else {
        await _reactionsApi.removeReaction(photoId: item.photoId);
      }

      await _reloadFeedAfterReaction(item.photoId);
    } catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'Не удалось обновить реакцию: ${_normalizeError(e)}',
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _updatingReactionPhotoIds.remove(item.photoId);
        });
      }
    }
  }

 Future<void> _showReactionPickerForItem(
    FeedItem item,
    Offset position,
  ) async {
    final selected = await showReactionPopupMenu(
      context,
      position: position,
      selectedType: item.myReaction,
    );

    if (selected == null) return;

    if (_updatingReactionPhotoIds.contains(item.photoId)) return;

    setState(() {
      _updatingReactionPhotoIds.add(item.photoId);
    });

    try {
      if (item.myReaction == selected.type) {
        await _reactionsApi.removeReaction(photoId: item.photoId);
      } else {
        await _reactionsApi.setReaction(
          photoId: item.photoId,
          type: selected.type,
        );
      }

      await _reloadFeedAfterReaction(item.photoId);
    } finally {
      if (mounted) {
        setState(() {
          _updatingReactionPhotoIds.remove(item.photoId);
        });
      }
    }
  }

  Future<void> _reloadFeedAfterReaction(String photoId) async {
    final group = _selectedGroup;
    if (group == null) return;

    await _loadFeedForGroup(group, silent: true);
  }

  String _normalizeError(Object error) {
    return ApiError.normalize(
      error,
      fallback: 'Не удалось выполнить действие. Попробуйте ещё раз.',
    );
  }

  @override
  void initState() {
    super.initState();
    _activeGroupController.addListener(_onGroupContextChanged);
    _realtime.addFeedChangedListener(_handleFeedChanged);
    _connectRealtime();
    _startInitialLoad();
  }

  void _startInitialLoad() {
    Future.microtask(() async {
      if (!mounted) return;

      setState(() {
        _loading = false;
      });

      await _loadInitial();
    });
  }

  @override
  void dispose() {
    _activeGroupController.removeListener(_onGroupContextChanged);
    _realtime.removeFeedChangedListener(_handleFeedChanged);
    super.dispose();
  }

  Future<void> _connectRealtime() async {
    try {
      await _realtime.ensureConnected();
    } catch (_) {}
  }

  Future<void> _safeJoinGroup(String groupId) async {
    try {
      await _realtime.joinGroup(groupId);
    } catch (_) {}
  }

  Future<void> _handleFeedChanged() async {
    if (!mounted) return;

    final group = _selectedGroup;
    if (group == null) return;

    await _loadFeedForGroup(group, silent: true);
  }

  void _openShellTab(AppShellTab tab) {
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(
        builder: (_) => AppShellPage(initialTab: tab),
      ),
      (route) => false,
    );
  }

  void _onGroupContextChanged() {
    if (!mounted) return;

    final groups = _deduplicateGroups(_activeGroupController.groups);
    final nextSnapshot = groups.map((group) => group.id).toList();
    final groupsChanged = !listEquals(_groupIdsSnapshot, nextSnapshot);

    final nextSelected = _resolveSelectedGroup(groups);
    final selectedChanged = nextSelected?.id != _selectedGroup?.id;

    if (!groupsChanged && !selectedChanged) {
      return;
    }

    setState(() {
      _groups = groups;
      _groupIdsSnapshot = nextSnapshot;
      _selectedGroup = nextSelected;

      if (groups.isEmpty) {
        _feed = [];
        _feedLoading = false;
        _feedError = null;
      }
    });

    if (nextSelected != null) {
      _loadFeedForGroup(nextSelected, silent: true);
    }
  }

  List<Group> _deduplicateGroups(List<Group> source) {
    final map = <String, Group>{};

    for (final group in source) {
      map[group.id] = group;
    }

    return map.values.toList();
  }

  Group? _resolveSelectedGroup(List<Group> groups) {
    if (groups.isEmpty) return null;

    final currentSelected = _selectedGroup;
    if (currentSelected != null) {
      for (final group in groups) {
        if (group.id == currentSelected.id) {
          return group;
        }
      }
    }

    final activeGroup = _activeGroupController.activeGroup;
    if (activeGroup != null) {
      for (final group in groups) {
        if (group.id == activeGroup.id) {
          return group;
        }
      }
    }

    return groups.first;
  }

  Future<void> _loadInitial() async {
    if (_loading) return;

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      await _activeGroupController.load(force: true);

      final groups = _deduplicateGroups(_activeGroupController.groups);
      final selectedGroup = _resolveSelectedGroup(groups);

      List<FeedItem> feed = [];
      if (selectedGroup != null) {
        feed = await _feedApi.getGroupFeed(selectedGroup.id);
        await _safeJoinGroup(selectedGroup.id);
      }

      if (!mounted) return;

      setState(() {
        _groups = groups;
        _groupIdsSnapshot = groups.map((g) => g.id).toList();
        _selectedGroup = selectedGroup;
        _feed = feed;
        _loading = false;
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _error = _normalizeError(e);
        _loading = false;
      });
    }
  }

  Future<void> _refresh() async {
    if (_refreshing || _loading || _feedLoading) return;

    setState(() {
      _refreshing = true;
      _error = null;
      _feedError = null;
    });

    try {
      await _activeGroupController.load(force: true);

      final groups = _deduplicateGroups(_activeGroupController.groups);
      final group = _resolveSelectedGroup(groups);

      if (!mounted) return;

      setState(() {
        _groups = groups;
        _groupIdsSnapshot = groups.map((g) => g.id).toList();
        _selectedGroup = group;
      });

      if (group == null) {
        setState(() {
          _feed = [];
          _feedError = null;
        });
        return;
      }

      await _loadFeedForGroup(group, silent: true);
    } catch (e) {
      if (!mounted) return;

      setState(() {
        if (_feed.isEmpty) {
          _feedError = _normalizeError(e);
        } else {
          _feedError = _normalizeError(e);
        }
      });

      _showMessage(_normalizeError(e));
    } finally {
      if (mounted) {
        setState(() {
          _refreshing = false;
        });
      }
    }
  }

  Future<void> _loadFeedForGroup(
    Group group, {
    bool silent = false,
  }) async {
    if (!mounted) return;

    setState(() {
      _selectedGroup = group;
      if (!silent) {
        _feedLoading = true;
      }
      _feedError = null;
    });

    try {
      final feed = await _feedApi.getGroupFeed(group.id);
      await _safeJoinGroup(group.id);

      if (!mounted) return;

      setState(() {
        _feed = feed;
        _feedLoading = false;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        if (!silent) {
          _feed = [];
        }

        _feedError = _normalizeError(e);
        _feedLoading = false;
      });
    }
  }

  Future<void> _selectGroupById(String? groupId) async {
    if (groupId == null) return;

    Group? group;

    for (final item in _groups) {
      if (item.id == groupId) {
        group = item;
        break;
      }
    }

    if (group == null) return;

    if (_selectedGroup?.id == group.id) return;

    try {
      // Сразу локально переключаем UI
      setState(() {
        _selectedGroup = group;
        _feed = [];
        _feedError = null;
        _feedLoading = true;
      });

      // Меняем active group
      if (_activeGroupController.activeGroup?.id != group.id) {
        await _activeGroupController.setActiveGroup(group);
      }

      // Единственная загрузка
      await _loadFeedForGroup(group);

    } catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'Не удалось открыть группу: ${_normalizeError(e)}',
          ),
        ),
      );
    }
  }

  Future<void> _openGroupSettingsPage() async {
    final group = _selectedGroup;

    if (group == null) {
      _showMessage('Сначала выберите группу');
      return;
    }

    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => GroupManagementPage(group: group),
      ),
    );

    if (!mounted) return;
    await _refresh();
  }

  Future<void> _openInvite() async {
    final group = _selectedGroup;

    if (group == null) {
      _showMessage('Сначала выберите группу');
      return;
    }

    if (!group.isManager) {
      _showMessage('Приглашать можно только в группе, которой вы управляете');
      return;
    }

    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const InviteToGroupPage(),
      ),
    );

    if (!mounted) return;
    await _refresh();
  }

  Future<void> _openPhoto(FeedItem item) async {
    final initialIndex = _feed.indexWhere((x) => x.photoId == item.photoId);

    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => _FeedPhotoPagerPage(
          items: List<FeedItem>.of(_feed),
          initialIndex: initialIndex < 0 ? 0 : initialIndex,
        ),
      ),
    );

    if (!mounted) return;

    final group = _selectedGroup;
    if (group == null) return;

    await _loadFeedForGroup(group, silent: true);
  }

  Future<void> _openGroupSheet() async {
    await _openGroupSettingsPage();
  }

  void _showMessage(String text) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(text)),
    );
  }

  String _formatDate(DateTime dateTime) {
    final local = dateTime.toLocal();

    String two(int n) => n.toString().padLeft(2, '0');

    return '${two(local.day)}.${two(local.month)}.${local.year} ${two(local.hour)}:${two(local.minute)}';
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: Center(
          child: CircularProgressIndicator(),
        ),
      );
    }

    if (_error != null) {
      return Scaffold(
        backgroundColor: AppColors.background,
        body: SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 10, 16, 24),
            child: Column(
              children: [
                _FeedPageHeader(
                  title: 'Лента',
                  onBack: () => Navigator.of(context).maybePop(),
                  onOpenSettings: _openGroupSheet,
                ),
                const Spacer(),
                InMomentSection(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(
                        Icons.error_outline_rounded,
                        color: AppColors.textSecondary,
                        size: 48,
                      ),
                      const SizedBox(height: 14),
                      Text(
                        'Не удалось открыть ленту.\n\n$_error',
                        textAlign: TextAlign.center,
                        style: const TextStyle(
                          color: AppColors.textPrimary,
                          height: 1.4,
                        ),
                      ),
                      const SizedBox(height: 16),
                      FilledButton(
                        onPressed: _loading ? null : _loadInitial,
                        child: _loading
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
                const Spacer(),
              ],
            ),
          ),
        ),
      );
    }

    final selectedGroup = _selectedGroup;
    final groups = _groups;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: AppColors.pageBackgroundGradient,
        ),
        child: Stack(
          children: [
            SafeArea(
              bottom: false,
              child: RefreshIndicator(
                onRefresh: _refresh,
                child: GestureDetector(
                  onVerticalDragEnd: (details) {
                    final velocity = details.primaryVelocity ?? 0;
                    if (velocity < -700) {
                      Navigator.of(context).maybePop();
                    }
                  },
                  child: CustomScrollView(
                    physics: const BouncingScrollPhysics(
                      parent: AlwaysScrollableScrollPhysics(),
                    ),
                    slivers: [
                      SliverToBoxAdapter(
                        child: Center(
                          child: SizedBox(
                            width: InMomentMediaFrame.resolveTabletContentWidth(
                              MediaQuery.sizeOf(context).width,
                            ),
                            child: Padding(
                              padding: const EdgeInsets.fromLTRB(0, 10, 0, 14),
                              child: Column(
                                children: [
                                  _FeedPageHeader(
                                    title: 'Лента',
                                    onBack: () => Navigator.of(context).maybePop(),
                                    onOpenSettings: _openGroupSheet,
                                  ),
                                  const SizedBox(height: 12),
                                  _FeedTopBar(
                                    groups: groups,
                                    selectedGroupId: selectedGroup?.id,
                                    savingGroup: _activeGroupController.saving,
                                    onOpenSettings: _openGroupSheet,
                                    onSelectGroup: _selectGroupById,
                                    onOpenInvite: _openInvite,
                                    canInvite: selectedGroup?.isManager == true,
                                  ),
                                ],
                              ),
                            ),  
                          ),
                        ),
                      ),
                      if (groups.isEmpty)
                        const SliverFillRemaining(
                          hasScrollBody: false,
                          child: _EmptyGroupsState(),
                        )
                      else if (_feedLoading)
                        const SliverFillRemaining(
                          hasScrollBody: false,
                          child: Center(
                            child: CircularProgressIndicator(),
                          ),
                        )
                      else if (_feedError != null && _feed.isEmpty)
                      SliverFillRemaining(
                        hasScrollBody: false,
                        child: _FeedErrorState(
                          message: _feedError!,
                          loading: _feedLoading,
                          onRetry: selectedGroup == null
                              ? null
                              : () => _loadFeedForGroup(selectedGroup),
                        ),
                      )
                      else if (_feed.isEmpty)
                        SliverFillRemaining(
                          hasScrollBody: false,
                          child: _EmptyFeedState(
                            groupName: selectedGroup?.name,
                          ),
                        )
                      else
                        SliverPadding(
                          padding: const EdgeInsets.fromLTRB(14, 0, 14, 132),
                          sliver: SliverToBoxAdapter(
                            child: Column(
                              children: [
                                if (_feedError != null) ...[
                                  _FeedInlineError(
                                    message: _feedError!,
                                    onRetry: selectedGroup == null || _feedLoading
                                        ? null
                                        : () => _loadFeedForGroup(selectedGroup, silent: true),
                                  ),
                                  const SizedBox(height: 12),
                                ],
                                _FeedMasonry(
                              key: ValueKey(_selectedGroup?.id),
                              items: _feed,
                              formatDate: _formatDate,
                              onOpen: _openPhoto,
                              topReactionTypeOf: _topReactionType,
                              isReactionUpdating: (photoId) =>
                                  _updatingReactionPhotoIds.contains(photoId),
                              onReactionTap: _togglePrimaryReaction,
                              onReactionPickerOpen: _showReactionPickerForItem,
                            ),                    
                          ],
                        ),
                      ),
                     ),                   
                   ],
                  ),
                ),
              ),
            ),
            Positioned(
              left: 0,
              right: 0,
              bottom: 0,
              child: AppBottomNavBar(
                selectedTab: AppShellTab.camera,
                onTabSelected: _openShellTab,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _FeedPageHeader extends StatelessWidget {
  final String title;
  final VoidCallback onBack;
  final Future<void> Function() onOpenSettings;

  const _FeedPageHeader({
    required this.title,
    required this.onBack,
    required this.onOpenSettings,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        InMomentCompactIconButton(
          icon: Icons.keyboard_arrow_up_rounded,
          onTap: onBack,
          translucent: true,
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            title,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 18,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        const SizedBox(width: 10),
        InMomentCompactIconButton(
          icon: Icons.tune_rounded,
          onTap: onOpenSettings,
          translucent: true,
        ),
      ],
    );
  }
}

class _FeedTopBar extends StatelessWidget {
  final List<Group> groups;
  final String? selectedGroupId;
  final bool savingGroup;
  final Future<void> Function() onOpenSettings;
  final ValueChanged<String?> onSelectGroup;
  final Future<void> Function() onOpenInvite;
  final bool canInvite;

  const _FeedTopBar({
    required this.groups,
    required this.selectedGroupId,
    required this.savingGroup,
    required this.onOpenSettings,
    required this.onSelectGroup,
    required this.onOpenInvite,
    required this.canInvite,
  });

  @override
  Widget build(BuildContext context) {

    return Row(
      children: [
        Expanded(
          child: GroupDropdownSelector(
            groups: groups,
            selectedGroupId: selectedGroupId,
            hintText: 'Группа',
            enabled: groups.isNotEmpty && !savingGroup,
            isLoading: savingGroup,
            height: 42,
            borderRadius: 18,
            avatarRadius: 13,
            fontSize: 14,
            onChanged: onSelectGroup,
          ),
        ),
        if (canInvite) ...[
          const SizedBox(width: 10),
          InMomentCompactIconButton(
            icon: Icons.person_add_alt_1_rounded,
            onTap: onOpenInvite,
            translucent: true,
          ),
        ],
      ],
    );
  }
}

class _FeedMasonry extends StatelessWidget {
  final List<FeedItem> items;
  final String Function(DateTime value) formatDate;
  final Future<void> Function(FeedItem item) onOpen;
  final int Function(FeedItem item) topReactionTypeOf;
  final bool Function(String photoId) isReactionUpdating;
  final Future<void> Function(FeedItem item) onReactionTap;
  final Future<void> Function(FeedItem item, Offset position) onReactionPickerOpen;

  const _FeedMasonry({
    required this.items,
    required this.formatDate,
    required this.onOpen,
    required this.topReactionTypeOf,
    required this.isReactionUpdating,
    required this.onReactionTap,
    required this.onReactionPickerOpen,
    super.key,
  });

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        const gap = InMomentMediaFrame.feedGap;

        final availableWidth = constraints.maxWidth.isFinite
            ? constraints.maxWidth
            : MediaQuery.sizeOf(context).width;
        final contentWidth = availableWidth.clamp(
          0.0,
          InMomentMediaFrame.resolveTabletContentWidth(
            MediaQuery.sizeOf(context).width,
          ),
        ).toDouble();

        final columnCount = contentWidth < 430
            ? 2
            : InMomentMediaFrame.resolveFeedColumnCount(contentWidth);

        final columnWidth = InMomentMediaFrame.resolveFeedCardWidth(
          contentWidth: contentWidth,
          columnCount: columnCount,
        );

        final columns = List.generate(columnCount, (_) => <FeedItem>[]);

        for (var i = 0; i < items.length; i++) {
          columns[i % columnCount].add(items[i]);
        }

        return Center(
          child: SizedBox(
            width: contentWidth,
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                for (var columnIndex = 0;
                    columnIndex < columns.length;
                    columnIndex++) ...[
                  SizedBox(
                    width: columnWidth,
                    child: Column(
                      children: [
                        for (var i = 0; i < columns[columnIndex].length; i++)
                          Padding(
                            padding: EdgeInsets.only(
                              bottom: i == columns[columnIndex].length - 1
                                  ? 0
                                  : gap,
                            ),
                            child: _FeedPhotoCard(
                              key: ValueKey(columns[columnIndex][i].photoId),
                              item: columns[columnIndex][i],
                              indexSeed: columnIndex + i * columnCount,
                              formattedDate: formatDate(
                                columns[columnIndex][i].createdAt,
                              ),
                              onOpen: () => onOpen(columns[columnIndex][i]),
                              topReactionType: topReactionTypeOf(
                                columns[columnIndex][i],
                              ),
                              hasMyReaction:
                                  columns[columnIndex][i].myReaction != 0,
                              reactionUpdating: isReactionUpdating(
                                columns[columnIndex][i].photoId,
                              ),
                              onReactionTap: () => onReactionTap(
                                columns[columnIndex][i],
                              ),
                              onReactionPickerOpen: (position) =>
                                  onReactionPickerOpen(
                                columns[columnIndex][i],
                                position,
                              ),
                            ),
                          ),
                      ],
                    ),
                  ),
                  if (columnIndex != columns.length - 1)
                    const SizedBox(width: gap),
                ],
              ],
            ),
          ),
        );
      },
    );
  }
}

class _FeedPhotoCard extends StatelessWidget {
  final FeedItem item;
  final int indexSeed;
  final String formattedDate;
  final VoidCallback onOpen;
  final int topReactionType;
  final bool hasMyReaction;
  final bool reactionUpdating;
  final VoidCallback onReactionTap;
  final ValueChanged<Offset> onReactionPickerOpen;

  const _FeedPhotoCard({
    super.key,
    required this.item,
    required this.indexSeed,
    required this.formattedDate,
    required this.onOpen,
    required this.topReactionType,
    required this.hasMyReaction,
    required this.reactionUpdating,
    required this.onReactionTap,
    required this.onReactionPickerOpen,
  });

  @override
  Widget build(BuildContext context) {
    final caption = (item.caption ?? '').trim();

    return RepaintBoundary(
      child: Material(
        color: Colors.transparent,
        borderRadius: BorderRadius.circular(26),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onOpen,
          borderRadius: BorderRadius.circular(26),
          child: Ink(
            decoration: BoxDecoration(
              color: AppColors.card,
              borderRadius: BorderRadius.circular(26),
              border: Border.all(
                color: AppColors.border.withValues(alpha: 0.86),
              ),
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                ClipRRect(
                  borderRadius: const BorderRadius.vertical(
                    top: Radius.circular(26),
                  ),
                  child: _AdaptiveFeedMedia(
                    photoId: item.photoId,
                    url: item.url,
                    contentType: item.contentType,
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(10, 9, 10, 10),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          _MiniAvatar(
                            imageUrl: item.authorProfilePhotoUrl,
                            userName: item.authorUserName,
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  item.authorUserName,
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: const TextStyle(
                                    color: AppColors.textPrimary,
                                    fontSize: 14,
                                    fontWeight: FontWeight.w800,
                                  ),
                                ),
                                Text(
                                  '@${item.authorUserName}',
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: const TextStyle(
                                    color: AppColors.textSecondary,
                                    fontSize: 12,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                      if (caption.isNotEmpty) ...[
                        const SizedBox(height: 8),
                        Text(
                          caption,
                          maxLines: 3,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            color: AppColors.textPrimary,
                            fontSize: 13,
                            height: 1.30,
                          ),
                        ),
                      ],
                      const SizedBox(height: 8),
                      Text(
                        formattedDate,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 12,
                          height: 1.25,
                        ),
                      ),
                      const SizedBox(height: 8),
                      FittedBox(
                        fit: BoxFit.scaleDown,
                        alignment: Alignment.centerLeft,
                        child: Row(
                          children: [
                            ReactionCounterPill(
                              value: '${item.reactionsCount}',
                              topReactionType: topReactionType,
                              reactionTypes: item.reactions.map((r) => r.type).toList(),
                              selected: hasMyReaction,
                              loading: reactionUpdating,
                              onTap: onReactionTap,
                              onOpenPicker: onReactionPickerOpen,
                            ),
                            const SizedBox(width: 8),
                            _FeedMetaPill(
                              icon: Icons.mode_comment_outlined,
                              value: '${item.commentsCount}',
                              onTap: onOpen,
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _MiniAvatar extends StatelessWidget {
  final String? imageUrl;
  final String userName;

  const _MiniAvatar({
    required this.imageUrl,
    required this.userName,
  });

  @override
  Widget build(BuildContext context) {
    if (imageUrl != null && imageUrl!.trim().isNotEmpty) {
      return CircleAvatar(
        radius: 16,
        backgroundImage: NetworkImage(imageUrl!),
      );
    }

    final trimmed = userName.trim();
    final letter = trimmed.isNotEmpty ? trimmed[0].toUpperCase() : 'U';

    return CircleAvatar(
      radius: 16,
      backgroundColor: AppColors.accent.withValues(alpha: 0.28),
      child: Text(
        letter,
        style: const TextStyle(
          color: AppColors.textPrimary,
          fontSize: 12,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

class _AdaptiveFeedMedia extends StatefulWidget {
  final String photoId;
  final String url;
  final String contentType;

  const _AdaptiveFeedMedia({
    required this.photoId,
    required this.url,
    required this.contentType,
  });

  @override
  State<_AdaptiveFeedMedia> createState() => _AdaptiveFeedMediaState();
}

class _AdaptiveFeedMediaState extends State<_AdaptiveFeedMedia> {
  double? _aspectRatio;
  ImageStream? _imageStream;
  ImageStreamListener? _imageListener;
  int _generation = 0;

  bool get _isVideo => widget.contentType.toLowerCase().startsWith('video/');
  bool get _isImage => widget.contentType.toLowerCase().startsWith('image/');

  @override
  void initState() {
    super.initState();
    _resolveAspectRatio();
  }

  @override
  void didUpdateWidget(covariant _AdaptiveFeedMedia oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.url != widget.url ||
        oldWidget.contentType != widget.contentType ||
        oldWidget.photoId != widget.photoId) {
      _resolveAspectRatio();
    }
  }

  @override
  void dispose() {
    _generation++;
    _detachImageListener();
    super.dispose();
  }

  void _detachImageListener() {
    final stream = _imageStream;
    final listener = _imageListener;

    if (stream != null && listener != null) {
      stream.removeListener(listener);
    }

    _imageStream = null;
    _imageListener = null;
  }

  void _resolveAspectRatio() {
    _generation++;
    _detachImageListener();

    final url = widget.url.trim();

    setState(() {
      _aspectRatio = null;
    });

    if (url.isEmpty) return;

    if (_isImage) {
      _resolveImageAspectRatio(url);
      return;
    }

    if (_isVideo) {
      setState(() {
        _aspectRatio = 9 / 16;
      });
    }
  }

  void _resolveImageAspectRatio(String url) {
    final generation = _generation;
    final provider = NetworkImage(url);
    final stream = provider.resolve(const ImageConfiguration());

    late final ImageStreamListener listener;

    listener = ImageStreamListener(
      (info, _) {
        final width = info.image.width;
        final height = info.image.height;

        if (!mounted || generation != _generation) return;
        if (width <= 0 || height <= 0) return;

        setState(() {
          _aspectRatio = width / height;
        });
      },
      onError: (_, _) {
        if (!mounted || generation != _generation) return;

        setState(() {
          _aspectRatio = 1;
        });
      },
    );

    _imageStream = stream;
    _imageListener = listener;
    stream.addListener(listener);
  }

  double _heightForWidth(double width) {
    final ratio = (_aspectRatio ?? 4 / 5).clamp(0.56, 1.78);
    final rawHeight = width / ratio;

    return rawHeight.clamp(168.0, 360.0);
  }

  @override
  Widget build(BuildContext context) {
    final url = widget.url.trim();

    return LayoutBuilder(
      builder: (context, constraints) {
        final width = constraints.maxWidth.isFinite
            ? constraints.maxWidth
            : MediaQuery.sizeOf(context).width / 2;

        final height = _heightForWidth(width);

        if (url.isEmpty) {
          return Container(
            width: double.infinity,
            height: height,
            color: AppColors.surface,
            alignment: Alignment.center,
            child: const Icon(
              Icons.photo_outlined,
              color: AppColors.textSecondary,
              size: 30,
            ),
          );
        }

        return AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          curve: Curves.easeOut,
          width: double.infinity,
          height: height,
          color: AppColors.background.withValues(alpha: 0.72),
          alignment: Alignment.center,
          child: NetworkVisualMedia(
            url: url,
            contentType: widget.contentType,
            allowInlineVideo: false,
            fit: _isVideo ? BoxFit.cover : BoxFit.contain,
            placeholderLabel: _isVideo
                ? 'Не удалось загрузить превью видео'
                : 'Не удалось загрузить медиа',
            showVideoBadge: _isVideo,
          ),
        );
      },
    );
  }
}

/*class _FeedVideoPlaceholder extends StatelessWidget {
  const _FeedVideoPlaceholder();

  @override
  Widget build(BuildContext context) {
    return Container(
      color: AppColors.surface,
      alignment: Alignment.center,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
        decoration: BoxDecoration(
          color: Colors.black.withValues(alpha: 0.34),
          borderRadius: BorderRadius.circular(999),
        ),
        child: const Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.play_arrow_rounded,
              color: Colors.white,
              size: 18,
            ),
            SizedBox(width: 5),
            Text(
              'Видео',
              style: TextStyle(
                color: Colors.white,
                fontSize: 12,
                fontWeight: FontWeight.w800,
              ),
            ),
          ],
        ),
      ),
    );
  }
}*/

class _FeedErrorState extends StatelessWidget {
  final String message;
  final bool loading;
  final VoidCallback? onRetry;

  const _FeedErrorState({
    required this.message,
    required this.loading,
    required this.onRetry,
  });

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: InMomentSection(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(
                Icons.error_outline_rounded,
                color: AppColors.textSecondary,
                size: 42,
              ),
              const SizedBox(height: 12),
              const Text(
                'Не удалось загрузить ленту',
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: 15,
                  fontWeight: FontWeight.w800,
                  height: 1.35,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                message,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: AppColors.textSecondary,
                  fontSize: 13,
                  height: 1.4,
                ),
              ),
              const SizedBox(height: 14),
              FilledButton(
                onPressed: loading ? null : onRetry,
                child: loading
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
    );
  }
}

class _FeedInlineError extends StatelessWidget {
  final String message;
  final VoidCallback? onRetry;

  const _FeedInlineError({
    required this.message,
    required this.onRetry,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 14, 14, 14),
      decoration: BoxDecoration(
        color: AppColors.card.withValues(alpha: 0.72),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(
          color: AppColors.error.withValues(alpha: 0.18),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(
            Icons.error_outline_rounded,
            color: AppColors.textSecondary,
            size: 20,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              message,
              style: const TextStyle(
                color: AppColors.textSecondary,
                fontSize: 13,
                height: 1.35,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
          const SizedBox(width: 10),
          TextButton(
            onPressed: onRetry,
            child: const Text('Повторить'),
          ),
        ],
      ),
    );
  }
}

class _EmptyGroupsState extends StatelessWidget {
  const _EmptyGroupsState();

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: InMomentSection(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: const [
              Icon(
                Icons.group_add_outlined,
                color: AppColors.textSecondary,
                size: 42,
              ),
              SizedBox(height: 12),
              Text(
                'У вас пока нет групп.',
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: 15,
                  fontWeight: FontWeight.w800,
                  height: 1.35,
                ),
              ),
              SizedBox(height: 8),
              Text(
                'Создайте группу или примите приглашение, чтобы начать обмениваться моментами.',
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: AppColors.textSecondary,
                  fontSize: 13,
                  height: 1.4,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _EmptyFeedState extends StatelessWidget {
  final String? groupName;

  const _EmptyFeedState({
    required this.groupName,
  });

  @override
  Widget build(BuildContext context) {
    final hasGroup = groupName != null && groupName!.trim().isNotEmpty;

    final title = hasGroup
        ? 'В группе «${groupName!.trim()}» пока нет фото'
        : 'Выберите группу';

    final subtitle = hasGroup
        ? 'Опубликуйте первый момент или дождитесь публикаций от участников группы.'
        : 'После выбора группы здесь появятся фотографии и обсуждения.';

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: InMomentSection(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(
                Icons.photo_library_outlined,
                color: AppColors.textSecondary,
                size: 42,
              ),
              const SizedBox(height: 12),
              Text(
                title,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: 15,
                  fontWeight: FontWeight.w800,
                  height: 1.35,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                subtitle,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: AppColors.textSecondary,
                  fontSize: 13,
                  height: 1.4,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _FeedMetaPill extends StatelessWidget {
  final IconData icon;
  final String value;
  final VoidCallback onTap;

  const _FeedMetaPill({
    required this.icon,
    required this.value,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.surface,
      borderRadius: BorderRadius.circular(999),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(999),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(999),
            border: Border.all(color: AppColors.border),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                icon,
                size: 15,
                color: AppColors.textSecondary,
              ),
              const SizedBox(width: 5),
              Text(
                value,
                style: const TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: 12,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _FeedPhotoPagerPage extends StatefulWidget {
  final List<FeedItem> items;
  final int initialIndex;

  const _FeedPhotoPagerPage({
    required this.items,
    required this.initialIndex,
  });

  @override
  State<_FeedPhotoPagerPage> createState() => _FeedPhotoPagerPageState();
}

class _FeedPhotoPagerPageState extends State<_FeedPhotoPagerPage> {
  late final PageController _controller;

  @override
  void initState() {
    super.initState();

    final safeIndex = widget.items.isEmpty
        ? 0
        : widget.initialIndex.clamp(0, widget.items.length - 1);

    _controller = PageController(initialPage: safeIndex);

    WidgetsBinding.instance.addPostFrameCallback((_) {
      _precacheAround(safeIndex);
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _precacheAround(int index) {
    if (!mounted) return;

    for (final i in <int>[index - 1, index, index + 1]) {
      if (i < 0 || i >= widget.items.length) continue;

      final item = widget.items[i];
      final url = item.url.trim();
      final isVideo = item.contentType.toLowerCase().startsWith('video/');

      if (url.isNotEmpty && !isVideo) {
        precacheImage(NetworkImage(url), context);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    if (widget.items.isEmpty) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: Center(
          child: Text(
            'Публикация недоступна',
            style: TextStyle(color: AppColors.textPrimary),
          ),
        ),
      );
    }

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Stack(
        children: [
          PageView.builder(
              controller: _controller,
              itemCount: widget.items.length,
              allowImplicitScrolling: true,
              onPageChanged: (index) {

                _precacheAround(index);
              },
              itemBuilder: (context, index) {
                final item = widget.items[index];

                return _KeepAlivePhotoDetailsPage(
                  photoId: item.photoId,
                  groupId: item.groupId,
                );
              }
          )
        ],
      ),
    );
  }
}

class _KeepAlivePhotoDetailsPage extends StatefulWidget {
  final String photoId;
  final String groupId;

  const _KeepAlivePhotoDetailsPage({
    required this.photoId,
    required this.groupId,
  });

  @override
  State<_KeepAlivePhotoDetailsPage> createState() =>
      _KeepAlivePhotoDetailsPageState();
}

class _KeepAlivePhotoDetailsPageState extends State<_KeepAlivePhotoDetailsPage>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);

    return PhotoDetailsPage(
      key: ValueKey(widget.photoId),
      photoId: widget.photoId,
      groupId: widget.groupId,
    );
  }
}