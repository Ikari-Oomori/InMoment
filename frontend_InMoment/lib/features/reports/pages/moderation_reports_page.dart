import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_responsive_content.dart';
import '../../../core/widgets/inmoment_dialog_wrapper.dart';
import '../api/reports_api.dart';
import '../models/report_item.dart';
import '../models/report_reason_option.dart';
import 'report_details_page.dart';
import '../../../core/layout/inmoment_media_frame.dart';

class ModerationReportsPage extends StatefulWidget {
  const ModerationReportsPage({super.key});

  @override
  State<ModerationReportsPage> createState() => _ModerationReportsPageState();
}

enum _ModerationTab {
  pending,
  appeals,
  archive,
}

class _ModerationReportsPageState extends State<ModerationReportsPage> {
  final ReportsApi _api = ReportsApi();

  bool _loading = true;
  String? _error;
  String? _processingReportId;
  List<ReportItem> _items = const [];

  _ModerationTab _tab = _ModerationTab.pending;

  ReportStatusOption? _statusFilter;
  ReportTargetType? _targetTypeFilter;
  ReportReasonOption? _reasonFilter;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load({bool silent = false}) async {
    if (!silent) {
      setState(() {
        _loading = true;
        _error = null;
      });
    }

    try {
      final items = await _api.getAllReports(
        status: _statusFilter?.value,
        targetType: _targetTypeFilter?.value,
        reason: _reasonFilter?.value,
      );

      if (!mounted) return;
      setState(() {
        _items = items;
        _loading = false;
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить жалобы.',
        );
      });
    }
  }

  String _formatDate(DateTime value) {
    final local = value.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');
    return '${two(local.day)}.${two(local.month)}.${local.year} ${two(local.hour)}:${two(local.minute)}';
  }

  String _targetTitle(ReportItem item) {
    switch (item.targetType) {
      case ReportTargetType.photo:
        return 'Жалоба на публикацию';
      case ReportTargetType.comment:
        return 'Жалоба на комментарий';
      case ReportTargetType.user:
        return 'Жалоба на пользователя';
    }
  }

  bool _hasAppeal(ReportItem item) =>
      (item.resolution.appealText ?? '').trim().isNotEmpty;

  bool _isArchive(ReportItem item) =>
      item.status == ReportStatusOption.rejected ||
      item.status == ReportStatusOption.resolved ||
      item.status == ReportStatusOption.reviewed;

  List<ReportItem> get _visibleItems {
    final filtered = _items.where((item) {
      switch (_tab) {
        case _ModerationTab.pending:
          return item.status == ReportStatusOption.pending && !_hasAppeal(item);
        case _ModerationTab.appeals:
          return item.status == ReportStatusOption.pending && _hasAppeal(item);
        case _ModerationTab.archive:
          return _isArchive(item);
      }
    }).toList();

    filtered.sort((a, b) {
      final aAppeal = _hasAppeal(a) ? 1 : 0;
      final bAppeal = _hasAppeal(b) ? 1 : 0;

      if (aAppeal != bAppeal) {
        return bAppeal.compareTo(aAppeal);
      }

      return b.createdAt.millisecondsSinceEpoch.compareTo(
        a.createdAt.millisecondsSinceEpoch,
      );
    });

    return filtered;
  }

  int get _pendingCount => _items
      .where((x) => x.status == ReportStatusOption.pending && !_hasAppeal(x))
      .length;

  int get _appealsCount => _items
      .where((x) => x.status == ReportStatusOption.pending && _hasAppeal(x))
      .length;

  int get _archiveCount => _items.where(_isArchive).length;

  Future<void> _openFilterSheet() async {
    final result = await showModalBottomSheet<_FilterResult>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) {
        return Center(
          child: SizedBox(
            width: InMomentMediaFrame.resolveBottomSheetWidth(
              MediaQuery.sizeOf(context).width,
            ),
            child: _ModerationFilterSheet(
              initialStatus: _statusFilter,
              initialTargetType: _targetTypeFilter,
              initialReason: _reasonFilter,
            ),
          ),
        );
      },
    );

    if (result == null) return;

    setState(() {
      _statusFilter = result.status;
      _targetTypeFilter = result.targetType;
      _reasonFilter = result.reason;
    });

    await _load(silent: true);
  }

  Future<void> _resetFilters() async {
    setState(() {
      _statusFilter = null;
      _targetTypeFilter = null;
      _reasonFilter = null;
    });

    await _load(silent: true);
  }

  Future<void> _applyDecision({
    required ReportItem item,
    required ReportStatusOption status,
    required ReviewReportActionOption action,
    required String confirmText,
  }) async {
    final confirmed = await showDialog<bool>(
          context: context,
          builder: (context) {
            return InMomentDialogWrapper(
              child: AlertDialog(
                backgroundColor: AppColors.card,
                title: const Text(
                  'Подтвердить действие',
                  style: TextStyle(color: AppColors.textPrimary),
                ),
                content: Text(
                  confirmText,
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    height: 1.45,
                  ),
                ),
                actions: [
                  TextButton(
                    onPressed: () => Navigator.of(context).pop(false),
                    child: const Text('Отмена'),
                  ),
                  FilledButton(
                    onPressed: () => Navigator.of(context).pop(true),
                    child: const Text('Подтвердить'),
                  ),
                ],
              ),
            );
          },
        ) ??
        false;

    if (!confirmed) return;

    setState(() {
      _processingReportId = item.id;
    });

    try {
      await _api.reviewReport(
        reportId: item.id,
        status: status,
        action: action,
      );

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Решение по жалобе сохранено')),
      );

      await _load(silent: true);
    } catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось выполнить действие с жалобой.',
            ),
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _processingReportId = null;
        });
      }
    }
  }

  List<_DecisionAction> _actionsFor(ReportItem item) {
    if (item.status != ReportStatusOption.pending) {
      return const [];
    }

    final common = <_DecisionAction>[
      _DecisionAction(
        label: 'Отклонить',
        status: ReportStatusOption.rejected,
        action: ReviewReportActionOption.none,
        confirmText: 'Жалоба будет отклонена без модерационного действия.',
      ),
      _DecisionAction(
        label: 'Просмотрена',
        status: ReportStatusOption.reviewed,
        action: ReviewReportActionOption.none,
        confirmText: 'Жалоба будет отмечена как просмотренная.',
      ),
      _DecisionAction(
        label: 'Решить без действия',
        status: ReportStatusOption.resolved,
        action: ReviewReportActionOption.none,
        confirmText:
            'Жалоба будет закрыта без удаления контента или деактивации пользователя.',
      ),
    ];

    switch (item.targetType) {
      case ReportTargetType.photo:
        return [
          ...common,
          _DecisionAction(
            label: 'Удалить фото',
            status: ReportStatusOption.resolved,
            action: ReviewReportActionOption.deletePhoto,
            confirmText: 'Фото будет удалено модератором, а жалоба будет закрыта.',
          ),
        ];
      case ReportTargetType.comment:
        return [
          ...common,
          _DecisionAction(
            label: 'Удалить комментарий',
            status: ReportStatusOption.resolved,
            action: ReviewReportActionOption.deleteComment,
            confirmText: 'Комментарий будет удалён, а жалоба будет закрыта.',
          ),
        ];
      case ReportTargetType.user:
        return [
          ...common,
          _DecisionAction(
            label: 'Деактивировать пользователя',
            status: ReportStatusOption.resolved,
            action: ReviewReportActionOption.deactivateUser,
            confirmText: 'Пользователь будет деактивирован, а жалоба будет закрыта.',
          ),
        ];
    }
  }

  Future<void> _openReportDetails(ReportItem item) async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => ReportDetailsPage(reportId: item.id),
      ),
    );

    if (!mounted) return;
    await _load(silent: true);
  }

  bool get _hasActiveFilters =>
      _statusFilter != null || _targetTypeFilter != null || _reasonFilter != null;

  Widget _buildItem(ReportItem item) {
final processing = _processingReportId == item.id;
final actions = _actionsFor(item);
final hasAppeal = _hasAppeal(item);

return Padding(
  padding: const EdgeInsets.only(bottom: 10),
  child: Container(
    padding: const EdgeInsets.fromLTRB(14, 14, 14, 14),
    decoration: BoxDecoration(
      color: AppColors.surfaceGlass(0.18),
      borderRadius: BorderRadius.circular(22),
      border: Border.all(
        color: hasAppeal ? const Color(0xFF9E7BFF) : AppColors.border,
      ),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                _targetTitle(item),
                style: const TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: 16,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
            _SmallBadge(label: item.statusLabel),
          ],
        ),
        const SizedBox(height: 10),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            _SmallBadge(label: item.targetType.label),
            _SmallBadge(label: item.reason.label),
            if (hasAppeal) const _SmallBadge(label: 'Апелляция'),
          ],
        ),
        const SizedBox(height: 12),
        if (item.reporter != null)
          Row(
            children: [
              CircleAvatar(
                radius: 16,
                backgroundColor: AppColors.card,
                backgroundImage:
                    (item.reporter!.profilePhotoUrl ?? '').isNotEmpty
                        ? NetworkImage(item.reporter!.profilePhotoUrl!)
                        : null,
                child: (item.reporter!.profilePhotoUrl ?? '').isEmpty
                    ? const Icon(Icons.person_outline, size: 16)
                    : null,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  'Жалобу отправил: ${item.reporter!.displayName} · @${item.reporter!.userName}',
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 12,
                    height: 1.4,
                  ),
                ),
              ),
            ],
          )
        else
          Text(
            'Reporter: ${item.reporterUserId}',
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12,
            ),
          ),
        const SizedBox(height: 10),
        Text(
          item.resolution.resolutionText,
          style: const TextStyle(
            color: AppColors.textSecondary,
            fontSize: 12,
            height: 1.4,
          ),
        ),
        if (hasAppeal) ...[
          const SizedBox(height: 10),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: AppColors.surfaceGlass(0.18),
              borderRadius: BorderRadius.circular(14),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Апелляция пользователя',
                  style: TextStyle(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  item.resolution.appealText!.trim(),
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    height: 1.4,
                  ),
                ),
                if (item.resolution.appealedAt != null) ...[
                  const SizedBox(height: 6),
                  Text(
                    'Отправлена: ${_formatDate(item.resolution.appealedAt!)}',
                    style: const TextStyle(
                      color: AppColors.textSecondary,
                      fontSize: 12,
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
        const SizedBox(height: 10),
        if ((item.description ?? '').trim().isNotEmpty)
          Text(
            item.description!.trim(),
            style: const TextStyle(
              color: AppColors.textPrimary,
              height: 1.45,
            ),
          )
        else
          const Text(
            'Пользователь не добавил дополнительное описание.',
            style: TextStyle(
              color: AppColors.textSecondary,
              height: 1.45,
            ),
          ),
        const SizedBox(height: 10),
        Text(
          'Создано: ${_formatDate(item.createdAt)}',
          style: const TextStyle(
            color: AppColors.textSecondary,
            fontSize: 12,
          ),
        ),
        if (item.reviewedAt != null) ...[
          const SizedBox(height: 4),
          Text(
            'Рассмотрено: ${_formatDate(item.reviewedAt!)}',
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12,
            ),
          ),
        ],
        const SizedBox(height: 14),
        SizedBox(
          width: double.infinity,
          child: OutlinedButton(
            style: OutlinedButton.styleFrom(
              minimumSize: const Size(0, 44),
              padding: const EdgeInsets.symmetric(horizontal: 14),
              foregroundColor: AppColors.textPrimary,
              side: BorderSide(
                color: AppColors.accentLight.withValues(alpha: 0.38),
              ),
              backgroundColor: AppColors.white.withValues(alpha: 0.03),
            ),
            onPressed: processing ? null : () => _openReportDetails(item),
            child: const Text('Открыть детали жалобы'),
          ),
        ),
        if (actions.isNotEmpty) ...[
          const SizedBox(height: 10),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: actions.map((action) {
              final isDestructive = action.action == ReviewReportActionOption.deletePhoto ||
                  action.action == ReviewReportActionOption.deleteComment ||
                  action.action == ReviewReportActionOption.deactivateUser;

              return FilledButton.tonal(
                style: FilledButton.styleFrom(
                  minimumSize: const Size(0, 38),
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  backgroundColor: isDestructive
                      ? const Color(0xFF6A3B50).withValues(alpha: 0.78)
                      : AppColors.accentLight.withValues(alpha: 0.66),
                  foregroundColor: AppColors.textPrimary,
                  tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  visualDensity: VisualDensity.compact,
                ),
                onPressed: processing
                    ? null
                    : () => _applyDecision(
                          item: item,
                          status: action.status,
                          action: action.action,
                          confirmText: action.confirmText,
                        ),
                child: Text(action.label),
              );
            }).toList(),
          ),
        ],
        if (processing) ...[
          const SizedBox(height: 12),
          const LinearProgressIndicator(),
        ],
      ],
    ),
  ),
);
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: Center(child: CircularProgressIndicator()),
      );
    }

    if (_error != null) {
      return Scaffold(
        backgroundColor: AppColors.background,
        body: SafeArea(
          bottom: false,
          child: InMomentResponsiveContent(
            child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(8, 12, 8, 24),
            children: [
              _PageHeader(
                title: 'Модерация',
                onBack: () => Navigator.of(context).maybePop(),
              ),
              const SizedBox(height: 24),
              Center(
                child: Text(
                  _error!,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    height: 1.45,
                  ),
                ),
              ),
              ],
            ),
          ),
        ),
      );
    }

    final items = _visibleItems;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        bottom: false,
        child: InMomentResponsiveContent(
          child: RefreshIndicator(
          onRefresh: () => _load(silent: true),
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(8, 12, 8, 24),
            children: [
              _PageHeader(
                title: 'Модерация',
                onBack: () => Navigator.of(context).maybePop(),
                onRefresh: () => _load(silent: true),
              ),
              const SizedBox(height: 16),
              _HeaderFiltersCard(
                hasActiveFilters: _hasActiveFilters,
                statusFilter: _statusFilter,
                targetTypeFilter: _targetTypeFilter,
                reasonFilter: _reasonFilter,
                count: items.length,
                onOpenFilters: _openFilterSheet,
                onResetFilters: _resetFilters,
              ),
              const SizedBox(height: 12),
              _TabBarCard(
                current: _tab,
                pendingCount: _pendingCount,
                appealsCount: _appealsCount,
                archiveCount: _archiveCount,
                onChanged: (tab) => setState(() => _tab = tab),
              ),
              const SizedBox(height: 12),
              if (items.isEmpty)
                const _EmptyModerationState()
              else
                ...items.map(_buildItem),
            ],
            ),
          ),
        ),
      ),
    );
  }
}

class _DecisionAction {
  final String label;
  final ReportStatusOption status;
  final ReviewReportActionOption action;
  final String confirmText;

  const _DecisionAction({
    required this.label,
    required this.status,
    required this.action,
    required this.confirmText,
  });
}

class _HeaderFiltersCard extends StatelessWidget {
  final bool hasActiveFilters;
  final ReportStatusOption? statusFilter;
  final ReportTargetType? targetTypeFilter;
  final ReportReasonOption? reasonFilter;
  final int count;
  final VoidCallback onOpenFilters;
  final VoidCallback onResetFilters;

  const _HeaderFiltersCard({
    required this.hasActiveFilters,
    required this.statusFilter,
    required this.targetTypeFilter,
    required this.reasonFilter,
    required this.count,
    required this.onOpenFilters,
    required this.onResetFilters,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 14, 14, 14),
      decoration: BoxDecoration(
        color: AppColors.surfaceGlass(0.18),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(
          color: AppColors.softStroke(0.08),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Expanded(
                child: Text(
                  'Фильтры и поиск',
                  style: TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 15.5,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              FilledButton.tonal(
                style: FilledButton.styleFrom(
                  minimumSize: const Size(0, 40),
                  padding: const EdgeInsets.symmetric(horizontal: 14),
                  backgroundColor: AppColors.accentLight.withValues(alpha: 0.82),
                  foregroundColor: AppColors.textPrimary,
                  tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  visualDensity: VisualDensity.compact,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(999),
                  ),
                ),
                onPressed: onOpenFilters,
                child: const Text('Фильтры'),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(
            'Найдено жалоб: $count',
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12.5,
              fontWeight: FontWeight.w700,
            ),
          ),
          if (hasActiveFilters) ...[
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                if (statusFilter != null) _SmallBadge(label: statusFilter!.label),
                if (targetTypeFilter != null)
                  _SmallBadge(label: targetTypeFilter!.label),
                if (reasonFilter != null) _SmallBadge(label: reasonFilter!.label),
                OutlinedButton(
                  style: OutlinedButton.styleFrom(
                    minimumSize: const Size(0, 36),
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                    visualDensity: VisualDensity.compact,
                  ),
                  onPressed: onResetFilters,
                  child: const Text('Сбросить'),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _TabBarCard extends StatelessWidget {
  final _ModerationTab current;
  final int pendingCount;
  final int appealsCount;
  final int archiveCount;
  final ValueChanged<_ModerationTab> onChanged;

  const _TabBarCard({
    required this.current,
    required this.pendingCount,
    required this.appealsCount,
    required this.archiveCount,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(6),
      decoration: BoxDecoration(
        color: AppColors.surfaceGlass(0.18),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(
          color: AppColors.softStroke(0.08),
        ),
      ),
      child: Row(
        children: [
          Expanded(
            child: _TabButton(
              label: 'Новые',
              count: pendingCount,
              selected: current == _ModerationTab.pending,
              onTap: () => onChanged(_ModerationTab.pending),
            ),
          ),
          const SizedBox(width: 6),
          Expanded(
            child: _TabButton(
              label: 'Апелляции',
              count: appealsCount,
              selected: current == _ModerationTab.appeals,
              onTap: () => onChanged(_ModerationTab.appeals),
            ),
          ),
          const SizedBox(width: 6),
          Expanded(
            child: _TabButton(
              label: 'Архив',
              count: archiveCount,
              selected: current == _ModerationTab.archive,
              onTap: () => onChanged(_ModerationTab.archive),
            ),
          ),
        ],
      ),
    );
  }
}

class _TabButton extends StatelessWidget {
  final String label;
  final int count;
  final bool selected;
  final VoidCallback onTap;

  const _TabButton({
    required this.label,
    required this.count,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected
          ? AppColors.white.withValues(alpha: 0.08)
          : Colors.transparent,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 10),
          child: Column(
            children: [
              Text(
                label,
                style: TextStyle(
                  color: selected
                      ? AppColors.textPrimary
                      : AppColors.textSecondary,
                  fontWeight: FontWeight.w800,
                  fontSize: 12.5,
                ),
              ),
              const SizedBox(height: 3),
              Text(
                count.toString(),
                style: TextStyle(
                  color: selected
                      ? AppColors.textPrimary.withValues(alpha: 0.88)
                      : AppColors.textSecondary,
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ModerationFilterSheet extends StatefulWidget {
  final ReportStatusOption? initialStatus;
  final ReportTargetType? initialTargetType;
  final ReportReasonOption? initialReason;

  const _ModerationFilterSheet({
    required this.initialStatus,
    required this.initialTargetType,
    required this.initialReason,
  });

  @override
  State<_ModerationFilterSheet> createState() => _ModerationFilterSheetState();
}

class _ModerationFilterSheetState extends State<_ModerationFilterSheet> {
  ReportStatusOption? _status;
  ReportTargetType? _targetType;
  ReportReasonOption? _reason;

  @override
  void initState() {
    super.initState();
    _status = widget.initialStatus;
    _targetType = widget.initialTargetType;
    _reason = widget.initialReason;
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
      child: SafeArea(
        top: false,
        child: ListView(
          shrinkWrap: true,
          children: [
            Center(
              child: Container(
                width: 44,
                height: 5,
                decoration: BoxDecoration(
                  color: AppColors.softStroke(0.12),
                  borderRadius: BorderRadius.circular(999),
                ),
              ),
            ),
            const SizedBox(height: 16),
            const Text(
              'Фильтры модерации',
              style: TextStyle(
                color: AppColors.textPrimary,
                fontSize: 18,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 16),
            _DropdownSection<ReportStatusOption?>(
              label: 'Статус',
              value: _status,
              items: [
                const DropdownMenuItem(value: null, child: Text('Все статусы')),
                ...ReportStatusOption.values.map(
                  (x) => DropdownMenuItem(value: x, child: Text(x.label)),
                ),
              ],
              onChanged: (value) => setState(() => _status = value),
            ),
            const SizedBox(height: 12),
            _DropdownSection<ReportTargetType?>(
              label: 'Тип жалобы',
              value: _targetType,
              items: [
                const DropdownMenuItem(value: null, child: Text('Все типы')),
                ...ReportTargetType.values.map(
                  (x) => DropdownMenuItem(value: x, child: Text(x.label)),
                ),
              ],
              onChanged: (value) => setState(() => _targetType = value),
            ),
            const SizedBox(height: 12),
            _DropdownSection<ReportReasonOption?>(
              label: 'Причина',
              value: _reason,
              items: [
                const DropdownMenuItem(value: null, child: Text('Все причины')),
                ...ReportReasonOption.values.map(
                  (x) => DropdownMenuItem(value: x, child: Text(x.label)),
                ),
              ],
              onChanged: (value) => setState(() => _reason = value),
            ),
            const SizedBox(height: 20),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: () {
                      Navigator.of(context).pop(
                        const _FilterResult(
                          status: null,
                          targetType: null,
                          reason: null,
                        ),
                      );
                    },
                    child: const Text('Сбросить'),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: FilledButton(
                    onPressed: () {
                      Navigator.of(context).pop(
                        _FilterResult(
                          status: _status,
                          targetType: _targetType,
                          reason: _reason,
                        ),
                      );
                    },
                    child: const Text('Применить'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _FilterResult {
  final ReportStatusOption? status;
  final ReportTargetType? targetType;
  final ReportReasonOption? reason;

  const _FilterResult({
    required this.status,
    required this.targetType,
    required this.reason,
  });
}

class _DropdownSection<T> extends StatelessWidget {
  final String label;
  final T value;
  final List<DropdownMenuItem<T>> items;
  final ValueChanged<T?> onChanged;

  const _DropdownSection({
    required this.label,
    required this.value,
    required this.items,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            color: AppColors.textSecondary,
            fontSize: 12,
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(height: 8),
        DropdownButtonFormField<T>(
          initialValue: value,
          items: items,
          onChanged: onChanged,
          dropdownColor: AppColors.surface,
          style: const TextStyle(color: AppColors.textPrimary),
          decoration: const InputDecoration(),
        ),
      ],
    );
  }
}

class _SmallBadge extends StatelessWidget {
  final String label;

  const _SmallBadge({
    required this.label,
  });

  Color _statusColor() {
    final normalized = label.toLowerCase();
    if (normalized.contains('нов')) return const Color(0xFFBFA7FF);
    if (normalized.contains('реш') ||
        normalized.contains('просмотр') ||
        normalized.contains('подтверж')) {
      return AppColors.success;
    }
    if (normalized.contains('отклон') || normalized.contains('удал')) {
      return AppColors.error;
    }
    if (normalized.contains('апелляц') ||
        normalized.contains('работ') ||
        normalized.contains('ожида')) {
      return AppColors.warning;
    }
    return AppColors.textSecondary;
  }

  @override
  Widget build(BuildContext context) {
    final statusColor = _statusColor();

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: statusColor.withValues(alpha: 0.22),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: statusColor.withValues(alpha: 0.44)),
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: AppColors.textPrimary,
          fontSize: 12,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

class _EmptyModerationState extends StatelessWidget {
  const _EmptyModerationState();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.only(top: 80),
      child: Column(
        children: [
          Icon(
            Icons.verified_user_outlined,
            size: 54,
            color: AppColors.textSecondary,
          ),
          SizedBox(height: 16),
          Text(
            'Список жалоб пуст',
            textAlign: TextAlign.center,
            style: TextStyle(
              color: AppColors.textPrimary,
              fontSize: 16,
              fontWeight: FontWeight.w700,
            ),
          ),
          SizedBox(height: 8),
          Text(
            'Когда пользователи отправят жалобы, они появятся здесь.',
            textAlign: TextAlign.center,
            style: TextStyle(
              color: AppColors.textSecondary,
              height: 1.45,
            ),
          ),
        ],
      ),      
    );
  }
}

class _PageHeader extends StatelessWidget {
  final String title;
  final VoidCallback? onBack;
  final VoidCallback? onRefresh;

  const _PageHeader({
    required this.title,
    this.onBack,
    this.onRefresh,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 56,
      child: Stack(
        alignment: Alignment.center,
        children: [
          if (onBack != null)
            Align(
              alignment: Alignment.centerLeft,
              child: _HeaderIconButton(
                icon: Icons.arrow_back_ios_new_rounded,
                onPressed: onBack,
              ),
            ),
          Text(
            title,
            textAlign: TextAlign.center,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 20,
              fontWeight: FontWeight.w800,
            ),
          ),
          Align(
            alignment: Alignment.centerRight,
            child: onRefresh != null
                ? _HeaderIconButton(
                    icon: Icons.refresh_rounded,
                    onPressed: onRefresh,
                  )
                : const SizedBox(width: 48, height: 48),
          ),
        ],
      ),
    );
  }
}

class _HeaderIconButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onPressed;

  const _HeaderIconButton({
    required this.icon,
    required this.onPressed,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      shape: const CircleBorder(),
      child: InkWell(
        onTap: onPressed,
        customBorder: const CircleBorder(),
        child: Container(
          width: 48,
          height: 48,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            color: AppColors.surfaceGlass(0.16),
            border: Border.all(color: AppColors.softStroke(0.08)),
          ),
          child: Icon(
            icon,
            color: AppColors.textPrimary,
            size: 25,
          ),
        ),
      ),
    );
  }
}