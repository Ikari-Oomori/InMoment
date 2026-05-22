import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/network_visual_media.dart';
import '../../../core/widgets/inmoment_responsive_content.dart';
import '../api/reports_api.dart';
import '../models/report_item.dart';
import '../models/report_reason_option.dart';

class ReportDetailsPage extends StatefulWidget {
  final String reportId;

  const ReportDetailsPage({
    super.key,
    required this.reportId,
  });

  @override
  State<ReportDetailsPage> createState() => _ReportDetailsPageState();
}

class _ReportDetailsPageState extends State<ReportDetailsPage> {
  final ReportsApi _api = ReportsApi();

  bool _loading = true;
  bool _processing = false;
  String? _error;
  ReportItem? _item;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final item = await _api.getReportDetails(widget.reportId);

      if (!mounted) return;
      setState(() {
        _item = item;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить жалобу.',
        );
        _loading = false;
      });
    }
  }

  String _formatDate(DateTime value) {
    final local = value.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');
    return '${two(local.day)}.${two(local.month)}.${local.year} ${two(local.hour)}:${two(local.minute)}';
  }

  Future<void> _applyDecision({
    required ReportStatusOption status,
    required ReviewReportActionOption action,
    required String confirmText,
  }) async {
    final item = _item;
    if (item == null) return;

    final confirmed = await showDialog<bool>(
          context: context,
          builder: (context) => AlertDialog(
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
        ) ??
        false;

    if (!confirmed) return;

    setState(() {
      _processing = true;
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
      await _load();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось обновить жалобу.',
            ),
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _processing = false;
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
        confirmText: 'Жалоба будет отклонена.',
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
        confirmText: 'Жалоба будет закрыта без дополнительных действий.',
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
            confirmText: 'Фото будет удалено, а жалоба закрыта.',
          ),
        ];
      case ReportTargetType.comment:
        return [
          ...common,
          _DecisionAction(
            label: 'Удалить комментарий',
            status: ReportStatusOption.resolved,
            action: ReviewReportActionOption.deleteComment,
            confirmText: 'Комментарий будет удалён, а жалоба закрыта.',
          ),
        ];
      case ReportTargetType.user:
        return [
          ...common,
          _DecisionAction(
            label: 'Деактивировать пользователя',
            status: ReportStatusOption.resolved,
            action: ReviewReportActionOption.deactivateUser,
            confirmText: 'Пользователь будет деактивирован, а жалоба закрыта.',
          ),
        ];
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

    if (_error != null || _item == null) {
      return Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(title: const Text('Жалоба')),
        body: InMomentResponsiveContent(
          alignment: Alignment.center,
            child: Center(
              child: Padding(
              padding: const EdgeInsets.all(24),
              child: Text(
                _error ?? 'Не удалось загрузить жалобу',
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: AppColors.textPrimary,
                  height: 1.45,
                ),
              ),
            ),
          ),
        ),
      );
    }

    final item = _item!;
    final photo = item.targetContext.photo;
    final comment = item.targetContext.comment;
    final user = item.targetContext.user;
    final actions = _actionsFor(item);
    final hasAppeal = (item.resolution.appealText ?? '').trim().isNotEmpty;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Жалоба')),
      body: InMomentResponsiveContent(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
          children: [
            _InfoCard(
              title: 'Сводка',
              children: [
                _InfoRow('Тип', item.targetType.label),
                _InfoRow('Причина', item.reason.label),
                _InfoRow('Статус', item.statusLabel),
                _InfoRow('Итог', item.resolution.resolutionText),
                _InfoRow(
                  'Автор жалобы',
                  item.reporter == null
                      ? item.reporterUserId
                      : '${item.reporter!.displayName} · @${item.reporter!.userName}',
                ),
                _InfoRow('Создано', _formatDate(item.createdAt)),
                if (item.reviewedAt != null)
                  _InfoRow('Рассмотрено', _formatDate(item.reviewedAt!)),
              ],
            ),
            const SizedBox(height: 12),
            if ((item.description ?? '').trim().isNotEmpty)
              _TextCard(
                title: 'Описание жалобы',
                text: item.description!.trim(),
              ),
            if ((item.description ?? '').trim().isNotEmpty)
              const SizedBox(height: 12),
            if (hasAppeal)
              _TextCard(
                title: 'Апелляция пользователя',
                text: item.resolution.appealText!.trim(),
              ),
            if (hasAppeal) const SizedBox(height: 12),
            if (photo != null)
              _PhotoPreviewCard(
                photo: photo,
              ),
            if (photo != null) const SizedBox(height: 12),
            if (comment != null)
              _CommentPreviewCard(
                comment: comment,
                photo: photo,
                ),
            if (comment != null) const SizedBox(height: 12),
            if (user != null)
              _UserPreviewCard(
                user: user,
              ),
            if (user != null) const SizedBox(height: 12),
            _ActionsCard(
              processing: _processing,
              actions: actions,
              status: item.status,
              onApply: (action) => _applyDecision(
                status: action.status,
                action: action.action,
                confirmText: action.confirmText,
              ),
            ),
          ],
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

class _InfoCard extends StatelessWidget {
  final String title;
  final List<Widget> children;

  const _InfoCard({
    required this.title,
    required this.children,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
      decoration: BoxDecoration(
        color: AppColors.surfaceGlass(0.18),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 16,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 10),
          ...children,
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  final String label;
  final String value;

  const _InfoRow(this.label, this.value);

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12,
              fontWeight: FontWeight.w700,
              height: 1.2,
            ),
          ),
          const SizedBox(height: 3),
          Text(
            value,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 14,
              fontWeight: FontWeight.w600,
              height: 1.35,
            ),
          ),
        ],
      ),
    );
  }
}

class _TextCard extends StatelessWidget {
  final String title;
  final String text;

  const _TextCard({
    required this.title,
    required this.text,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surfaceGlass(0.18),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 16,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 10),
          Text(
            text,
            style: const TextStyle(
              color: AppColors.textPrimary,
              height: 1.45,
            ),
          ),
        ],
      ),
    );
  }
}

class _PhotoPreviewCard extends StatelessWidget {
  final ReportPhotoPreview photo;

  const _PhotoPreviewCard({
    required this.photo,
  });

    String _contentTypeFromUrl(String url) {
    final normalized = url.toLowerCase();

    if (normalized.contains('.mp4') ||
        normalized.contains('.mov') ||
        normalized.contains('.m4v') ||
        normalized.contains('.webm') ||
        normalized.contains('.3gp')) {
      return 'video/mp4';
    }

    return 'image/jpeg';
  }

  @override
  Widget build(BuildContext context) {
    final hasPhoto = (photo.photoUrl ?? '').trim().isNotEmpty;
    final hasCaption = (photo.caption ?? '').trim().isNotEmpty;
    final hasGroup = (photo.groupName ?? '').trim().isNotEmpty;

    return Container(
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
      decoration: BoxDecoration(
        color: AppColors.surfaceGlass(0.18),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Публикация',
            style: TextStyle(
              color: AppColors.textPrimary,
              fontSize: 16,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 12),
          if (hasPhoto)
            ClipRRect(
              borderRadius: BorderRadius.circular(16),
              child: AspectRatio(
                aspectRatio: 1,
                child: NetworkVisualMedia(
                  url: photo.photoUrl!.trim(),
                  contentType: _contentTypeFromUrl(photo.photoUrl!.trim()),
                  allowInlineVideo: true,
                  autoplay: false,
                  looping: true,
                  startMuted: true,
                  showControls: false,
                  allowPlaybackSpeedChanging: false,
                  showVideoBadge: true,
                  fit: BoxFit.cover,
                  placeholderLabel: 'Не удалось загрузить медиа',
                ),
              ),
            )
          else
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(
                horizontal: 14,
                vertical: 18,
              ),
              decoration: BoxDecoration(
                color: AppColors.surfaceGlass(0.18),
                borderRadius: BorderRadius.circular(16),
              ),
              child: const Row(
                children: [
                  Icon(
                    Icons.image_not_supported_outlined,
                    color: AppColors.textSecondary,
                    size: 20,
                  ),
                  SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'Превью публикации недоступно',
                      style: TextStyle(
                        color: AppColors.textSecondary,
                        height: 1.35,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          const SizedBox(height: 12),
          Text(
            'Автор: ${photo.authorDisplayName}',
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w700,
            ),
          ),
          if (hasGroup) ...[
            const SizedBox(height: 6),
            Text(
              'Группа: ${photo.groupName!.trim()}',
              style: const TextStyle(
                color: AppColors.textSecondary,
                height: 1.35,
              ),
            ),
          ],
          if (hasCaption) ...[
            const SizedBox(height: 10),
            Text(
              photo.caption!.trim(),
              style: const TextStyle(
                color: AppColors.textPrimary,
                height: 1.45,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _CommentPreviewCard extends StatelessWidget {
  final ReportCommentPreview comment;
  final ReportPhotoPreview? photo;

  const _CommentPreviewCard({
    required this.comment,
    required this.photo,
  });

  @override
  Widget build(BuildContext context) {
    final hasParentPreview =
        (comment.parentCommentTextPreview ?? '').trim().isNotEmpty;
    final hasPhotoCaption = (photo?.caption ?? '').trim().isNotEmpty;

    return Container(
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
      decoration: BoxDecoration(
        color: AppColors.surfaceGlass(0.18),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Комментарий',
            style: TextStyle(
              color: AppColors.textPrimary,
              fontSize: 16,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 10),
          Text(
            'Автор: ${comment.authorDisplayName}',
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'Username: @${comment.authorUserName}',
            style: const TextStyle(
              color: AppColors.textSecondary,
              height: 1.35,
            ),
          ),
          if (hasParentPreview) ...[
            const SizedBox(height: 10),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(
                horizontal: 12,
                vertical: 10,
              ),
              decoration: BoxDecoration(
                color: AppColors.card.withValues(alpha: 0.72),
                borderRadius: BorderRadius.circular(14),
              ),
              child: Text(
                'Ответ на: ${comment.parentCommentTextPreview!.trim()}',
                style: const TextStyle(
                  color: AppColors.textSecondary,
                  height: 1.35,
                ),
              ),
            ),
          ],
          const SizedBox(height: 10),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(
              horizontal: 12,
              vertical: 10,
            ),
            decoration: BoxDecoration(
              color: AppColors.card.withValues(alpha: 0.46),
              borderRadius: BorderRadius.circular(14),
            ),
            child: Text(
              comment.text.trim(),
              style: const TextStyle(
                color: AppColors.textPrimary,
                height: 1.42,
              ),
            ),
          ),
          if (photo != null) ...[
            const SizedBox(height: 14),
            const Text(
              'Контекст публикации',
              style: TextStyle(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 8),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(
                horizontal: 12,
                vertical: 10,
              ),
              decoration: BoxDecoration(
                color: AppColors.card.withValues(alpha: 0.52),
                borderRadius: BorderRadius.circular(14),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Автор фото: ${photo!.authorDisplayName}',
                    style: const TextStyle(
                      color: AppColors.textSecondary,
                      height: 1.35,
                    ),
                  ),
                  if (hasPhotoCaption) ...[
                    const SizedBox(height: 6),
                    Text(
                      photo!.caption!.trim(),
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        height: 1.35,
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _UserPreviewCard extends StatelessWidget {
  final ReportUserPreview user;

  const _UserPreviewCard({
    required this.user,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surfaceGlass(0.18),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          CircleAvatar(
            radius: 26,
            backgroundColor: AppColors.card,
            backgroundImage: (user.profilePhotoUrl ?? '').isNotEmpty
                ? NetworkImage(user.profilePhotoUrl!)
                : null,
            child: (user.profilePhotoUrl ?? '').isEmpty
                ? const Icon(
                    Icons.person_outline_rounded,
                    color: AppColors.textSecondary,
                  )
                : null,
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  user.displayName,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 16,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  '@${user.userName}',
                  style: const TextStyle(color: AppColors.textSecondary),
                ),
                const SizedBox(height: 6),
                Text(
                  user.isActive ? 'Аккаунт активен' : 'Аккаунт деактивирован',
                  style: TextStyle(
                    color: user.isActive
                        ? AppColors.textSecondary
                        : Colors.redAccent,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 10),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    _StatBadge(
                      label: 'Всего жалоб',
                      value: user.reportsAgainstCount.toString(),
                    ),
                    _StatBadge(
                      label: 'Новых',
                      value: user.pendingReportsAgainstCount.toString(),
                    ),
                    _StatBadge(
                      label: 'Решённых',
                      value: user.resolvedReportsAgainstCount.toString(),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _StatBadge extends StatelessWidget {
  final String label;
  final String value;

  const _StatBadge({
    required this.label,
    required this.value,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
      decoration: BoxDecoration(
        color: AppColors.card.withValues(alpha: 0.62),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(
          color: AppColors.border.withValues(alpha: 0.55),
        ),
      ),
      child: Text(
        '$label: $value',
        style: const TextStyle(
          color: AppColors.textSecondary,
          fontSize: 11.5,
          fontWeight: FontWeight.w700,
          height: 1.2,
        ),
      ),
    );
  }
}

class _ActionsCard extends StatelessWidget {
  final bool processing;
  final List<_DecisionAction> actions;
  final ReportStatusOption status;
  final ValueChanged<_DecisionAction> onApply;

  const _ActionsCard({
    required this.processing,
    required this.actions,
    required this.status,
    required this.onApply,
  });

  @override
  Widget build(BuildContext context) {
    final isPending = status == ReportStatusOption.pending;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surfaceGlass(0.18),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Решение модератора',
            style: TextStyle(
              color: AppColors.textPrimary,
              fontSize: 16,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 12),
          if (isPending)
            Wrap(
              spacing: 8,
              runSpacing: 8,
             children: actions.map((action) {
                final isDestructive =
                    action.action == ReviewReportActionOption.deletePhoto ||
                    action.action == ReviewReportActionOption.deleteComment ||
                    action.action == ReviewReportActionOption.deactivateUser;

                return FilledButton.tonal(
                  style: FilledButton.styleFrom(
                    minimumSize: const Size(0, 40),
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    backgroundColor: isDestructive
                        ? const Color(0xFF6A3B50).withValues(alpha: 0.88)
                        : AppColors.accentLight.withValues(alpha: 0.70),
                    foregroundColor: AppColors.textPrimary,
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                    visualDensity: VisualDensity.compact,
                  ),
                  onPressed: processing ? null : () => onApply(action),
                  child: Text(action.label),
                );
              }).toList(),
            )
          else
            const Text(
              'Для уже обработанной жалобы быстрые действия скрыты. При необходимости используйте пересмотр через апелляцию пользователя.',
              style: TextStyle(
                color: AppColors.textSecondary,
                height: 1.4,
              ),
            ),
          if (processing) ...[
            const SizedBox(height: 12),
            const LinearProgressIndicator(),
          ],
        ],
      ),
    );
  }
}