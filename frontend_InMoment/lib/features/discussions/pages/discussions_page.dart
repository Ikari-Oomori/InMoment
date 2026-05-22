import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/realtime/group_realtime_service.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/network_visual_media.dart';
import '../../groups/controllers/active_group_controller.dart';
import '../../groups/models/group.dart';
import '../../photo/api/photo_api.dart';
import '../../photo/pages/photo_details_page.dart';
import '../api/discussions_api.dart';
import '../models/discussion_item.dart';

class DiscussionsPage extends StatefulWidget {
  const DiscussionsPage({super.key});

  @override
  State<DiscussionsPage> createState() => _DiscussionsPageState();
}

class _DiscussionsPageState extends State<DiscussionsPage> {
  final _activeGroupController = ActiveGroupController.instance;
  final _discussionsApi = DiscussionsApi();
  final _realtime = GroupRealtimeService.instance;
  final _photoApi = PhotoApi();

  List<DiscussionItem> _discussions = [];

  bool _loading = true;
  bool _discussionsLoading = false;
  String? _error;
  String? _discussionsError;

  String? _selectedGroupId;
  List<String> _groupIdsSnapshot = const [];

  final Set<String> _updatingReactionPhotoIds = <String>{};

  static const List<_ReactionOption> _reactionOptions = [
    _ReactionOption(1, '❤️'),
    _ReactionOption(2, '💜'),
    _ReactionOption(3, '🔥'),
    _ReactionOption(4, '😊'),
  ];

  @override
  void initState() {
    super.initState();
    _activeGroupController.addListener(_onGroupContextChanged);
    _realtime.addFeedChangedListener(_handleFeedChanged);
    _connectRealtime();
    _loadInitialData();
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

  String _normalizeError(Object error) {
    return ApiError.normalize(
      error,
      fallback: 'Не удалось загрузить обсуждения. Попробуйте ещё раз.',
    );
  }

  Future<void> _handleFeedChanged() async {
    if (!mounted) return;

    final group = _selectedGroup;
    if (group == null) return;

    await _loadDiscussionsForGroup(group, silent: true);
  }

  Future<void> _loadInitialData() async {
    try {
      await _activeGroupController.load(force: true);

      final selected = _resolveSelectedGroup(
        groups: _activeGroupController.groups,
        preferredGroupId: _selectedGroupId,
      );

      if (!mounted) return;

      setState(() {
        _groupIdsSnapshot =
            _activeGroupController.groups.map((g) => g.id).toList();
        _selectedGroupId = selected?.id;
        _loading = false;
      });

      if (selected != null) {
        await _loadDiscussionsForGroup(selected, silent: true);
      }
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _error = _normalizeError(e);
        _loading = false;
      });
    }
  }

  void _onGroupContextChanged() {
    if (!mounted) return;

    final groups = _activeGroupController.groups;
    final nextSnapshot = groups.map((group) => group.id).toList();
    final groupsChanged = !listEquals(_groupIdsSnapshot, nextSnapshot);

    final nextSelected = _resolveSelectedGroup(
      groups: groups,
      preferredGroupId: _selectedGroupId,
    );

    final nextSelectedId = nextSelected?.id;
    final selectedChanged = nextSelectedId != _selectedGroupId;

    if (!groupsChanged && !selectedChanged) {
      return;
    }

    setState(() {
      _groupIdsSnapshot = nextSnapshot;
      _selectedGroupId = nextSelectedId;

      if (groups.isEmpty) {
        _discussions = [];
        _discussionsError = null;
        _discussionsLoading = false;
        _updatingReactionPhotoIds.clear();
      }
    });

    if (nextSelected != null) {
      _loadDiscussionsForGroup(nextSelected, silent: true);
    }
  }

  Group? get _selectedGroup {
    final groups = _activeGroupController.groups;
    for (final group in groups) {
      if (group.id == _selectedGroupId) {
        return group;
      }
    }
    return null;
  }

  Group? _resolveSelectedGroup({
    required List<Group> groups,
    required String? preferredGroupId,
  }) {
    if (groups.isEmpty) return null;

    if (preferredGroupId != null) {
      for (final group in groups) {
        if (group.id == preferredGroupId) {
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

  Future<void> _loadDiscussionsForGroup(
    Group group, {
    bool silent = false,
  }) async {
    if (!mounted) return;

    setState(() {
      _selectedGroupId = group.id;
      if (!silent) {
        _discussionsLoading = true;
      }
      _discussionsError = null;
    });

    try {
      final discussions = await _discussionsApi.getGroupDiscussions(group.id);
      await _safeJoinGroup(group.id);

      if (!mounted) return;

      setState(() {
        _discussions = discussions;
        _discussionsLoading = false;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _discussions = [];
        _discussionsLoading = false;
        _discussionsError = _normalizeError(e);
      });
    }
  }

  Future<void> _refresh() async {
    await _activeGroupController.load(force: true);

    final group = _selectedGroup;
    if (group == null) return;

    await _loadDiscussionsForGroup(group);
  }

  void _openActiveGroup() {
    final activeGroup = _activeGroupController.activeGroup;
    if (activeGroup == null) return;

    _loadDiscussionsForGroup(activeGroup);
  }

  String _formatDate(DateTime dateTime) {
    final local = dateTime.toLocal();

    String two(int n) => n.toString().padLeft(2, '0');

    return '${two(local.day)}.${two(local.month)}.${local.year} ${two(local.hour)}:${two(local.minute)}';
  }

  String _resolveDiscussionContentType(DiscussionItem item) {
    final explicit = item.photoContentType?.trim();
    if (explicit != null && explicit.isNotEmpty) {
      return explicit;
    }

    final normalized = item.photoUrl.toLowerCase();

    if (normalized.contains('.mp4') ||
        normalized.contains('.mov') ||
        normalized.contains('.m4v') ||
        normalized.contains('.webm') ||
        normalized.contains('.3gp') ||
        normalized.contains('/video') ||
        normalized.contains('contenttype=video') ||
        normalized.contains('content-type=video') ||
        normalized.contains('mime=video')) {
      return 'video/mp4';
    }

    return 'image/jpeg';
  }

  Future<void> _openDiscussion(DiscussionItem item) async {
    final group = _selectedGroup;

    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => PhotoDetailsPage(
          photoId: item.photoId,
          groupId: group?.id,
        ),
      ),
    );

    if (!mounted) return;
    if (group == null) return;

    await _loadDiscussionsForGroup(group, silent: true);
  }

  Future<void> _toggleReaction({
    required DiscussionItem item,
    required int nextReaction,
  }) async {
    if (_updatingReactionPhotoIds.contains(item.photoId)) return;

    setState(() {
      _updatingReactionPhotoIds.add(item.photoId);
    });

    try {
      if (item.myReaction == nextReaction) {
        await _photoApi.removeReaction(item.photoId);
      } else {
        await _photoApi.setReaction(
          photoId: item.photoId,
          type: nextReaction,
        );
      }

      final group = _selectedGroup;
      if (group == null) return;

      await _loadDiscussionsForGroup(group, silent: true);
    } catch (e) {
      if (!mounted) return;

      final text = _normalizeError(e);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Не удалось обновить реакцию: $text'),
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

  @override
  Widget build(BuildContext context) {
    final groups = _activeGroupController.groups;
    final selectedGroup = _selectedGroup;

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
        appBar: AppBar(
          title: const Text('Обсуждения'),
        ),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Text(
              'Не удалось загрузить обсуждения.\n\n$_error',
              textAlign: TextAlign.center,
              style: const TextStyle(color: AppColors.textPrimary),
            ),
          ),
        ),
      );
    }

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Обсуждения'),
        actions: [
          IconButton(
            tooltip: 'Открыть активную группу',
            onPressed: _activeGroupController.activeGroup == null
                ? null
                : _openActiveGroup,
            icon: const Icon(Icons.star_rounded),
          ),
        ],
      ),
      body: Column(
        children: [
          if (groups.isNotEmpty) _buildGroupSelector(groups, selectedGroup),
          Expanded(
            child: Builder(
              builder: (context) {
                if (groups.isEmpty) {
                  return RefreshIndicator(
                    onRefresh: _refresh,
                    child: ListView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      padding: const EdgeInsets.all(24),
                      children: const [
                        SizedBox(height: 160),
                        Text(
                          'У вас пока нет групп.\nПримите приглашение или создайте группу — после этого обсуждения появятся здесь.',
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            color: AppColors.textPrimary,
                            height: 1.5,
                          ),
                        ),
                      ],
                    ),
                  );
                }

                if (_discussionsLoading) {
                  return const Center(
                    child: CircularProgressIndicator(),
                  );
                }

                if (_discussionsError != null) {
                  return Center(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Text(
                        'Не удалось загрузить обсуждения.\n\n$_discussionsError',
                        textAlign: TextAlign.center,
                        style: const TextStyle(color: AppColors.textPrimary),
                      ),
                    ),
                  );
                }

                if (_discussions.isEmpty) {
                  return RefreshIndicator(
                    onRefresh: _refresh,
                    child: ListView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      padding: const EdgeInsets.all(24),
                      children: [
                        const SizedBox(height: 160),
                        Text(
                          selectedGroup == null
                              ? 'Выберите группу, чтобы открыть обсуждения.'
                              : 'В группе «${selectedGroup.name}» пока нет обсуждений.\nКогда под фотографиями появятся комментарии, они будут отображаться здесь.',
                          textAlign: TextAlign.center,
                          style: const TextStyle(
                            color: AppColors.textPrimary,
                            height: 1.5,
                          ),
                        ),
                      ],
                    ),
                  );
                }

                return RefreshIndicator(
                  onRefresh: _refresh,
                  child: ListView.separated(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
                    itemCount: _discussions.length,
                    separatorBuilder: (_, _) => const SizedBox(height: 12),
                    itemBuilder: (context, index) {
                      final item = _discussions[index];
                      final reactionsUpdating =
                          _updatingReactionPhotoIds.contains(item.photoId);

                      return InkWell(
                        borderRadius: BorderRadius.circular(24),
                        onTap: () => _openDiscussion(item),
                        child: Container(
                          decoration: BoxDecoration(
                            color: AppColors.surface,
                            borderRadius: BorderRadius.circular(24),
                            border: Border.all(color: AppColors.border),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              ClipRRect(
                                borderRadius: const BorderRadius.vertical(
                                  top: Radius.circular(24),
                                ),
                                child: AspectRatio(
                                  aspectRatio: 1,
                                  child: NetworkVisualMedia(
                                    url: item.photoUrl,
                                    contentType: _resolveDiscussionContentType(item),
                                    allowInlineVideo: false,
                                    autoplay: false,
                                    looping: false,
                                    startMuted: true,
                                    showControls: false,
                                    allowPlaybackSpeedChanging: false,
                                    showVideoBadge: true,
                                    fit: BoxFit.cover,
                                    placeholderLabel: 'Не удалось загрузить медиа',
                                  ),
                                ),
                              ),
                              Padding(
                                padding:
                                    const EdgeInsets.fromLTRB(14, 14, 14, 14),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      '@${item.photoAuthorUserName}',
                                      style: const TextStyle(
                                        color: AppColors.textPrimary,
                                        fontWeight: FontWeight.w700,
                                        fontSize: 15,
                                      ),
                                    ),
                                    const SizedBox(height: 6),
                                    if ((item.photoCaption ?? '')
                                        .trim()
                                        .isNotEmpty)
                                      Text(
                                        item.photoCaption!.trim(),
                                        maxLines: 2,
                                        overflow: TextOverflow.ellipsis,
                                        style: const TextStyle(
                                          color: AppColors.textPrimary,
                                          fontSize: 15,
                                          height: 1.35,
                                        ),
                                      ),
                                    if ((item.photoCaption ?? '')
                                        .trim()
                                        .isNotEmpty)
                                      const SizedBox(height: 8),
                                    Text(
                                      item.latestCommentText?.trim().isNotEmpty ==
                                              true
                                          ? 'Последний комментарий: ${item.latestCommentText!.trim()}'
                                          : 'Комментариев: ${item.commentsCount}',
                                      maxLines: 2,
                                      overflow: TextOverflow.ellipsis,
                                      style: const TextStyle(
                                        color: AppColors.textSecondary,
                                        fontSize: 13,
                                        height: 1.35,
                                      ),
                                    ),
                                    const SizedBox(height: 12),
                                    _DiscussionReactionsSection(
                                      item: item,
                                      updating: reactionsUpdating,
                                      options: _reactionOptions,
                                      onTapReaction: (option) {
                                        _toggleReaction(
                                          item: item,
                                          nextReaction: option.type,
                                        );
                                      },
                                    ),
                                    const SizedBox(height: 12),
                                    Row(
                                      children: [
                                        Text(
                                          _formatDate(item.lastActivityAt),
                                          style: const TextStyle(
                                            color: AppColors.textSecondary,
                                            fontSize: 12,
                                          ),
                                        ),
                                        const Spacer(),
                                        Container(
                                          padding: const EdgeInsets.symmetric(
                                            horizontal: 12,
                                            vertical: 8,
                                          ),
                                          decoration: BoxDecoration(
                                            color: AppColors.surfaceLight,
                                            borderRadius:
                                                BorderRadius.circular(999),
                                          ),
                                          child: Text(
                                            'Комментариев: ${item.commentsCount}',
                                            style: const TextStyle(
                                              color: AppColors.textPrimary,
                                              fontSize: 12,
                                              fontWeight: FontWeight.w600,
                                            ),
                                          ),
                                        ),
                                      ],
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                        ),
                      );
                    },
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildGroupSelector(List<Group> groups, Group? selectedGroup) {
    return SizedBox(
      height: 62,
      child: ListView.separated(
        padding: const EdgeInsets.fromLTRB(16, 10, 16, 8),
        scrollDirection: Axis.horizontal,
        itemCount: groups.length,
        separatorBuilder: (_, _) => const SizedBox(width: 10),
        itemBuilder: (context, index) {
          final group = groups[index];
          final selected = selectedGroup?.id == group.id;

          return ChoiceChip(
            selected: selected,
            onSelected: (_) => _loadDiscussionsForGroup(group),
            label: Text(group.name),
            avatar: selected
                ? const Icon(
                    Icons.check_rounded,
                    size: 16,
                    color: AppColors.textPrimary,
                  )
                : null,
            selectedColor: AppColors.accent.withValues(alpha: 0.45),
            backgroundColor: AppColors.surface,
            side: BorderSide(
              color: selected ? AppColors.accentLight : AppColors.border,
            ),
            labelStyle: const TextStyle(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w600,
            ),
          );
        },
      ),
    );
  }
}

class _DiscussionReactionsSection extends StatelessWidget {
  final DiscussionItem item;
  final bool updating;
  final List<_ReactionOption> options;
  final ValueChanged<_ReactionOption> onTapReaction;

  const _DiscussionReactionsSection({
    required this.item,
    required this.updating,
    required this.options,
    required this.onTapReaction,
  });

  int _countForType(int type) {
    for (final reaction in item.reactions) {
      if (reaction.type == type) {
        return reaction.count;
      }
    }
    return 0;
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(
          color: AppColors.border.withValues(alpha: 0.25),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Text(
                'Реакции',
                style: TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: 14,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const Spacer(),
              if (updating)
                const SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              else
                Text(
                  '${item.reactionsCount}',
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                  ),
                ),
            ],
          ),
          const SizedBox(height: 10),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: options.map((option) {
              final selected = item.myReaction == option.type;
              final count = _countForType(option.type);

              return IgnorePointer(
                ignoring: updating,
                child: GestureDetector(
                  onTap: () => onTapReaction(option),
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 180),
                    padding: const EdgeInsets.symmetric(
                      horizontal: 10,
                      vertical: 8,
                    ),
                    decoration: BoxDecoration(
                      color: selected ? AppColors.accent : AppColors.surface,
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(
                        color: selected
                            ? AppColors.textPrimary
                            : AppColors.border.withValues(alpha: 0.35),
                      ),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          option.emoji,
                          style: const TextStyle(fontSize: 18),
                        ),
                        const SizedBox(width: 6),
                        Text(
                          '$count',
                          style: const TextStyle(
                            color: AppColors.textPrimary,
                            fontSize: 12,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              );
            }).toList(),
          ),
        ],
      ),
    );
  }
}

class _ReactionOption {
  final int type;
  final String emoji;

  const _ReactionOption(this.type, this.emoji);
}