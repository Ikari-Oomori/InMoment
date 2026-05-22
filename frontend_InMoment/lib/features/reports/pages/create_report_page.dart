import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../../core/widgets/inmoment_section.dart';
import '../../../core/widgets/inmoment_surface.dart';
import '../api/reports_api.dart';
import '../models/report_reason_option.dart';

class CreateReportPage extends StatefulWidget {
  final ReportTargetType targetType;
  final String targetId;
  final String titleText;
  final String subtitleText;

  const CreateReportPage({
    super.key,
    required this.targetType,
    required this.targetId,
    required this.titleText,
    required this.subtitleText,
  });

  @override
  State<CreateReportPage> createState() => _CreateReportPageState();
}

class _CreateReportPageState extends State<CreateReportPage> {
  final ReportsApi _api = ReportsApi();
  final TextEditingController _descriptionController = TextEditingController();

  ReportReasonOption _selectedReason = ReportReasonOption.spam;
  bool _loading = false;
  String? _error;

  Future<void> _submit() async {
    if (_loading) return;

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      await _api.createReport(
        targetType: widget.targetType,
        targetId: widget.targetId,
        reason: _selectedReason,
        description: _descriptionController.text,
      );

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Жалоба отправлена')),
      );

      Navigator.of(context).pop(true);
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось отправить жалобу. Попробуйте ещё раз.',
        );
      });
    } finally {
      if (mounted) {
        setState(() {
          _loading = false;
        });
      }
    }
  }

  @override
  void dispose() {
    _descriptionController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return InMomentPageShell(
      title: 'Жалоба',
      showSurface: false,
      contentPadding: const EdgeInsets.fromLTRB(16, 12, 16, 120),
      bottom: FilledButton.icon(
        onPressed: _loading ? null : _submit,
        icon: _loading
            ? const SizedBox(
                width: 18,
                height: 18,
                child: CircularProgressIndicator(strokeWidth: 2),
              )
            : const Icon(Icons.flag_rounded),
        label: const Text('Отправить жалобу'),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          InMomentSurface(
            tone: InMomentSurfaceTone.elevated,
            borderRadius: BorderRadius.circular(AppTheme.radiusLg),
            padding: const EdgeInsets.fromLTRB(16, 15, 16, 16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  widget.titleText,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 17,
                    fontWeight: FontWeight.w800,
                    height: 1.16,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  widget.subtitleText,
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                    height: 1.42,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          InMomentSection(
            title: 'Причина',
            subtitle: 'Выбери наиболее подходящий вариант',
            contentPadding: const EdgeInsets.fromLTRB(8, 8, 8, 8),
            child: Column(
              children: [
                for (final reason in ReportReasonOption.values)
                  _ReasonTile(
                    reason: reason,
                    selected: _selectedReason == reason,
                    enabled: !_loading,
                    onTap: () {
                      setState(() {
                        _selectedReason = reason;
                      });
                    },
                  ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          InMomentSection(
            title: 'Описание',
            subtitle: 'Можно добавить детали, чтобы модерации было проще разобраться',
            child: TextField(
              controller: _descriptionController,
              minLines: 4,
              maxLines: 7,
              enabled: !_loading,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontSize: 14,
                height: 1.35,
              ),
              decoration: const InputDecoration(
                hintText: 'Опишите ситуацию подробнее, если это нужно',
              ),
            ),
          ),
          if (_error != null) ...[
            const SizedBox(height: 16),
            InMomentSurface(
              tone: InMomentSurfaceTone.danger,
              borderRadius: BorderRadius.circular(AppTheme.radiusMd),
              padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
              child: Text(
                _error!,
                style: const TextStyle(
                  color: AppColors.error,
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  height: 1.35,
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _ReasonTile extends StatelessWidget {
  final ReportReasonOption reason;
  final bool selected;
  final bool enabled;
  final VoidCallback onTap;

  const _ReasonTile({
    required this.reason,
    required this.selected,
    required this.enabled,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 7),
      child: InMomentSurface(
        tone: selected ? InMomentSurfaceTone.accent : InMomentSurfaceTone.overlay,
        borderRadius: BorderRadius.circular(18),
        padding: const EdgeInsets.fromLTRB(13, 11, 12, 11),
        onTap: enabled ? onTap : null,
        showGlow: selected,
        child: Row(
          children: [
            Icon(
              selected
                  ? Icons.radio_button_checked_rounded
                  : Icons.radio_button_off_rounded,
              color: selected ? AppColors.accentSoft : AppColors.textMuted,
              size: 20,
            ),
            const SizedBox(width: 11),
            Expanded(
              child: Text(
                reason.label,
                style: TextStyle(
                  color: selected
                      ? AppColors.textPrimary
                      : AppColors.textSecondary,
                  fontSize: 13.4,
                  fontWeight: selected ? FontWeight.w800 : FontWeight.w600,
                  height: 1.22,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}