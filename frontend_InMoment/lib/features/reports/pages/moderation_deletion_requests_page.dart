import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_async_state.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../../core/widgets/inmoment_section.dart';
import '../../account/api/account_api.dart';
import '../../account/models/account_deletion_request.dart';

class ModerationDeletionRequestsPage extends StatefulWidget {
  const ModerationDeletionRequestsPage({super.key});

  @override
  State<ModerationDeletionRequestsPage> createState() =>
      _ModerationDeletionRequestsPageState();
}

class _ModerationDeletionRequestsPageState
    extends State<ModerationDeletionRequestsPage> {
  final AccountApi _api = AccountApi();

  bool _loading = true;
  String? _error;
  String? _processingRequestId;
  List<AccountDeletionRequest> _items = const [];
  int? _statusFilter;

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
      final items = await _api.getModerationDeletionRequests(
        statusCode: _statusFilter,
      );

      if (!mounted) return;

      setState(() {
        _items = items
          ..sort(
            (a, b) => b.requestedAtUtc.millisecondsSinceEpoch.compareTo(
              a.requestedAtUtc.millisecondsSinceEpoch,
            ),
          );
        _loading = false;
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _loading = false;
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить запросы на удаление аккаунтов.',
        );
      });
    }
  }

  Future<void> _setStatusFilter(int? value) async {
    setState(() {
      _statusFilter = value;
    });

    await _load(silent: true);
  }

  String _formatDate(DateTime? value) {
    if (value == null) return '—';

    final local = value.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');

    return '${two(local.day)}.${two(local.month)}.${local.year} • ${two(local.hour)}:${two(local.minute)}';
  }

  Color _statusColor(AccountDeletionRequest request) {
    switch (request.statusCode) {
      case 1:
        return AppColors.accentLight;
      case 2:
        return AppColors.textPrimary;
      case 3:
        return Colors.greenAccent.shade100;
      case 4:
        return Colors.orangeAccent.shade100;
      case 5:
        return Colors.blueGrey.shade200;
      default:
        return AppColors.textSecondary;
    }
  }

  bool _canModerate(AccountDeletionRequest request) {
    return request.statusCode == 1 || request.statusCode == 2;
  }

  Future<String?> _askForNote({
    required String title,
    String? initialValue,
    String? helperText,
  }) async {
    final controller = TextEditingController(text: initialValue ?? '');

    final result = await showDialog<String?>(
      context: context,
      builder: (context) {
        return AlertDialog(
          backgroundColor: AppColors.surface,
          title: Text(
            title,
            style: const TextStyle(color: AppColors.textPrimary),
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (helperText != null && helperText.trim().isNotEmpty) ...[
                Text(
                  helperText,
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    height: 1.35,
                  ),
                ),
                const SizedBox(height: 12),
              ],
              TextField(
                controller: controller,
                minLines: 3,
                maxLines: 6,
                style: const TextStyle(color: AppColors.textPrimary),
                decoration: const InputDecoration(
                  hintText: 'Комментарий модератора',
                ),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(null),
              child: const Text('Отмена'),
            ),
            FilledButton(
              onPressed: () => Navigator.of(context).pop(controller.text.trim()),
              child: const Text('Сохранить'),
            ),
          ],
        );
      },
    );

    controller.dispose();
    return result;
  }

  Future<void> _applyAction({
    required AccountDeletionRequest item,
    required int statusCode,
    required bool permanentlyDeleteNow,
    required String dialogTitle,
    required String helperText,
    required String successMessage,
  }) async {
    if (_processingRequestId != null) return;

    final note = await _askForNote(
      title: dialogTitle,
      initialValue: item.processingNote,
      helperText: helperText,
    );

    if (note == null || !mounted) return;

    setState(() {
      _processingRequestId = item.id;
    });

    try {
      await _api.reviewDeletionRequest(
        requestId: item.id,
        statusCode: statusCode,
        processingNote: note,
        permanentlyDeleteNow: permanentlyDeleteNow,
      );

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(successMessage)),
      );

      await _load(silent: true);
    } catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось обработать запрос.',
            ),
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _processingRequestId = null;
        });
      }
    }
  }

  Widget _buildFilterChip({
    required String label,
    required bool selected,
    required VoidCallback onTap,
  }) {
    return ChoiceChip(
      label: Text(label),
      selected: selected,
      onSelected: (_) => onTap(),
      labelStyle: TextStyle(
        color: selected ? AppColors.textPrimary : AppColors.textSecondary,
        fontWeight: FontWeight.w700,
      ),
      selectedColor: AppColors.accent.withValues(alpha: 0.24),
      backgroundColor: AppColors.card.withValues(alpha: 0.36),
      side: BorderSide(
        color: selected
            ? AppColors.accentLight.withValues(alpha: 0.5)
            : AppColors.border.withValues(alpha: 0.3),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return InMomentPageShell(
      title: 'Удаление аккаунтов',
      actions: [
        IconButton(
          onPressed: () => _load(silent: false),
          icon: const Icon(
            Icons.refresh_rounded,
            color: AppColors.textPrimary,
          ),
        ),
      ],
      child: InMomentAsyncState(
        isLoading: _loading,
        error: _error,
        onRetry: _load,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            InMomentSection(
              title: 'Фильтры',
              subtitle:
                  'Обработка privacy-запросов на удаление аккаунтов внутри общей модераторской зоны.',
              child: Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  _buildFilterChip(
                    label: 'Все',
                    selected: _statusFilter == null,
                    onTap: () => _setStatusFilter(null),
                  ),
                  _buildFilterChip(
                    label: 'Новые',
                    selected: _statusFilter == 1,
                    onTap: () => _setStatusFilter(1),
                  ),
                  _buildFilterChip(
                    label: 'В работе',
                    selected: _statusFilter == 2,
                    onTap: () => _setStatusFilter(2),
                  ),
                  _buildFilterChip(
                    label: 'Завершённые',
                    selected: _statusFilter == 3,
                    onTap: () => _setStatusFilter(3),
                  ),
                  _buildFilterChip(
                    label: 'Отклонённые',
                    selected: _statusFilter == 4,
                    onTap: () => _setStatusFilter(4),
                  ),
                  _buildFilterChip(
                    label: 'Отменённые',
                    selected: _statusFilter == 5,
                    onTap: () => _setStatusFilter(5),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            if (_items.isEmpty)
              const InMomentSection(
                title: 'Пока пусто',
                child: Text(
                  'Запросов на удаление аккаунтов пока нет.',
                  style: TextStyle(
                    color: AppColors.textSecondary,
                    height: 1.4,
                  ),
                ),
              )
            else
              ..._items.map((item) {
                final busy = _processingRequestId == item.id;

                return Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: InMomentSection(
                    title: item.requestedUserName,
                    subtitle: item.requestedEmail,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: Align(
                                alignment: Alignment.centerLeft,
                                child: Container(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: 12,
                                    vertical: 7,
                                  ),
                                  decoration: BoxDecoration(
                                    color: _statusColor(item).withValues(alpha: 0.22),
                                    borderRadius: BorderRadius.circular(999),
                                    border: Border.all(
                                      color: _statusColor(item).withValues(alpha: 0.44),
                                    ),
                                  ),
                                  child: Text(
                                    item.statusLabel,
                                    style: const TextStyle(
                                      color: AppColors.textPrimary,
                                      fontWeight: FontWeight.w800,
                                      fontSize: 13,
                                    ),
                                  ),
                                ),
                              ),
                            ),
                            if (busy)
                              const SizedBox(
                                width: 18,
                                height: 18,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              ),
                          ],
                        ),
                        const SizedBox(height: 12),
                        _InfoRow(
                          label: 'Создан',
                          value: _formatDate(item.requestedAtUtc),
                        ),
                        _InfoRow(
                          label: 'Обновлён',
                          value: _formatDate(item.updatedAtUtc),
                        ),
                        _InfoRow(
                          label: 'Обработан',
                          value: _formatDate(item.processedAtUtc),
                        ),
                        if ((item.note ?? '').trim().isNotEmpty) ...[
                          const SizedBox(height: 10),
                          Text(
                            'Комментарий пользователя',
                            style: TextStyle(
                              color: AppColors.textSecondary.withValues(alpha: 0.92),
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            item.note!.trim(),
                            style: const TextStyle(
                              color: AppColors.textPrimary,
                              height: 1.42,
                            ),
                          ),
                        ],
                        if ((item.processingNote ?? '').trim().isNotEmpty) ...[
                          const SizedBox(height: 10),
                          Text(
                            'Комментарий модератора',
                            style: TextStyle(
                              color: AppColors.textSecondary.withValues(alpha: 0.92),
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            item.processingNote!.trim(),
                            style: const TextStyle(
                              color: AppColors.textPrimary,
                              height: 1.42,
                            ),
                          ),
                        ],
                        if (_canModerate(item)) ...[
                          const SizedBox(height: 14),
                          Wrap(
                            spacing: 8,
                            runSpacing: 8,
                            children: [
                              OutlinedButton.icon(
                                onPressed: busy
                                    ? null
                                    : () => _applyAction(
                                          item: item,
                                          statusCode: 2,
                                          permanentlyDeleteNow: false,
                                          dialogTitle: 'Перевести в работу',
                                          helperText:
                                              'Добавьте внутренний комментарий модератора. Пользователь увидит обновлённый статус запроса.',
                                          successMessage:
                                              'Запрос переведён в работу.',
                                        ),
                                icon: const Icon(Icons.schedule_rounded),
                                label: const Text('В работу'),
                              ),
                              OutlinedButton.icon(
                                onPressed: busy
                                    ? null
                                    : () => _applyAction(
                                          item: item,
                                          statusCode: 4,
                                          permanentlyDeleteNow: false,
                                          dialogTitle: 'Отклонить запрос',
                                          helperText:
                                              'Укажите причину отклонения. Она будет сохранена в истории запроса.',
                                          successMessage:
                                              'Запрос отклонён.',
                                        ),
                                icon: const Icon(Icons.close_rounded),
                                label: const Text('Отклонить'),
                              ),
                              FilledButton.icon(
                                onPressed: busy
                                    ? null
                                    : () => _applyAction(
                                          item: item,
                                          statusCode: 3,
                                          permanentlyDeleteNow: true,
                                          dialogTitle: 'Выполнить удаление аккаунта',
                                          helperText:
                                              'Это действие завершит запрос и сразу выполнит фактическое удаление аккаунта.',
                                          successMessage:
                                              'Удаление аккаунта выполнено.',
                                        ),
                                icon: const Icon(Icons.delete_forever_rounded),
                                label: const Text('Удалить аккаунт'),
                              ),
                            ],
                          ),
                        ],
                      ],
                    ),
                  ),
                );
              }),
          ],
        ),
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  final String label;
  final String value;

  const _InfoRow({
    required this.label,
    required this.value,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Text(
              label,
              style: const TextStyle(
                color: AppColors.textSecondary,
              ),
            ),
          ),
          const SizedBox(width: 12),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.right,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}