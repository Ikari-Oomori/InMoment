import 'dart:ui' show ImageFilter;
import 'dart:async';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:share_plus/share_plus.dart';
import 'package:video_player/video_player.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/group_dropdown_selector.dart';
import '../../../core/widgets/inmoment_surface.dart';
import '../../../core/widgets/network_visual_media.dart';
import '../../../core/widgets/inmoment_responsive_content.dart';
import '../../../core/layout/inmoment_media_frame.dart';
import '../api/system_memories_api.dart';
import '../models/system_memory.dart';
import '../../feed/api/feed_api.dart';
import '../../feed/models/feed_item.dart';
import '../../groups/controllers/active_group_controller.dart';
import '../../groups/models/group.dart';
import '../../profile/api/profile_api.dart';
import '../../profile/models/user_profile.dart';

class MemoriesPage extends StatefulWidget {
  final String? initialSystemMemoryId;

  const MemoriesPage({super.key, this.initialSystemMemoryId});

  @override
  State<MemoriesPage> createState() => _MemoriesPageState();
}

class _MemoriesPageState extends State<MemoriesPage> {
  final ActiveGroupController _activeGroupController =
      ActiveGroupController.instance;
  final FeedApi _feedApi = FeedApi();
  final ProfileApi _profileApi = ProfileApi();
  final SystemMemoriesApi _systemMemoriesApi = SystemMemoriesApi();

  int _selectedTabIndex = 0;
  DateTime _displayedMonth = DateTime(DateTime.now().year, DateTime.now().month);
  DateTime _selectedDate = DateTime.now();

  bool _loading = true;
  bool _silentRefreshing = false;
  bool _loadInProgress = false;
  String? _error;

  UserProfile? _profile;
  List<FeedItem> _allMoments = const [];
  List<SystemMemory> _systemMemories = const [];

  @override
  void initState() {
    super.initState();
    _activeGroupController.addListener(_onGroupsChanged);
    _load();
    WidgetsBinding.instance.addPostFrameCallback((_) => _openInitialSystemMemoryIfNeeded());
  }

  @override
  void dispose() {
    _activeGroupController.removeListener(_onGroupsChanged);
    super.dispose();
  }

  Future<void> _openInitialSystemMemoryIfNeeded() async {
    final id = widget.initialSystemMemoryId;
    if (id == null || id.trim().isEmpty) return;

    try {
      final memory = await _systemMemoriesApi.getById(id);
      if (!mounted) return;
      await _openSystemMemory(memory);
    } catch (_) {
      // Если воспоминание уже недоступно, остаёмся на общем экране воспоминаний.
    }
  }

  void _onGroupsChanged() {
    if (!mounted) return;
    _load(silent: true);
  }


  Future<void> _load({bool silent = false}) async {
    if (_loadInProgress) return;

    _loadInProgress = true;

    if (!silent) {
      setState(() {
        _loading = true;
        _error = null;
      });
    } else {
      setState(() {
        _silentRefreshing = true;
      });
    }

    try {
      await _activeGroupController.load(force: false);

      final activeGroup = _activeGroupController.activeGroup;
      final profile = await _profileApi.getMe();

      final systemMemories = await _systemMemoriesApi.list();

      List<FeedItem> moments = const [];
      if (activeGroup != null) {
        moments = await _feedApi.getGroupFeed(activeGroup.id);
      }

      if (!mounted) return;

      setState(() {
        _profile = profile;
        _allMoments = moments;
        _systemMemories = systemMemories;
        _loading = false;
        _silentRefreshing = false;
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;

      final message = ApiError.normalize(
        e,
        fallback: 'Не удалось загрузить воспоминания.',
      );

      setState(() {
        _loading = false;
        _silentRefreshing = false;

        if (!silent || (_profile == null && _allMoments.isEmpty)) {
          _error = message;
        }
      });

      if (silent) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(message)),
        );
      }
    } finally {
      _loadInProgress = false;
    }
  }

  void _goToPreviousMonth() {
    setState(() {
      _displayedMonth = DateTime(
        _displayedMonth.year,
        _displayedMonth.month - 1,
      );
      _selectedDate = DateTime(
        _displayedMonth.year,
        _displayedMonth.month,
        1,
      );
    });
  }

  void _goToNextMonth() {
    setState(() {
      _displayedMonth = DateTime(
        _displayedMonth.year,
        _displayedMonth.month + 1,
      );
      _selectedDate = DateTime(
        _displayedMonth.year,
        _displayedMonth.month,
        1,
      );
    });
  }

  Future<void> _changeActiveGroup(Group? group) async {
    if (group == null) return;

    try {
      await _activeGroupController.setActiveGroup(group);
      await _load(silent: true);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось загрузить данные.',
            ),
          ),
        ),
      );
    }
  }

  void _selectDate(DateTime date) {
    setState(() {
      _selectedDate = date;
      _displayedMonth = DateTime(date.year, date.month);
    });
  }

  bool _isSameDate(DateTime a, DateTime b) {
    return a.year == b.year && a.month == b.month && a.day == b.day;
  }

  List<DateTime> _buildCalendarDays(DateTime month) {
    final firstDayOfMonth = DateTime(month.year, month.month, 1);
    final lastDayOfMonth = DateTime(month.year, month.month + 1, 0);

    final start = firstDayOfMonth.subtract(
      Duration(days: firstDayOfMonth.weekday - 1),
    );

    final end = lastDayOfMonth.add(
      Duration(days: DateTime.daysPerWeek - lastDayOfMonth.weekday),
    );

    final totalDays = end.difference(start).inDays + 1;
    final weeks = (totalDays / 7).ceil();
    final visibleDays = weeks * 7;

    return List.generate(
      visibleDays,
      (index) => start.add(Duration(days: index)),
    );
  }

  List<FeedItem> _currentContextMoments() {
    final profile = _profile;
    if (profile == null) return const [];

    if (_selectedTabIndex == 0) {
      return _allMoments;
    }

    return _allMoments.where((item) => item.authorId == profile.id).toList();
  }

  List<FeedItem> _momentsForMonth(DateTime month) {
    final moments = _currentContextMoments();

    return moments.where((item) {
      final local = item.createdAt.toLocal();
      return local.year == month.year && local.month == month.month;
    }).toList()
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
  }

  List<FeedItem> _momentsForDate(DateTime date) {
    final moments = _currentContextMoments();

    return moments.where((item) {
      final local = item.createdAt.toLocal();
      return _isSameDate(local, date);
    }).toList()
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
  }

  List<FeedItem> _ownMomentsForMonth(DateTime month) {
    final profile = _profile;
    if (profile == null) return const [];

    return _allMoments.where((item) {
      final local = item.createdAt.toLocal();
      return item.authorId == profile.id &&
          local.year == month.year &&
          local.month == month.month;
    }).toList()
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
  }

  int _monthTotalCount() => _momentsForMonth(_displayedMonth).length;

  Future<void> _openSystemMemory(SystemMemory memory) async {
    try {
      final freshMemory = await _systemMemoriesApi.getById(memory.id);

      if (freshMemory.items.isEmpty && !freshMemory.hasGeneratedVideo) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('В этом воспоминании пока нет доступных медиа'),
          ),
        );
        return;
      }

      await _systemMemoriesApi.markViewed(freshMemory.id);

      if (!mounted) return;
      await Navigator.of(context).push(
        MaterialPageRoute(
          builder: (_) => _SystemMemoryViewerPage(memory: freshMemory),
        ),
      );

      if (!mounted) return;
      await _load(silent: true);
    } catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось открыть воспоминание. Попробуйте ещё раз.',
            ),
          ),
        ),
      );
    }
  }

  int _monthOwnCount() => _ownMomentsForMonth(_displayedMonth).length;

  int _monthActiveDaysCount() {
    final days = _momentsForMonth(_displayedMonth)
        .map((item) {
          final local = item.createdAt.toLocal();
          return '${local.year}-${local.month}-${local.day}';
        })
        .toSet();

    return days.length;
  }

  FeedItem? _previewForDate(DateTime date) {
    final items = _momentsForDate(date);
    if (items.isEmpty) return null;
    return items.first;
  }

  Future<void> _handleDateTap(DateTime date) async {
    _selectDate(date);

    final items = _momentsForDate(date);
    if (items.isEmpty) return;

    await _openViewerForDate(date);
  }

  Future<void> _openViewerForDate(DateTime date, {int initialDayIndex = 0}) async {
    final dayItems = _momentsForDate(date);
    if (dayItems.isEmpty) return;

    final all = List<FeedItem>.from(_currentContextMoments())
      ..sort((a, b) => a.createdAt.compareTo(b.createdAt));

    final safeIndex = initialDayIndex.clamp(0, dayItems.length - 1);
    final selectedPhotoId = dayItems[safeIndex].photoId;
    final index = all.indexWhere((i) => i.photoId == selectedPhotoId);
    if (index == -1) return;

    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => _MemoryViewerPage(
          items: all,
          initialIndex: index,
        ),
      ),
    );

    if (!mounted) return;
    await _load(silent: true);
  }

  String _monthLabel(DateTime value) {
    const months = [
      'Январь',
      'Февраль',
      'Март',
      'Апрель',
      'Май',
      'Июнь',
      'Июль',
      'Август',
      'Сентябрь',
      'Октябрь',
      'Ноябрь',
      'Декабрь',
    ];

    return '${months[value.month - 1]} ${value.year}';
  }

  @override
  Widget build(BuildContext context) {
    final days = _buildCalendarDays(_displayedMonth);
    final groups = _activeGroupController.groups;
    final activeGroup = _activeGroupController.activeGroup;

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
        body: DecoratedBox(
          decoration: const BoxDecoration(
            gradient: AppColors.pageBackgroundGradient,
          ),
          child: SafeArea(
            child: Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: InMomentSurface(
                  tone: InMomentSurfaceTone.base,
                  borderRadius: BorderRadius.circular(24),
                  padding: const EdgeInsets.all(20),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(
                        Icons.error_outline_rounded,
                        color: AppColors.textSecondary,
                        size: 42,
                      ),
                      const SizedBox(height: 14),
                      Text(
                        'Не удалось загрузить воспоминания.\n\n$_error',
                        textAlign: TextAlign.center,
                        style: const TextStyle(
                          color: AppColors.textPrimary,
                          height: 1.42,
                        ),
                      ),
                      const SizedBox(height: 16),
                      FilledButton(
                        onPressed: _loading || _silentRefreshing ? null : _load,
                        child: _loading || _silentRefreshing
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
          ),
        ),
      );
    }

    return Scaffold(
      backgroundColor: AppColors.background,
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: AppColors.pageBackgroundGradient,
        ),
        child: SafeArea(
          bottom: false,
          child: InMomentResponsiveContent(
            child: RefreshIndicator(
              onRefresh: () async {
                await _load(silent: true);
              },
              child: ListView(
              physics: const BouncingScrollPhysics(
                parent: AlwaysScrollableScrollPhysics(),
              ),
              padding: const EdgeInsets.fromLTRB(8, 10, 8, 136),
              children: [
                Row(
                  children: [
                    const SizedBox(width: 36),
                    Expanded(
                      child: Text(
                        'Воспоминания',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          color: AppColors.textPrimary,
                          fontSize: 18,
                          fontWeight: FontWeight.w900,
                        ),
                      ),
                    ),
                    SizedBox(
                      width: 36,
                      child: _silentRefreshing
                          ? const Padding(
                              padding: EdgeInsets.all(8),
                              child: SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              ),
                            )
                          : IconButton(
                              onPressed: _loading || _silentRefreshing
                                  ? null
                                  : () => _load(silent: true),
                              icon: const Icon(Icons.refresh_rounded),
                              color: AppColors.textPrimary,
                              iconSize: 24,
                            ),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Center(
                  child: SizedBox(
                    width: InMomentMediaFrame.resolveTabletContentWidth(
                      MediaQuery.sizeOf(context).width,
                    ),
                    child: _MemoriesModeSwitch(
                      selectedTabIndex: _selectedTabIndex,
                      onChanged: (index) {
                        setState(() {
                          _selectedTabIndex = index;
                        });
                      },
                    ),
                  ),
                ),
                const SizedBox(height: 8),
                Center(
                  child: SizedBox(
                    width: InMomentMediaFrame.resolveTabletContentWidth(
                      MediaQuery.sizeOf(context).width,
                    ),
                    child: GroupDropdownSelector(
                      groups: groups,
                      selectedGroupId: activeGroup?.id,
                      hintText: 'Группа',
                      enabled: groups.isNotEmpty,
                      isLoading: false,
                      height: 38,
                      borderRadius: 18,
                      avatarRadius: 12,
                      fontSize: 14,
                      onChanged: groups.isEmpty
                          ? null
                          : (groupId) {
                              final selected = groups.cast<Group?>().firstWhere(
                                    (group) => group?.id == groupId,
                                    orElse: () => null,
                                  );
                              _changeActiveGroup(selected);
                            },
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                Builder(
                  builder: (context) {
                    final frame = InMomentMediaFrame.resolveHomeSquare(
                      viewportWidth: MediaQuery.sizeOf(context).width,
                      viewportHeight: MediaQuery.sizeOf(context).height,
                    );

                    return Center(
                      child: SizedBox(
                        width: frame.width,
                        height: frame.height,
                        child: InMomentSurface(
                          tone: InMomentSurfaceTone.elevated,
                          borderRadius: BorderRadius.circular(24),
                          padding: const EdgeInsets.fromLTRB(10, 10, 10, 10),
                          child: Column(
                            children: [
                              _CalendarMonthControl(
                                monthLabel: _monthLabel(_displayedMonth),
                                onPrevious: _goToPreviousMonth,
                                onNext: _goToNextMonth,
                              ),
                              const SizedBox(height: 8),
                              Expanded(
                                child: _CalendarCard(
                                  width: frame.width - 20,
                                  days: days,
                                  displayedMonth: _displayedMonth,
                                  selectedDate: _selectedDate,
                                  isSameDate: _isSameDate,
                                  previewForDate: _previewForDate,
                                  onTapDate: _handleDateTap,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    );
                  },
                ),
                const SizedBox(height: 10),
                const Text(
                  'Статистика',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 8),
                Center(
                  child: SizedBox(
                    width: InMomentMediaFrame.resolveTabletContentWidth(
                      MediaQuery.sizeOf(context).width,
                    ),
                    child: _MemoriesStatsCard(
                      ownCount: _monthOwnCount(),
                      totalCount: _monthTotalCount(),
                      activeDaysCount: _monthActiveDaysCount(),
                    ),
                  ),
                ),
                const SizedBox(height: 14),
                Center(
                  child: SizedBox(
                    width: InMomentMediaFrame.resolveTabletContentWidth(
                      MediaQuery.sizeOf(context).width,
                    ),
                    child: _SystemMemoriesSection(
                      memories: _systemMemories,
                      onOpen: _openSystemMemory,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
        ),
      ),
    );
  }
}

class _CalendarMonthControl extends StatelessWidget {
  final String monthLabel;
  final VoidCallback onPrevious;
  final VoidCallback onNext;

  const _CalendarMonthControl({
    required this.monthLabel,
    required this.onPrevious,
    required this.onNext,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        _MonthIconButton(
          icon: Icons.chevron_left_rounded,
          onTap: onPrevious,
        ),
        Expanded(
          child: Text(
            monthLabel,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 14,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        _MonthIconButton(
          icon: Icons.chevron_right_rounded,
          onTap: onNext,
        ),
      ],
    );
  }
}

class _MonthIconButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onTap;

  const _MonthIconButton({
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: InMomentSurfaceTone.overlay,
      borderRadius: BorderRadius.circular(999),
      onTap: onTap,
      padding: const EdgeInsets.all(8),
      child: Icon(
        icon,
        color: AppColors.textPrimary,
        size: 18,
      ),
    );
  }
}

class _CalendarCard extends StatelessWidget {
  final double width;
  final List<DateTime> days;
  final DateTime displayedMonth;
  final DateTime selectedDate;
  final bool Function(DateTime, DateTime) isSameDate;
  final FeedItem? Function(DateTime) previewForDate;
  final ValueChanged<DateTime> onTapDate;

  const _CalendarCard({
    required this.width,
    required this.days,
    required this.displayedMonth,
    required this.selectedDate,
    required this.isSameDate,
    required this.previewForDate,
    required this.onTapDate,
  });

  @override
  Widget build(BuildContext context) {
    const weekDays = ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'];
    const spacing = 6.0;

    return LayoutBuilder(
      builder: (context, constraints) {
        final rows = (days.length / 7).ceil();
        final gridHeight = constraints.maxHeight - 28;

        final cellWidth = (width - spacing * 6) / 7;
        final cellHeight = (gridHeight - spacing * (rows - 1)) / rows;
        final cellAspectRatio = cellWidth / cellHeight;

        return Column(
          children: [
            Row(
              children: weekDays
                  .map(
                    (day) => Expanded(
                      child: Center(
                        child: Text(
                          day,
                          style: const TextStyle(
                            color: AppColors.textSecondary,
                            fontSize: 10.5,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                    ),
                  )
                  .toList(),
            ),
            const SizedBox(height: 8),
            Expanded(
              child: GridView.builder(
                itemCount: days.length,
                physics: const NeverScrollableScrollPhysics(),
                padding: EdgeInsets.zero,
                gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                  crossAxisCount: 7,
                  mainAxisSpacing: spacing,
                  crossAxisSpacing: spacing,
                  childAspectRatio: cellAspectRatio,
                ),
                itemBuilder: (context, index) {
                  final date = days[index];
                  final inCurrentMonth = date.month == displayedMonth.month;
                  final selected = isSameDate(date, selectedDate);
                  final preview =
                      inCurrentMonth ? previewForDate(date) : null;

                  return InMomentSurface(
                    tone: selected
                        ? InMomentSurfaceTone.elevated
                        : InMomentSurfaceTone.overlay,
                    borderRadius: BorderRadius.circular(10),
                    onTap: () => onTapDate(date),
                    padding: EdgeInsets.zero,
                    child: Stack(
                      fit: StackFit.expand,
                      children: [
                        if (preview != null)
                          ClipRRect(
                            borderRadius: BorderRadius.circular(10),
                            child: NetworkVisualMedia(
                              url: preview.url,
                              contentType: preview.contentType,
                              allowInlineVideo: false,
                              fit: BoxFit.cover,
                              placeholderLabel: 'Медиа',
                              showVideoBadge: true,
                            ),
                          ),
                        DecoratedBox(
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(10),
                            gradient: LinearGradient(
                              begin: Alignment.topCenter,
                              end: Alignment.bottomCenter,
                              colors: [
                                Colors.black.withValues(alpha: 0.05),
                                Colors.transparent,
                                Colors.black.withValues(alpha: 0.18),
                              ],
                            ),
                          ),
                        ),
                        Padding(
                          padding: const EdgeInsets.fromLTRB(4, 4, 4, 4),
                          child: Align(
                            alignment: Alignment.topLeft,
                            child: Text(
                              '${date.day}',
                              style: TextStyle(
                                color: inCurrentMonth
                                    ? AppColors.textPrimary
                                    : AppColors.textSecondary
                                        .withValues(alpha: 0.40),
                                fontWeight: FontWeight.w800,
                                fontSize: 11,
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                  );
                },
              ),
            ),
          ],
        );
      },
    );
  }
}

class _MemoriesModeSwitch extends StatelessWidget {
  final int selectedTabIndex;
  final ValueChanged<int> onChanged;

  const _MemoriesModeSwitch({
    required this.selectedTabIndex,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: _SegmentButton(
            title: 'Группы',
            selected: selectedTabIndex == 0,
            onTap: () => onChanged(0),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _SegmentButton(
            title: 'Личные',
            selected: selectedTabIndex == 1,
            onTap: () => onChanged(1),
          ),
        ),
      ],
    );
  }
}

class _SegmentButton extends StatelessWidget {
  final String title;
  final bool selected;
  final VoidCallback onTap;

  const _SegmentButton({
    required this.title,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: selected
          ? InMomentSurfaceTone.elevated
          : InMomentSurfaceTone.overlay,
      borderRadius: BorderRadius.circular(18),
      onTap: onTap,
      padding: const EdgeInsets.symmetric(vertical: 10),
      child: Center(
        child: Text(
          title,
          style: TextStyle(
            color: selected ? AppColors.textPrimary : AppColors.textSecondary,
            fontWeight: FontWeight.w800,
            fontSize: 13,
          ),
        ),
      ),
    );
  }
}

class _MemoriesStatsCard extends StatelessWidget {
  final int ownCount;
  final int totalCount;
  final int activeDaysCount;

  const _MemoriesStatsCard({
    required this.ownCount,
    required this.totalCount,
    required this.activeDaysCount,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: _StatTile(
            value: '$ownCount',
            title: 'Своих',
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _StatTile(
            value: '$totalCount',
            title: 'Всего',
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _StatTile(
            value: '$activeDaysCount',
            title: 'Активных дней',
          ),
        ),
      ],
    );
  }
}

class _StatTile extends StatelessWidget {
  final String value;
  final String title;

  const _StatTile({
    required this.value,
    required this.title,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: InMomentSurfaceTone.overlay,
      borderRadius: BorderRadius.circular(18),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 10),
      child: Column(
        children: [
          Text(
            value,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 16,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            title,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 11,
            ),
          ),
        ],
      ),
    );
  }
}

class _MemoryViewerPage extends StatefulWidget {
  final List<FeedItem> items;
  final int initialIndex;

  const _MemoryViewerPage({
    required this.items,
    required this.initialIndex,
  });

  @override
  State<_MemoryViewerPage> createState() => _MemoryViewerPageState();
}

class _SystemMemoriesSection extends StatelessWidget {
  final List<SystemMemory> memories;
  final ValueChanged<SystemMemory> onOpen;

  const _SystemMemoriesSection({
    required this.memories,
    required this.onOpen,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: InMomentSurfaceTone.elevated,
      borderRadius: BorderRadius.circular(24),
      padding: const EdgeInsets.fromLTRB(14, 14, 14, 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: const [
              Icon(
                Icons.auto_awesome_rounded,
                color: AppColors.textPrimary,
                size: 18,
              ),
              SizedBox(width: 8),
              Expanded(
                child: Text(
                  'Собранные воспоминания',
                  style: TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 16,
                    fontWeight: FontWeight.w900,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          if (memories.isEmpty)
            const Text(
              'Когда InMoment соберёт для вас подборку за 3, 6 или 12 месяцев, она появится здесь.',
              style: TextStyle(
                color: AppColors.textSecondary,
                fontSize: 12,
                height: 1.35,
              ),
            )
          else
            LayoutBuilder(
              builder: (context, constraints) {
                const visibleCards = 3;
                const spacing = 8.0;
                

                final availableWidth = constraints.maxWidth.isFinite
                    ? constraints.maxWidth
                    : MediaQuery.sizeOf(context).width - 60;

                final rawTileWidth =
                    (availableWidth - spacing * (visibleCards - 1)) /
                        visibleCards;

                final tileWidth = rawTileWidth.clamp(92.0, 140.0).toDouble();
                final tileHeight = (tileWidth * 1.22).clamp(128.0, 170.0);

                return SizedBox(
                  height: tileHeight,
                  child: ListView.separated(
                    scrollDirection: Axis.horizontal,
                    physics: const BouncingScrollPhysics(),
                    itemCount: memories.length,
                    separatorBuilder: (_, _) => const SizedBox(width: spacing),
                    itemBuilder: (context, index) {
                      final memory = memories[index];

                      return _SystemMemoryTile(
                        width: tileWidth,
                        memory: memory,
                        onTap: () => onOpen(memory),
                      );
                    },
                  ),
                );
              },
            ),
        ],
      ),
    );
  }
}

class _SystemMemoryTile extends StatefulWidget {
  final double width;
  final SystemMemory memory;
  final VoidCallback onTap;

  const _SystemMemoryTile({
    required this.width,
    required this.memory,
    required this.onTap,
  });

  @override
  State<_SystemMemoryTile> createState() => _SystemMemoryTileState();
}

class _SystemMemoryTileState extends State<_SystemMemoryTile>
    with SingleTickerProviderStateMixin {
  late final AnimationController _shimmerController;

  @override
  void initState() {
    super.initState();
    _shimmerController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 2200),
    )..repeat();
  }

  @override
  void dispose() {
    _shimmerController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final memory = widget.memory;
    final preview = memory.items.isNotEmpty
        ? memory.items.firstWhere(
            (e) => !isVideoContentType(e.contentType),
            orElse: () => memory.items.first,
          )
        : null;

   return SizedBox(
      width: widget.width,
      child: InMomentSurface(
        tone: InMomentSurfaceTone.overlay,
        borderRadius: BorderRadius.circular(24),
        onTap: widget.onTap,
        padding: EdgeInsets.zero,
        child: ClipRRect(
          borderRadius: BorderRadius.circular(24),
          child: Stack(
            fit: StackFit.expand,
            children: [
              if (preview != null)
                ImageFiltered(
                  imageFilter: ImageFilter.blur(sigmaX: 8, sigmaY: 8),
                  child: Transform.scale(
                    scale: 1.12,
                    child: NetworkVisualMedia(
                      url: preview.url,
                      contentType: preview.contentType,
                      fit: BoxFit.cover,
                      allowInlineVideo: false,
                      showVideoBadge: false,
                      placeholderLabel: 'Воспоминание',
                    ),
                  ),
                )
              else
                const DecoratedBox(
                  decoration: BoxDecoration(
                    gradient: AppColors.pageBackgroundGradient,
                  ),
                ),
              DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: [
                      Colors.black.withValues(alpha: 0.40),
                      Colors.black.withValues(alpha: 0.48),
                      Colors.black.withValues(alpha: 0.84),
                    ],
                  ),
                ),
              ),
              _MemoryTileShimmer(animation: _shimmerController),
              Positioned(
                top: 10,
                right: 10,
                child: Container(
                  width: 34,
                  height: 34,
                  decoration: BoxDecoration(
                    color: AppColors.surfaceGlassStrong(0.72),
                    shape: BoxShape.circle,
                    border: Border.all(
                      color: AppColors.softStroke(0.18),
                    ),
                  ),
                  child: const Icon(
                    Icons.play_arrow_rounded,
                    color: AppColors.textPrimary,
                    size: 23,
                  ),
                ),
              ),
              Positioned(
                left: 10,
                bottom: 12,
                right: 10,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: AppColors.surfaceGlassStrong(0.66),
                        borderRadius: BorderRadius.circular(999),
                        border: Border.all(
                          color: AppColors.softStroke(0.16),
                        ),
                      ),
                      child: Text(
                        '${memory.periodMonths} мес.',
                        style: const TextStyle(
                          color: AppColors.textPrimary,
                          fontSize: 10,
                          fontWeight: FontWeight.w900,
                        ),
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      memory.title,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 13,
                        height: 1.04,
                        fontWeight: FontWeight.w900,
                      ),
                    ),
                    const SizedBox(height: 5),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _MemoryTileShimmer extends StatelessWidget {
  final Animation<double> animation;

  const _MemoryTileShimmer({
    required this.animation,
  });

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: AnimatedBuilder(
        animation: animation,
        builder: (context, _) {
          final value = animation.value;
          final start = -1.0 + value * 2.4;
          final end = start + 0.55;

          return DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment(start, -1),
                end: Alignment(end, 1),
                colors: [
                  Colors.transparent,
                  Colors.white.withValues(alpha: 0.04),
                  AppColors.accentSoft.withValues(alpha: 0.16),
                  Colors.white.withValues(alpha: 0.05),
                  Colors.transparent,
                ],
                stops: const [0.0, 0.36, 0.5, 0.64, 1.0],
              ),
            ),
          );
        },
      ),
    );
  }
}

class _SystemMemoryViewerPage extends StatelessWidget {
  final SystemMemory memory;

  const _SystemMemoryViewerPage({required this.memory});

  @override
  Widget build(BuildContext context) {
    final hasItems = memory.items.isNotEmpty;
    final hasGeneratedVideo = memory.hasGeneratedVideo;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Container(
        decoration: const BoxDecoration(
          gradient: AppColors.pageBackgroundGradient,
        ),
        child: SafeArea(
          bottom: false,
          child: Center(
            child: SizedBox(
              width: InMomentMediaFrame.resolveMediaViewerWidth(
                MediaQuery.sizeOf(context).width,
              ),
              child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 12),
                child: Row(
                  children: [
                    _ViewerCircleButton(
                      icon: Icons.arrow_back_ios_new_rounded,
                      onTap: () => Navigator.of(context).pop(),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        children: [
                          Text(
                            memory.title,
                            textAlign: TextAlign.center,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: AppColors.textPrimary,
                              fontSize: 20,
                              fontWeight: FontWeight.w900,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            memory.subtitle,
                            textAlign: TextAlign.center,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: AppColors.textSecondary,
                              fontSize: 13,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 58),
                  ],
                ),
              ),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(16, 0, 16, 18),
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(34),
                    child: DecoratedBox(
                      decoration: BoxDecoration(
                        color: AppColors.surfaceDeep,
                        borderRadius: BorderRadius.circular(34),
                        border: Border.all(
                          color: AppColors.purpleStroke(0.28),
                        ),
                        boxShadow: [
                          BoxShadow(
                            color: AppColors.shadow(0.42),
                            blurRadius: 34,
                            offset: const Offset(0, 18),
                          ),
                        ],
                      ),
                      child: hasGeneratedVideo
                          ? _SystemMemoryGeneratedVideo(
                              url: memory.generatedVideoUrl!,
                            )
                          : hasItems
                              ? _SystemMemoryFallbackPager(memory: memory)
                              : const Center(
                                  child: Padding(
                                    padding:
                                        EdgeInsets.symmetric(horizontal: 28),
                                    child: Text(
                                      'В этом воспоминании пока нет доступных медиа',
                                      textAlign: TextAlign.center,
                                      style: TextStyle(
                                        color: AppColors.textPrimary,
                                        fontSize: 16,
                                        fontWeight: FontWeight.w800,
                                      ),
                                    ),
                                  ),
                                ),
                    ),
                  ),
                ),
              ),
            ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _SystemMemoryGeneratedVideo extends StatefulWidget {
  final String url;

  const _SystemMemoryGeneratedVideo({
    required this.url,
  });

  @override
  State<_SystemMemoryGeneratedVideo> createState() =>
      _SystemMemoryGeneratedVideoState();
}

class _SystemMemoryGeneratedVideoState
    extends State<_SystemMemoryGeneratedVideo> {
  VideoPlayerController? _controller;
  bool _isLoading = true;
  bool _hasError = false;
  bool _muted = true;

  @override
  void initState() {
    super.initState();
    _initController();
  }

  @override
  void didUpdateWidget(covariant _SystemMemoryGeneratedVideo oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.url != widget.url) {
      _disposeController();
      _initController();
    }
  }

  Future<void> _initController() async {
    if (mounted) {
      setState(() {
        _isLoading = true;
        _hasError = false;
        _muted = true;
      });
    }

    try {
      final controller = VideoPlayerController.networkUrl(
        Uri.parse(widget.url),
        videoPlayerOptions: VideoPlayerOptions(mixWithOthers: true),
      );

      await controller.initialize().timeout(
        const Duration(seconds: 12),
      );
      await controller.setLooping(true);
      await controller.setVolume(1);
      controller.addListener(_handleControllerChanged);

      if (!mounted) {
        controller.removeListener(_handleControllerChanged);
        await controller.dispose();
        return;
      }

      setState(() {
        _controller = controller;
        _isLoading = false;
        _hasError = false;
        _muted = false;
      });

      await controller.play();
    } catch (_) {
      if (!mounted) return;

      setState(() {
        _isLoading = false;
        _hasError = true;
      });
    }
  }

  void _handleControllerChanged() {
    if (!mounted) return;

    final controller = _controller;
    if (controller != null && controller.value.hasError) {
      setState(() {
        _hasError = true;
        _isLoading = false;
      });
      return;
    }

    setState(() {});
  }

  Future<void> _toggleMute() async {
    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) return;

    final nextMuted = !_muted;
    await controller.setVolume(nextMuted ? 0 : 1);

    if (!mounted) return;
    setState(() => _muted = nextMuted);
  }

  Future<void> _togglePlayback() async {
    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) return;

    if (controller.value.isPlaying) {
      await controller.pause();
    } else {
      await controller.play();
    }

    if (!mounted) return;
    setState(() {});
  }

  void _disposeController() {
    final controller = _controller;
    _controller = null;

    if (controller != null) {
      try {
        controller.removeListener(_handleControllerChanged);
      } catch (_) {}

      try {
        controller.dispose();
      } catch (_) {}
    }
  }

  @override
  void dispose() {
    _disposeController();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final controller = _controller;

    if (_isLoading) {
      return const Center(
        child: CircularProgressIndicator(color: Colors.white),
      );
    }

    if (_hasError || controller == null || !controller.value.isInitialized) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 28),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(
                Icons.videocam_off_rounded,
                color: Colors.white,
                size: 44,
              ),
              const SizedBox(height: 14),
              Text(
                'Не удалось открыть видео-воспоминание',
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: Colors.white.withValues(alpha: 0.88),
                  fontSize: 16,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 14),
              TextButton.icon(
                onPressed: _initController,
                icon: const Icon(Icons.refresh_rounded),
                label: const Text('Повторить'),
              ),
            ],
          ),
        ),
      );
    }

    final value = controller.value;
    final duration = value.duration;
    final position = value.position;
    final progress = duration.inMilliseconds <= 0
        ? 0.0
        : (position.inMilliseconds / duration.inMilliseconds).clamp(0.0, 1.0);
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: _togglePlayback,
      child: Stack(
        fit: StackFit.expand,
        children: [
          const DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: [
                  AppColors.surfaceDeep,
                  AppColors.background,
                ],
              ),
            ),
          ),
          Positioned.fill(
            child: _MemoryVideoSurface(controller: controller, fit: BoxFit.cover,),
          ),
          Positioned.fill(
            child: IgnorePointer(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.bottomCenter,
                    end: Alignment.topCenter,
                    colors: [
                      Colors.black.withValues(alpha: 0.72),
                      Colors.transparent,
                      Colors.black.withValues(alpha: 0.22),
                    ],
                  ),
                ),
              ),
            ),
          ),
          Positioned(
            right: 18,
            top: 18,
            child: _ViewerCircleButton(
              icon: _muted
                  ? Icons.volume_off_rounded
                  : Icons.volume_up_rounded,
              onTap: _toggleMute,
            ),
          ),
          Center(
            child: AnimatedOpacity(
              opacity: value.isPlaying ? 0 : 1,
              duration: const Duration(milliseconds: 160),
              child: Container(
                width: 76,
                height: 76,
                decoration: BoxDecoration(
                  color: AppColors.surfaceGlassStrong(0.70),
                  shape: BoxShape.circle,
                  border: Border.all(
                    color: AppColors.softStroke(0.22),
                  ),
                  boxShadow: [
                    BoxShadow(
                      color: AppColors.shadow(0.36),
                      blurRadius: 22,
                      offset: const Offset(0, 10),
                    ),
                  ],
                ),
                child: const Icon(
                  Icons.play_arrow_rounded,
                  color: AppColors.textPrimary,
                  size: 46,
                ),
              ),
            ),
          ),
          Positioned(
            left: 18,
            right: 18,
            bottom: 18,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(99),
                  child: LinearProgressIndicator(
                    minHeight: 5,
                    value: progress,
                    color: AppColors.accentSoft,
                    backgroundColor: Colors.white.withValues(alpha: 0.18),
                  ),
                ),
                const SizedBox(height: 12),
                Text(
                  value.isPlaying
                      ? 'Нажмите, чтобы поставить на паузу'
                      : 'Нажмите, чтобы продолжить',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: AppColors.textPrimary.withValues(alpha: 0.74),
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SystemMemoryFallbackPager extends StatefulWidget {
  final SystemMemory memory;

  const _SystemMemoryFallbackPager({required this.memory});

  @override
  State<_SystemMemoryFallbackPager> createState() =>
      _SystemMemoryFallbackPagerState();
}

class _SystemMemoryFallbackPagerState
    extends State<_SystemMemoryFallbackPager> {
  late final PageController _pageController;
  int _index = 0;

  @override
  void initState() {
    super.initState();
    _pageController = PageController(viewportFraction: 0.74);
  }

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final items = widget.memory.items;

    if (items.isEmpty) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.symmetric(horizontal: 28),
          child: Text(
            'В этом воспоминании пока нет доступных медиа',
            textAlign: TextAlign.center,
            style: TextStyle(
              color: Colors.white,
              fontSize: 16,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
      );
    }

    return Stack(
      children: [
        PageView.builder(
          controller: _pageController,
          physics: const BouncingScrollPhysics(),
          itemCount: items.length,
          onPageChanged: (value) => setState(() => _index = value),
          itemBuilder: (context, index) {
            final item = items[index];
            final isVideo = isVideoContentType(item.contentType);

            return Padding(
              padding: const EdgeInsets.fromLTRB(8, 18, 8, 70),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(30),
                child: SizedBox.expand(
                  child: isVideo
                      ? _SystemMemoryItemVideoPlayer(
                          url: item.url,
                          isActive: index == _index,
                        )
                      : NetworkVisualMedia(
                          url: item.url,
                          contentType: item.contentType,
                          fit: BoxFit.cover,
                          allowInlineVideo: false,
                          showVideoBadge: false,
                          placeholderLabel: 'Фото воспоминания',
                        ),
                ),
              ),
            );
          },
        ),
        if (items.length > 1)
          Positioned(
            left: 20,
            right: 20,
            bottom: 26,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: List.generate(
                items.length,
                (index) => AnimatedContainer(
                  duration: const Duration(milliseconds: 160),
                  width: index == _index ? 18 : 6,
                  height: 6,
                  margin: const EdgeInsets.symmetric(horizontal: 3),
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(
                      alpha: index == _index ? 0.95 : 0.32,
                    ),
                    borderRadius: BorderRadius.circular(99),
                  ),
                ),
              ),
            ),
          ),
      ],
    );
  }
}

class _SystemMemoryItemVideoPlayer extends StatefulWidget {
  final String url;
  final bool isActive;

  const _SystemMemoryItemVideoPlayer({
    required this.url,
    required this.isActive,
  });

  @override
  State<_SystemMemoryItemVideoPlayer> createState() =>
      _SystemMemoryItemVideoPlayerState();
}

class _SystemMemoryItemVideoPlayerState
    extends State<_SystemMemoryItemVideoPlayer> {
  VideoPlayerController? _controller;

  bool _loading = false;
  bool _failed = false;
  bool _muted = true;
  bool _initializing = false;

  @override
  void initState() {
    super.initState();

    if (widget.isActive) {
      _init();
    }
  }

  @override
  void didUpdateWidget(covariant _SystemMemoryItemVideoPlayer oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.url != widget.url) {
      _disposeController();
      setState(() {
        _loading = false;
        _failed = false;
        _muted = true;
        _initializing = false;
      });

      if (widget.isActive) {
        _init();
      }

      return;
    }

    if (!oldWidget.isActive && widget.isActive) {
      final controller = _controller;

      if (controller == null || !controller.value.isInitialized) {
        _init();
      } else {
        controller.play();
      }

      return;
    }

    if (oldWidget.isActive && !widget.isActive) {
      final controller = _controller;
      if (controller != null && controller.value.isInitialized) {
        controller.pause();
      }
    }
  }

  Future<void> _init() async {
    if (_initializing) return;

    _initializing = true;

    if (mounted) {
      setState(() {
        _loading = true;
        _failed = false;
        _muted = true;
      });
    }

    try {
      final controller = VideoPlayerController.networkUrl(
        Uri.parse(widget.url),
        videoPlayerOptions: VideoPlayerOptions(mixWithOthers: true),
      );

      await controller.initialize().timeout(
            const Duration(seconds: 12),
          );
      await controller.setLooping(true);
      await controller.setVolume(0);
      controller.addListener(_handleChanged);

      if (!mounted) {
        controller.removeListener(_handleChanged);
        await controller.dispose();
        return;
      }

      setState(() {
        _controller = controller;
        _loading = false;
        _failed = false;
        _muted = true;
        _initializing = false;
      });

      if (widget.isActive) {
        await controller.play();
      }
    } catch (_) {
      if (!mounted) return;

      setState(() {
        _loading = false;
        _failed = true;
        _initializing = false;
      });
    }
  }

  void _handleChanged() {
    if (!mounted) return;

    final controller = _controller;
    if (controller != null && controller.value.hasError) {
      setState(() {
        _failed = true;
        _loading = false;
      });
      return;
    }

    setState(() {});
  }

  Future<void> _togglePlayback() async {
    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) return;

    if (controller.value.isPlaying) {
      await controller.pause();
    } else {
      await controller.play();
    }

    if (!mounted) return;
    setState(() {});
  }

  Future<void> _toggleMute() async {
    final controller = _controller;
    if (controller == null || !controller.value.isInitialized) return;

    final nextMuted = !_muted;
    await controller.setVolume(nextMuted ? 0 : 1);

    if (!mounted) return;
    setState(() => _muted = nextMuted);
  }

  void _disposeController() {
    final controller = _controller;
    _controller = null;

    if (controller != null) {
      try {
        controller.removeListener(_handleChanged);
      } catch (_) {}

      try {
        controller.dispose();
      } catch (_) {}
    }
  }

  @override
  void dispose() {
    _disposeController();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final controller = _controller;

    if (!widget.isActive && (controller == null || !controller.value.isInitialized)) {
      return const _MemoryVideoPlaceholder(
        label: 'Видео',
        loading: false,
      );
    }

    if (_loading) {
      return const _MemoryVideoPlaceholder(
        label: 'Загрузка видео…',
        loading: true,
      );
    }

    if (_failed || controller == null || !controller.value.isInitialized) {
      return ColoredBox(
        color: Colors.black,
        child: Center(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 22),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(
                  Icons.videocam_off_rounded,
                  color: Colors.white.withValues(alpha: 0.82),
                  size: 42,
                ),
                const SizedBox(height: 12),
                Text(
                  'Не удалось загрузить видео',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: Colors.white.withValues(alpha: 0.78),
                    fontSize: 13,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 10),
                TextButton.icon(
                  onPressed: _init,
                  icon: const Icon(Icons.refresh_rounded),
                  label: const Text('Повторить'),
                ),
              ],
            ),
          ),
        ),
      );
    }

    final value = controller.value;

    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: _togglePlayback,
      child: Stack(
        fit: StackFit.expand,
        children: [
          const ColoredBox(color: Colors.black),
          Positioned.fill(
            child: _MemoryVideoSurface(controller: controller),
          ),
          Positioned.fill(
            child: IgnorePointer(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.bottomCenter,
                    end: Alignment.topCenter,
                    colors: [
                      Colors.black.withValues(alpha: 0.48),
                      Colors.transparent,
                      Colors.black.withValues(alpha: 0.16),
                    ],
                  ),
                ),
              ),
            ),
          ),
          Positioned(
            top: 14,
            right: 14,
            child: GestureDetector(
              onTap: _toggleMute,
              child: Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: Colors.black.withValues(alpha: 0.52),
                  shape: BoxShape.circle,
                  border: Border.all(
                    color: Colors.white.withValues(alpha: 0.14),
                  ),
                ),
                child: Icon(
                  _muted ? Icons.volume_off_rounded : Icons.volume_up_rounded,
                  color: Colors.white,
                  size: 20,
                ),
              ),
            ),
          ),
          Center(
            child: AnimatedOpacity(
              opacity: value.isPlaying ? 0 : 1,
              duration: const Duration(milliseconds: 160),
              child: Container(
                width: 72,
                height: 72,
                decoration: BoxDecoration(
                  color: Colors.black.withValues(alpha: 0.46),
                  shape: BoxShape.circle,
                  border: Border.all(
                    color: Colors.white.withValues(alpha: 0.18),
                  ),
                ),
                child: const Icon(
                  Icons.play_arrow_rounded,
                  color: Colors.white,
                  size: 42,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _MemoryVideoPlaceholder extends StatelessWidget {
  final String label;
  final bool loading;

  const _MemoryVideoPlaceholder({
    required this.label,
    required this.loading,
  });

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: Colors.black,
      child: Stack(
        fit: StackFit.expand,
        children: [
          DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [
                  AppColors.surfaceDeep,
                  AppColors.card.withValues(alpha: 0.76),
                  Colors.black,
                ],
              ),
            ),
          ),
          Center(
            child: loading
                ? const CircularProgressIndicator(color: Colors.white)
                : Container(
                    width: 72,
                    height: 72,
                    decoration: BoxDecoration(
                      color: Colors.black.withValues(alpha: 0.46),
                      shape: BoxShape.circle,
                      border: Border.all(
                        color: Colors.white.withValues(alpha: 0.18),
                      ),
                    ),
                    child: const Icon(
                      Icons.play_arrow_rounded,
                      color: Colors.white,
                      size: 42,
                    ),
                  ),
          ),
          Positioned(
            left: 14,
            right: 14,
            bottom: 14,
            child: Text(
              label,
              textAlign: TextAlign.center,
              style: TextStyle(
                color: Colors.white.withValues(alpha: 0.74),
                fontSize: 12,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _MemoryVideoSurface extends StatelessWidget {
  final VideoPlayerController controller;
  final BoxFit fit;

  const _MemoryVideoSurface({
    required this.controller,
    this.fit = BoxFit.contain,
  });

  @override
  Widget build(BuildContext context) {
    final value = controller.value;
    final size = value.size;

    final rawAspectRatio = value.aspectRatio;
    final safeAspectRatio = _safeVideoAspectRatio(rawAspectRatio);

    final videoWidth = size.width > 0 ? size.width : safeAspectRatio * 1000;
    final videoHeight = size.height > 0 ? size.height : 1000.0;

    return ColoredBox(
      color: Colors.black,
      child: Center(
        child: FittedBox(
          fit: fit,
          child: SizedBox(
            width: videoWidth,
            height: videoHeight,
            child: VideoPlayer(controller),
          ),
        ),
      ),
    );
  }
}

class _MemoryMediaFrame extends StatelessWidget {
  final FeedItem item;
  final bool isActive;

  const _MemoryMediaFrame({
    required this.item,
    required this.isActive,
  });

  @override
  Widget build(BuildContext context) {
    final isVideo = isVideoContentType(item.contentType);

    return LayoutBuilder(
      builder: (context, constraints) {
        final maxWidth = constraints.maxWidth;
        final maxHeight = constraints.maxHeight;

        if (isVideo) {
          return SizedBox(
            width: maxWidth,
            height: maxHeight,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(32),
              child: isActive
                  ? _SystemMemoryItemVideoPlayer(
                      url: item.url,
                      isActive: true,
                    )
                  : const _MemoryVideoPlaceholder(
                      label: 'Видео',
                      loading: false,
                    ),
            ),
          );
        }

        return ClipRRect(
          borderRadius: BorderRadius.circular(32),
          child: ConstrainedBox(
            constraints: BoxConstraints(
              maxWidth: maxWidth,
              maxHeight: maxHeight,
            ),
            child: Image.network(
              item.url,
              fit: BoxFit.contain,
              alignment: Alignment.center,
            ),
          ),
        );
      },
    );
  }
}

double _safeVideoAspectRatio(double value) {
  if (!value.isFinite || value <= 0) {
    return 9 / 16;
  }

  return value.clamp(9 / 21, 21 / 9).toDouble();
}

class _MemoryViewerPageState extends State<_MemoryViewerPage> {
  late final PageController _pageController;
  late final ScrollController _thumbController;

  final Dio _dio = Dio();

  late int _currentIndex;
  bool _downloading = false;

  @override
  void initState() {
    super.initState();

    _currentIndex = widget.initialIndex;

    _pageController = PageController(
      initialPage: _currentIndex,
      viewportFraction: 0.86,
    );

    _thumbController = ScrollController();

    WidgetsBinding.instance.addPostFrameCallback((_) {
      _scrollToCurrentThumb(animated: false);
    });
  }

  @override
  void dispose() {
    _pageController.dispose();
    _thumbController.dispose();
    super.dispose();
  }

  FeedItem get _currentItem => widget.items[_currentIndex];

  void _scrollToCurrentThumb({bool animated = true}) {
    if (!_thumbController.hasClients) return;

    const itemWidth = 84.0;
    const spacing = 12.0;
    const horizontalPadding = 24.0;

    final viewportWidth = _thumbController.position.viewportDimension;
    final itemCenter = horizontalPadding +
        (_currentIndex * (itemWidth + spacing)) +
        (itemWidth / 2);

    final target = (itemCenter - viewportWidth / 2)
        .clamp(0.0, _thumbController.position.maxScrollExtent)
        .toDouble();

    if (animated) {
      _thumbController.animateTo(
        target,
        duration: const Duration(milliseconds: 250),
        curve: Curves.easeOut,
      );
    } else {
      _thumbController.jumpTo(target);
    }
  }

  Future<void> _downloadCurrent() async {
    if (_downloading) return;

    setState(() {
      _downloading = true;
    });

    try {
      final response = await _dio.get<List<int>>(
        _currentItem.url,
        options: Options(responseType: ResponseType.bytes),
      );

      final bytes = response.data;
      if (bytes == null || bytes.isEmpty) {
        throw Exception('Не удалось получить медиа');
      }

      final ext = _fileExtension(_currentItem.contentType);
      final fileName = 'inmoment_${_currentItem.photoId}.$ext';

      await Share.shareXFiles(
        [
          XFile.fromData(
            Uint8List.fromList(bytes),
            name: fileName,
            mimeType: _currentItem.contentType,
          ),
        ],
      );
    } catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось поделиться медиа. Попробуйте ещё раз.',
            ),
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _downloading = false;
        });
      }
    }
  }

  String _fileExtension(String contentType) {
    final normalized = contentType.toLowerCase();

    switch (normalized) {
      case 'image/png':
        return 'png';
      case 'image/webp':
        return 'webp';
      case 'image/heic':
        return 'heic';
      case 'image/heif':
        return 'heif';
      case 'video/mp4':
        return 'mp4';
      case 'video/quicktime':
        return 'mov';
      case 'video/x-m4v':
        return 'm4v';
      case 'video/webm':
        return 'webm';
      case 'video/3gpp':
        return '3gp';
      default:
        return 'jpg';
    }
  }

  String _yearLabel(DateTime value) => '${value.toLocal().year}';

  String _dayMonthLabel(DateTime value) {
    final local = value.toLocal();

    const months = [
      'Января',
      'Февраля',
      'Марта',
      'Апреля',
      'Мая',
      'Июня',
      'Июля',
      'Августа',
      'Сентября',
      'Октября',
      'Ноября',
      'Декабря',
    ];

    return '${local.day} ${months[local.month - 1]}';
  }

  @override
  Widget build(BuildContext context) {
    final currentDate = _currentItem.createdAt;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Center(
          child: SizedBox(
            width: InMomentMediaFrame.resolveMediaViewerWidth(
              MediaQuery.sizeOf(context).width,
            ),
            child: Stack(
          children: [
            PageView.builder(
              controller: _pageController,
              itemCount: widget.items.length,
              onPageChanged: (index) {
                setState(() {
                  _currentIndex = index;
                });

                _scrollToCurrentThumb();
              },
              itemBuilder: (context, index) {
                final item = widget.items[index];
                final isCurrent = index == _currentIndex;

                return AnimatedPadding(
                  duration: const Duration(milliseconds: 220),
                  curve: Curves.easeOut,
                  padding: EdgeInsets.symmetric(
                    horizontal: isCurrent ? 4 : 10,
                    vertical: isCurrent ? 72 : 118,
                  ),
                    child: Center(
                    child: _MemoryMediaFrame(
                      item: item,
                      isActive: isCurrent,
                    ),
                  ),
                );
              },
            ),
            Positioned(
              left: 16,
              top: 8,
              child: _ViewerCircleButton(
                icon: Icons.arrow_back_ios_new_rounded,
                onTap: () => Navigator.of(context).pop(),
              ),
            ),
            Positioned(
              right: 16,
              top: 8,
              child: _ViewerCircleButton(
                icon: _downloading ? Icons.hourglass_top : Icons.download,
                onTap: _downloading ? null : _downloadCurrent,
              ),
            ),
            Positioned(
              top: 10,
              left: 70,
              right: 70,
              child: Column(
                children: [
                  Text(
                    _yearLabel(currentDate),
                    style: const TextStyle(
                      color: AppColors.textPrimary,
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    _dayMonthLabel(currentDate),
                    style: const TextStyle(
                      color: AppColors.textPrimary,
                      fontSize: 24,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                ],
              ),
            ),
            Positioned(
              left: 0,
              right: 0,
              bottom: 10,
              child: SizedBox(
                height: 84,
                child: ListView.separated(
                  controller: _thumbController,
                  scrollDirection: Axis.horizontal,
                  itemCount: widget.items.length,
                  padding: const EdgeInsets.symmetric(horizontal: 24),
                  separatorBuilder: (_, _) => const SizedBox(width: 12),
                  itemBuilder: (context, index) {
                    final item = widget.items[index];
                    final distance = (index - _currentIndex).abs();
                    final selected = distance == 0;

                    double scale;
                    if (distance == 0) {
                      scale = 1.0;
                    } else if (distance == 1) {
                      scale = 0.90;
                    } else if (distance == 2) {
                      scale = 0.82;
                    } else {
                      scale = 0.76;
                    }
                    return GestureDetector(
                      onTap: () {
                        _pageController.animateToPage(
                          index,
                          duration: const Duration(milliseconds: 220),
                          curve: Curves.easeOut,
                        );
                      },
                      child: AnimatedScale(
                        scale: scale,
                        duration: const Duration(milliseconds: 220),
                        curve: Curves.easeOut,
                        child: AnimatedContainer(
                          duration: const Duration(milliseconds: 220),
                          width: 84,
                          height: 84,
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(18),
                            border: Border.all(
                              color: selected
                                  ? AppColors.accentLight
                                  : Colors.white.withValues(alpha: 0.12),
                              width: selected ? 2 : 1,
                            ),
                          ),
                          child: ClipRRect(
                            borderRadius: BorderRadius.circular(18),
                            child: NetworkVisualMedia(
                              url: item.url,
                              contentType: item.contentType,
                              allowInlineVideo: false,
                              fit: BoxFit.cover,
                              placeholderLabel: 'Медиа',
                              showVideoBadge: true,
                            ),
                          ),
                        ),
                      ),
                    );
                  },
                ),
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

class _ViewerCircleButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onTap;

  const _ViewerCircleButton({
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.black.withValues(alpha: 0.24),
      shape: const CircleBorder(),
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: SizedBox(
          width: 46,
          height: 46,
          child: Icon(
            icon,
            color: AppColors.textPrimary,
          ),
        ),
      ),
    );
  }
}