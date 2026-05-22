import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_responsive_content.dart';
import '../api/reports_api.dart';
import '../models/report_item.dart';
import '../models/report_reason_option.dart';
import '../../photo/pages/photo_details_page.dart';
import '../../profile/pages/public_user_profile_page.dart';

class MyReportsPage extends StatefulWidget {
  const MyReportsPage({super.key});

  @override
  State<MyReportsPage> createState() => _MyReportsPageState();
}

enum _MyReportsTab {
  all,
  newReports,
  waitingDecision,
  resolved,
}

class _MyReportsPageState extends State<MyReportsPage> {
  final ReportsApi _api = ReportsApi();

  bool _loading = true;
  String? _error;
  String? _appealingReportId;
  List<ReportItem> _items = const [];
  _MyReportsTab _tab = _MyReportsTab.all;

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
      final items = await _api.getMyReports();

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

  String _targetLabel(ReportItem item) {
    switch (item.targetType) {
      case ReportTargetType.photo:
        return 'Публикация';
      case ReportTargetType.comment:
        return 'Комментарий';
      case ReportTargetType.user:
        return 'Пользователь';
    }
  }

  String? _targetSubtitle(ReportItem item) {
    switch (item.targetType) {
      case ReportTargetType.photo:
        final photo = item.targetContext.photo;
        if (photo == null) return 'ID публикации: ${item.targetId}';

        final author = photo.authorDisplayName.trim().isNotEmpty
            ? photo.authorDisplayName.trim()
            : (photo.authorUserName.trim().isNotEmpty
                ? '@${photo.authorUserName.trim()}'
                : 'Автор публикации');

        final group = (photo.groupName ?? '').trim();
        final caption = (photo.caption ?? '').trim();

        if (group.isNotEmpty && caption.isNotEmpty) {
          return '$author · группа «$group» · "$caption"';
        }

        if (group.isNotEmpty) {
          return '$author · группа «$group»';
        }

        if (caption.isNotEmpty) {
          return '$author · "$caption"';
        }

        return author;

      case ReportTargetType.comment:
        final comment = item.targetContext.comment;
        if (comment == null) return 'ID комментария: ${item.targetId}';

        final author = comment.authorDisplayName.trim().isNotEmpty
            ? comment.authorDisplayName.trim()
            : (comment.authorUserName.trim().isNotEmpty
                ? '@${comment.authorUserName.trim()}'
                : 'Автор комментария');

        final text = comment.text.trim();
        if (text.isNotEmpty) {
          return '$author · "$text"';
        }

        return author;

      case ReportTargetType.user:
        final user = item.targetContext.user;
        if (user == null) return 'ID пользователя: ${item.targetId}';

        final display = user.displayName.trim().isNotEmpty
            ? user.displayName.trim()
            : (user.userName.trim().isNotEmpty
                ? '@${user.userName.trim()}'
                : 'Пользователь');

        final status =
            user.isActive ? 'аккаунт активен' : 'аккаунт деактивирован';

        if (user.userName.trim().isNotEmpty) {
          return '$display · @${user.userName.trim()} · $status';
        }

        return '$display · $status';
    }
  }

  bool _hasAppeal(ReportItem item) =>
      (item.resolution.appealText ?? '').trim().isNotEmpty;

  bool _isNewReport(ReportItem item) =>
      item.status == ReportStatusOption.pending && !_hasAppeal(item);

  bool _isWaitingDecision(ReportItem item) =>
      item.status == ReportStatusOption.reviewed ||
      (item.status == ReportStatusOption.pending && _hasAppeal(item));

  bool _isResolved(ReportItem item) =>
      item.status == ReportStatusOption.resolved ||
      item.status == ReportStatusOption.rejected;

  List<ReportItem> get _visibleItems {
    final list = _items.where((item) {
      switch (_tab) {
        case _MyReportsTab.all:
          return true;
        case _MyReportsTab.newReports:
          return _isNewReport(item);
        case _MyReportsTab.waitingDecision:
          return _isWaitingDecision(item);
        case _MyReportsTab.resolved:
          return _isResolved(item);
      }
    }).toList();

    list.sort(
      (a, b) => b.createdAt.millisecondsSinceEpoch.compareTo(
        a.createdAt.millisecondsSinceEpoch,
      ),
    );

    return list;
  }

  int get _allCount => _items.length;
  int get _newCount => _items.where(_isNewReport).length;
  int get _waitingCount => _items.where(_isWaitingDecision).length;
  int get _resolvedCount => _items.where(_isResolved).length;

  bool _canAppeal(ReportItem item) {
    if (item.status == ReportStatusOption.pending) return false;
    if ((item.resolution.appealText ?? '').trim().isNotEmpty) return false;
    return true;
  }

  Future<void> _openAppealDialog(ReportItem item) async {
    final controller = TextEditingController();
    String? localError;

    final submitted = await showDialog<bool>(
          context: context,
          builder: (dialogContext) {
            return StatefulBuilder(
              builder: (context, setLocalState) {
                return AlertDialog(
                  backgroundColor: AppColors.card,
                  title: const Text(
                    'Запросить пересмотр',
                    style: TextStyle(color: AppColors.textPrimary),
                  ),
                  content: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Опишите, почему вы не согласны с решением модерации.',
                        style: TextStyle(
                          color: AppColors.textSecondary,
                          height: 1.4,
                        ),
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: controller,
                        minLines: 4,
                        maxLines: 6,
                        style: const TextStyle(color: AppColors.textPrimary),
                        decoration: const InputDecoration(
                          hintText: 'Например: решение было принято без учёта контекста...',
                        ),
                      ),
                      if (localError != null) ...[
                        const SizedBox(height: 10),
                        Text(
                          localError!,
                          style: const TextStyle(
                            color: Colors.redAccent,
                            fontSize: 12,
                          ),
                        ),
                      ],
                    ],
                  ),
                  actions: [
                    TextButton(
                      onPressed: () => Navigator.of(dialogContext).pop(false),
                      child: const Text('Отмена'),
                    ),
                    FilledButton(
                      onPressed: () {
                        final text = controller.text.trim();
                        if (text.isEmpty) {
                          setLocalState(() {
                            localError = 'Введите текст апелляции.';
                          });
                          return;
                        }
                        Navigator.of(dialogContext).pop(true);
                      },
                      child: const Text('Отправить'),
                    ),
                  ],
                );
              },
            );
          },
        ) ??
        false;

    if (!submitted) {
      controller.dispose();
      return;
    }

    setState(() {
      _appealingReportId = item.id;
    });

    try {
      await _api.appealReport(
        reportId: item.id,
        text: controller.text.trim(),
      );

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Апелляция отправлена')),
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
      controller.dispose();
      if (mounted) {
        setState(() {
          _appealingReportId = null;
        });
      }
    }
  }

  void _showSnack(String text) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(text)),
    );
  }

  bool _canOpenTarget(ReportItem item) {
    switch (item.targetType) {
      case ReportTargetType.user:
        return item.targetId.trim().isNotEmpty;

      case ReportTargetType.photo:
        return item.targetId.trim().isNotEmpty;

      case ReportTargetType.comment:
        final photoId = item.targetContext.comment?.photoId.trim() ?? '';
        return item.targetId.trim().isNotEmpty && photoId.isNotEmpty;
    }
  }

  Future<void> _openReportTarget(ReportItem item) async {
    try {
      switch (item.targetType) {
        case ReportTargetType.user:
          if (item.targetId.trim().isEmpty) {
            _showSnack('Профиль пользователя недоступен.');
            return;
          }

          await Navigator.of(context).push(
            MaterialPageRoute(
              builder: (_) => PublicUserProfilePage(
                userId: item.targetId,
              ),
            ),
          );
          return;

        case ReportTargetType.photo:
          final photo = item.targetContext.photo;
          final photoId = item.targetId.trim();

          if (photoId.isEmpty) {
            _showSnack('Публикация недоступна.');
            return;
          }

          await Navigator.of(context).push(
            MaterialPageRoute(
              builder: (_) => PhotoDetailsPage(
                photoId: photoId,
                groupId: photo?.groupId,
              ),
            ),
          );
          return;

        case ReportTargetType.comment:
          final comment = item.targetContext.comment;
          final commentId = item.targetId.trim();
          final photoId = comment?.photoId.trim() ?? '';

          if (commentId.isEmpty || photoId.isEmpty) {
            _showSnack('Комментарий недоступен.');
            return;
          }

          await Navigator.of(context).push(
            MaterialPageRoute(
              builder: (_) => PhotoDetailsPage(
                photoId: photoId,
                initialCommentId: commentId,
              ),
            ),
          );
          return;
      }
    } catch (e) {
      if (!mounted) return;
      _showSnack(
        ApiError.normalize(
          e,
          fallback: 'Не удалось выполнить действие с жалобой.',
        ),
      );
    }
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
          child: InMomentResponsiveContent(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
              child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  _error!,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    height: 1.45,
                  ),
                ),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: _load,
                  child: const Text('Повторить'),
                ),
              ],
              ),
            ),
          ),
        ),
      );
    }

    final items = _visibleItems;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: InMomentResponsiveContent(
          child: RefreshIndicator(
          onRefresh: () => _load(silent: true),
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
            children: [
              _ReportsPageHeader(
                title: 'Мои жалобы',
                onBack: () => Navigator.of(context).maybePop(),
              ),
              const SizedBox(height: 16),
              _MyReportsTabsCard(
                current: _tab,
                allCount: _allCount,
                newCount: _newCount,
                waitingCount: _waitingCount,
                resolvedCount: _resolvedCount,
                onChanged: (tab) => setState(() => _tab = tab),
              ),
              const SizedBox(height: 12),
              if (items.isEmpty)
                const _EmptyMyReportsState()
              else
                ...items.map((item) {
                  final appealing = _appealingReportId == item.id;
                  final canAppeal = _canAppeal(item);
                  final hasAppeal = _hasAppeal(item);

                  return Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: _GlassReportPanel(
                      padding: const EdgeInsets.all(16),
                      highlight: hasAppeal,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Expanded(
                                child: Text(
                                  _targetLabel(item),
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
                              _SmallBadge(label: item.reason.label),
                              if (hasAppeal) const _SmallBadge(label: 'Апелляция отправлена'),
                            ],
                          ),
                          const SizedBox(height: 12),
                          Material(
                            color: Colors.transparent,
                            child: InkWell(
                              borderRadius: BorderRadius.circular(16),
                              onTap: _canOpenTarget(item)
                                  ? () => _openReportTarget(item)
                                  : null,
                              child: _GlassInfoBlock(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Row(
                                      children: [
                                        const Expanded(
                                          child: Text(
                                            'Объект жалобы',
                                            style: TextStyle(
                                              color: AppColors.textPrimary,
                                              fontWeight: FontWeight.w700,
                                            ),
                                          ),
                                        ),
                                        if (_canOpenTarget(item))
                                          const Icon(
                                            Icons.open_in_new_rounded,
                                            size: 18,
                                            color: AppColors.textSecondary,
                                          ),
                                      ],
                                    ),
                                    const SizedBox(height: 8),
                                    Text(
                                      _targetLabel(item),
                                      style: const TextStyle(
                                        color: AppColors.textPrimary,
                                        fontSize: 14,
                                        fontWeight: FontWeight.w700,
                                      ),
                                    ),
                                    const SizedBox(height: 6),
                                    Text(
                                      (_targetSubtitle(item) ?? '').trim().isEmpty
                                          ? 'Детали объекта жалобы пока недоступны.'
                                          : _targetSubtitle(item)!,
                                      style: const TextStyle(
                                        color: AppColors.textSecondary,
                                        height: 1.4,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          ),
                          const SizedBox(height: 12),
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
                              'Вы не добавили дополнительное описание к жалобе.',
                              style: TextStyle(
                                color: AppColors.textSecondary,
                                height: 1.45,
                              ),
                            ),
                          const SizedBox(height: 12),
                          _GlassInfoBlock(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                const Text(
                                  'Итог по жалобе',
                                  style: TextStyle(
                                    color: AppColors.textPrimary,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                                Text(
                                  item.resolution.resolutionText,
                                  style: const TextStyle(
                                    color: AppColors.textSecondary,
                                    height: 1.4,
                                  ),
                                ),
                                if (_canOpenTarget(item)) ...[
                                  const SizedBox(height: 8),
                                  const Text(
                                    'Нажмите на блок «Объект жалобы», чтобы открыть связанный объект.',
                                    style: TextStyle(
                                      color: AppColors.textSecondary,
                                      fontSize: 12,
                                      height: 1.35,
                                    ),
                                  ),
                                ],
                                if (item.targetType == ReportTargetType.user) ...[
                                  const SizedBox(height: 8),
                                  Text(
                                    item.targetContext.user != null
                                        ? (item.targetContext.user!.displayName.trim().isNotEmpty
                                            ? 'Пользователь: ${item.targetContext.user!.displayName}'
                                            : (item.targetContext.user!.userName.trim().isNotEmpty
                                                ? 'Пользователь: @${item.targetContext.user!.userName}'
                                                : 'Пользователь: ${item.targetId}'))
                                        : 'Пользователь: ${item.targetId}',
                                    style: const TextStyle(
                                      color: AppColors.textPrimary,
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                ],
                                if (hasAppeal) ...[
                                  const SizedBox(height: 10),
                                  const Text(
                                    'Ваша апелляция',
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
                              ],
                            ),
                          ),
                          const SizedBox(height: 10),
                          Text(
                            'Отправлено: ${_formatDate(item.createdAt)}',
                            style: const TextStyle(
                              color: AppColors.textSecondary,
                              fontSize: 12,
                            ),
                          ),
                          if (item.reviewedAt != null) ...[
                            const SizedBox(height: 4),
                            Text(
                              'Решение принято: ${_formatDate(item.reviewedAt!)}',
                              style: const TextStyle(
                                color: AppColors.textSecondary,
                                fontSize: 12,
                              ),
                            ),
                          ],
                          if (canAppeal) ...[
                            const SizedBox(height: 14),
                            Align(
                              alignment: Alignment.centerLeft,
                              child: OutlinedButton(
                                onPressed: appealing ? null : () => _openAppealDialog(item),
                                style: OutlinedButton.styleFrom(
                                  minimumSize: const Size(0, 42),
                                  padding: const EdgeInsets.symmetric(horizontal: 18),
                                ),
                                child: Text(
                                  appealing ? 'Отправка...' : 'Запросить пересмотр',
                                ),
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                  );
                }
              ),
            ],
          ),
        ),
      ),
      ),
    );
  }
}


class _ReportsPageHeader extends StatelessWidget {
  final String title;
  final VoidCallback onBack;

  const _ReportsPageHeader({
    required this.title,
    required this.onBack,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 44,
      child: Row(
        children: [
          _HeaderCircleButton(
            icon: Icons.arrow_back_ios_new_rounded,
            onTap: onBack,
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              title,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontSize: 20,
                fontWeight: FontWeight.w900,
              ),
            ),
          ),
          const SizedBox(width: 52),
        ],
      ),
    );
  }
}

class _HeaderCircleButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onTap;

  const _HeaderCircleButton({
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.surfaceGlass(0.28),
      shape: const CircleBorder(),
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: SizedBox(
          width: 44,
          height: 44,
          child: Icon(
            icon,
            size: 19,
            color: AppColors.textPrimary,
          ),
        ),
      ),
    );
  }
}


class _MyReportsTabsCard extends StatelessWidget {
  final _MyReportsTab current;
  final int allCount;
  final int newCount;
  final int waitingCount;
  final int resolvedCount;
  final ValueChanged<_MyReportsTab> onChanged;

  const _MyReportsTabsCard({
    required this.current,
    required this.allCount,
    required this.newCount,
    required this.waitingCount,
    required this.resolvedCount,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return _GlassReportPanel(
      padding: const EdgeInsets.all(10),
      child: Column(
        children: [
          Row(
            children: [
              Expanded(
                child: _TabButton(
                  label: 'Все',
                  count: allCount,
                  selected: current == _MyReportsTab.all,
                  onTap: () => onChanged(_MyReportsTab.all),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: _TabButton(
                  label: 'Новые',
                  count: newCount,
                  selected: current == _MyReportsTab.newReports,
                  onTap: () => onChanged(_MyReportsTab.newReports),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: _TabButton(
                  label: 'Ожидают решения',
                  count: waitingCount,
                  selected: current == _MyReportsTab.waitingDecision,
                  onTap: () => onChanged(_MyReportsTab.waitingDecision),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: _TabButton(
                  label: 'Решённые',
                  count: resolvedCount,
                  selected: current == _MyReportsTab.resolved,
                  onTap: () => onChanged(_MyReportsTab.resolved),
                ),
              ),
            ],
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
      color: Colors.transparent,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: onTap,
        child: AnimatedContainer(
          height: 86,
          duration: const Duration(milliseconds: 180),
          curve: Curves.easeOut,
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 12),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            color: selected
                ? AppColors.accentSoft.withValues(alpha: 0.14)
                : AppColors.surfaceDeep.withValues(alpha: 0.20),
            border: Border.all(
              color: selected
                  ? AppColors.accentSoft.withValues(alpha: 0.30)
                  : AppColors.softStroke(0.06),
            ),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text(
                label,
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: selected ? AppColors.textPrimary : AppColors.textSecondary,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                count.toString(),
                style: const TextStyle(
                  color: AppColors.textSecondary,
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
        ),
      ),
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
    if (normalized.contains('реш') || normalized.contains('подтверж')) {
      return AppColors.success;
    }
    if (normalized.contains('отклон')) return AppColors.error;
    if (normalized.contains('ожида') || normalized.contains('апелляц')) {
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
        color: statusColor.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: statusColor.withValues(alpha: 0.28)),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: statusColor,
          fontSize: 12,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

class _GlassReportPanel extends StatelessWidget {
  final Widget child;
  final EdgeInsetsGeometry padding;
  final bool highlight;

  const _GlassReportPanel({
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.highlight = false,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: padding,
      decoration: BoxDecoration(
        color: AppColors.surfaceDeep.withValues(alpha: highlight ? 0.50 : 0.34),
        borderRadius: BorderRadius.circular(24),
        border: Border.all(
          color: highlight
              ? AppColors.accentSoft.withValues(alpha: 0.24)
              : AppColors.softStroke(0.09),
        ),
      ),
      child: child,
    );
  }
}

class _GlassInfoBlock extends StatelessWidget {
  final Widget child;

  const _GlassInfoBlock({required this.child});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(17),
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [
            Colors.white.withValues(alpha: 0.045),
            AppColors.surfaceDeep.withValues(alpha: 0.42),
          ],
        ),
        border: Border.all(color: AppColors.softStroke(0.10)),
      ),
      child: child,
    );
  }
}

class _EmptyMyReportsState extends StatelessWidget {
  const _EmptyMyReportsState();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.only(top: 80),
      child: Column(
        children: [
          Icon(
            Icons.shield_outlined,
            size: 54,
            color: AppColors.textSecondary,
          ),
          SizedBox(height: 16),
          Text(
            'У вас пока нет отправленных жалоб',
            textAlign: TextAlign.center,
            style: TextStyle(
              color: AppColors.textPrimary,
              fontSize: 16,
              fontWeight: FontWeight.w700,
            ),
          ),
          SizedBox(height: 8),
          Text(
            'Когда вы пожалуетесь на публикацию, комментарий или пользователя, это появится здесь.',
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