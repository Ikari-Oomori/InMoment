import 'package:flutter/material.dart';

import '../../../app/app.dart';
import '../../../core/api/api_error.dart';
import '../../../core/config/app_contacts.dart';
import '../../../core/config/app_legal.dart';
import '../../../core/controllers/app_session_controller.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/app_external_actions.dart';
import '../../../core/widgets/inmoment_action_tile.dart';
import '../../../core/widgets/inmoment_async_state.dart';
import '../../../core/widgets/inmoment_feedback.dart';
import '../../../core/widgets/inmoment_glass_dialog.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../../core/widgets/inmoment_section.dart';
import '../api/account_api.dart';
import '../models/account_data_summary.dart';
import '../models/account_deletion_request.dart';

class DeleteAccountPage extends StatefulWidget {
  const DeleteAccountPage({super.key});

  @override
  State<DeleteAccountPage> createState() => _DeleteAccountPageState();
}

class _DeleteAccountPageState extends State<DeleteAccountPage> {
  final AccountApi _api = AccountApi();

  bool _loading = true;
  bool _deactivating = false;
  bool _permanentlyDeleting = false;
  bool _creatingDeletionRequest = false;
  String? _error;

  AccountDataSummary? _summary;
  AccountDeletionRequest? _deletionRequest;

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
      final results = await Future.wait<dynamic>([
        _api.getDataSummary(),
        _api.getDeletionRequest(),
      ]);

      if (!mounted) return;

      setState(() {
        _summary = results[0] as AccountDataSummary;
        _deletionRequest = results[1] as AccountDeletionRequest?;
        _loading = false;
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить данные аккаунта.',
        );
        _loading = false;
      });
    }
  }

  Future<void> _createDeletionRequest() async {
    if (_creatingDeletionRequest) return;
    if (_deletionRequest?.isActive == true) return;

    final noteController = TextEditingController();

    final confirm = await showDialog<bool>(
      context: context,
      barrierColor: Colors.black.withValues(alpha: 0.54),
      builder: (context) {
        return InMomentGlassDialog(
          title: 'Официальный запрос на удаление данных',
          message:
              'Будет создан отдельный серверный запрос на удаление аккаунта и связанных данных. Этот сценарий нужен как официальный privacy/legal-канал.',
          confirmText: 'Отправить',
          content: TextField(
            controller: noteController,
            maxLength: 500,
            maxLines: 4,
            style: const TextStyle(color: AppColors.textPrimary),
            decoration: InputDecoration(
              hintText: 'Примечание (необязательно)',
              hintStyle: const TextStyle(color: AppColors.textSecondary),
              filled: true,
              fillColor: AppColors.white.withValues(alpha: 0.035),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(18),
                borderSide: BorderSide(color: AppColors.softStroke(0.12)),
              ),
              focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(18),
                borderSide: BorderSide(
                  color: AppColors.accentSoft.withValues(alpha: 0.42),
                ),
              ),
            ),
          ),
        );
      },
    );

    if (confirm != true || !mounted) return;

    setState(() {
      _creatingDeletionRequest = true;
    });

    try {
      final created = await _api.createDeletionRequest(
        note: noteController.text,
      );

      if (!mounted) return;

      setState(() {
        _deletionRequest = created;
      });

      InMomentFeedback.showSuccess(
        context,
        'Официальный запрос на удаление данных отправлен.',
      );
    } catch (e) {
      if (!mounted) return;

      InMomentFeedback.showError(
        context,
        ApiError.normalize(
          e,
          fallback: 'Не удалось отправить запрос на удаление аккаунта и данных.',
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _creatingDeletionRequest = false;
        });
      }
    }
  }

  Future<void> _deactivate() async {
    if (_deactivating) return;

    final confirm = await showInMomentConfirmDialog(
      context: context,
      title: 'Деактивировать аккаунт',
      message:
          'Деактивация — это мягкий временный сценарий. Профиль будет отключён, активные сессии завершены, но позже вы сможете вернуться, снова выполнив вход.',
      confirmText: 'Деактивировать',
    );

    if (confirm != true || !mounted) return;

    setState(() {
      _deactivating = true;
    });

    try {
      await _api.deactivateAccount();
      await AppSessionController.instance.logout();

      if (!mounted) return;

      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(
          builder: (_) => const AppBootstrap(),
        ),
        (route) => false,
      );
    } catch (e) {
      if (!mounted) return;

      InMomentFeedback.showError(
        context,
        ApiError.normalize(
          e,
          fallback: 'Не удалось деактивировать аккаунт.',
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _deactivating = false;
        });
      }
    }
  }

  Future<void> _permanentlyDelete() async {
    if (_permanentlyDeleting) return;

    final confirm = await showInMomentConfirmDialog(
      context: context,
      title: 'Удалить аккаунт навсегда',
      message:
          'Это необратимое действие. После полного удаления аккаунта вернуться под теми же данными входа уже не получится.',
      confirmText: 'Удалить',
      danger: true,
    );

    if (confirm != true || !mounted) return;

    setState(() {
      _permanentlyDeleting = true;
    });

    try {
      await _api.permanentlyDeleteAccount('DELETE');
      await AppSessionController.instance.logout();

      if (!mounted) return;

      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(
          builder: (_) => const AppBootstrap(),
        ),
        (route) => false,
      );
    } catch (e) {
      if (!mounted) return;

      InMomentFeedback.showError(
        context,
        ApiError.normalize(
          e,
          fallback: 'Не удалось удалить аккаунт навсегда.',
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _permanentlyDeleting = false;
        });
      }
    }
  }

  Future<void> _openPrivacyRequest() async {
    await AppExternalActions.openMail(
      context,
      email: AppContacts.privacyEmail,
      subject: 'InMoment — запрос на удаление аккаунта и данных',
      body: [
        'Здравствуйте.',
        '',
        'Прошу обработать запрос на удаление аккаунта и связанных данных.',
        '',
        'Email аккаунта:',
        '',
        'Username:',
        '',
        'Дополнительная информация:',
        '',
      ].join('\n'),
    );
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
        return Colors.redAccent.shade100;
      default:
        return AppColors.textSecondary;
    }
  }

  @override
  Widget build(BuildContext context) {
    final summary = _summary;
    final deletionRequest = _deletionRequest;

    return InMomentPageShell(
      title: 'Удаление аккаунта',
      showSurface: false,
      child: InMomentAsyncState(
        isLoading: _loading,
        error: _error,
        onRetry: _load,
        child: summary == null
            ? const SizedBox.shrink()
            : Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const InMomentSection(
                    title: 'Что важно знать',
                    child: Text(
                      'Здесь доступны три разных сценария: временная деактивация аккаунта, полное удаление аккаунта навсегда и отдельный официальный запрос на удаление данных. Это разные действия с разными последствиями.',
                      style: TextStyle(
                        color: AppColors.textSecondary,
                        height: 1.45,
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  InMomentSection(
                    title: 'Что затронет состояние аккаунта',
                    child: Column(
                      children: [
                        _SummaryRow(label: 'Группы', value: '${summary.groupsCount}'),
                        _SummaryRow(
                          label: 'Собственные группы',
                          value: '${summary.ownedGroupsCount}',
                        ),
                        _SummaryRow(
                          label: 'Публикации',
                          value: '${summary.photosCount}',
                        ),
                        _SummaryRow(
                          label: 'Комментарии',
                          value: '${summary.commentsCount}',
                        ),
                        _SummaryRow(
                          label: 'Реакции',
                          value: '${summary.reactionsCount}',
                        ),
                        _SummaryRow(
                          label: 'Активные сессии',
                          value: '${summary.activeSessionsCount}',
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  const InMomentSection(
                    title: 'Хранение и ограничения',
                    child: Text(
                      AppLegal.deletionSummary,
                      style: TextStyle(
                        color: AppColors.textSecondary,
                        height: 1.45,
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  InMomentSection(
                    title: 'Деактивация аккаунта',
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'Деактивация — временный сценарий. Профиль отключается, активные сессии завершаются, но позже вы можете вернуться, снова войдя в аккаунт.',
                          style: TextStyle(
                            color: AppColors.textSecondary,
                            height: 1.42,
                          ),
                        ),
                        const SizedBox(height: 16),
                        SizedBox(
                          width: double.infinity,
                          child: FilledButton.icon(
                            onPressed: _deactivating ? null : _deactivate,
                            icon: _deactivating
                                ? const SizedBox(
                                    width: 16,
                                    height: 16,
                                    child: CircularProgressIndicator(strokeWidth: 2),
                                  )
                                : const Icon(Icons.pause_circle_outline_rounded),
                            label: Text(
                              _deactivating
                                  ? 'Деактивируем...'
                                  : 'Деактивировать аккаунт',
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  InMomentSection(
                    title: 'Удаление аккаунта навсегда',
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'Это необратимый сценарий. После полного удаления аккаунта вернуться с теми же данными входа уже не получится.',
                          style: TextStyle(
                            color: AppColors.textSecondary,
                            height: 1.42,
                          ),
                        ),
                        const SizedBox(height: 16),
                        SizedBox(
                          width: double.infinity,
                          child: FilledButton.icon(
                            onPressed: _permanentlyDeleting
                                ? null
                                : _permanentlyDelete,
                            icon: _permanentlyDeleting
                                ? const SizedBox(
                                    width: 16,
                                    height: 16,
                                    child: CircularProgressIndicator(strokeWidth: 2),
                                  )
                                : const Icon(Icons.delete_forever_rounded),
                            label: Text(
                              _permanentlyDeleting
                                  ? 'Удаляем...'
                                  : 'Удалить аккаунт навсегда',
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  InMomentSection(
                    title: 'Официальный запрос на удаление данных',
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        if (deletionRequest == null) ...[
                          const Text(
                            'Если нужен отдельный официальный privacy/legal-запрос на удаление аккаунта и данных, его можно отправить прямо из приложения.',
                            style: TextStyle(
                              color: AppColors.textSecondary,
                              height: 1.42,
                            ),
                          ),
                          const SizedBox(height: 16),
                          SizedBox(
                            width: double.infinity,
                            child: FilledButton.icon(
                              onPressed: _creatingDeletionRequest
                                  ? null
                                  : _createDeletionRequest,
                              icon: _creatingDeletionRequest
                                  ? const SizedBox(
                                      width: 16,
                                      height: 16,
                                      child: CircularProgressIndicator(strokeWidth: 2),
                                    )
                                  : const Icon(Icons.mail_outline_rounded),
                              label: Text(
                                _creatingDeletionRequest
                                    ? 'Отправляем...'
                                    : 'Отправить официальный запрос',
                              ),
                            ),
                          ),
                        ] else ...[
                          Row(
                            children: [
                              Expanded(
                                child: Text(
                                  deletionRequest.statusLabel,
                                  style: TextStyle(
                                    color: _statusColor(deletionRequest),
                                    fontWeight: FontWeight.w800,
                                    fontSize: 16,
                                  ),
                                ),
                              ),
                              if (deletionRequest.isActive)
                                const Icon(
                                  Icons.schedule_rounded,
                                  color: AppColors.textSecondary,
                                  size: 18,
                                ),
                            ],
                          ),
                          const SizedBox(height: 12),
                          _SummaryRow(
                            label: 'Создан',
                            value: _formatDate(deletionRequest.requestedAtUtc),
                          ),
                          _SummaryRow(
                            label: 'Обновлён',
                            value: _formatDate(deletionRequest.updatedAtUtc),
                          ),
                          _SummaryRow(
                            label: 'Email',
                            value: deletionRequest.requestedEmail,
                          ),
                          _SummaryRow(
                            label: 'Username',
                            value: deletionRequest.requestedUserName,
                          ),
                          if (deletionRequest.processedAtUtc != null)
                            _SummaryRow(
                              label: 'Обработан',
                              value: _formatDate(deletionRequest.processedAtUtc),
                            ),
                          if ((deletionRequest.note ?? '').trim().isNotEmpty)
                            Padding(
                              padding: const EdgeInsets.only(top: 4),
                              child: Text(
                                'Примечание: ${deletionRequest.note!.trim()}',
                                style: const TextStyle(
                                  color: AppColors.textSecondary,
                                  height: 1.4,
                                ),
                              ),
                            ),
                          if ((deletionRequest.processingNote ?? '').trim().isNotEmpty)
                            Padding(
                              padding: const EdgeInsets.only(top: 8),
                              child: Text(
                                'Ответ модерации: ${deletionRequest.processingNote!.trim()}',
                                style: const TextStyle(
                                  color: AppColors.textSecondary,
                                  height: 1.4,
                                ),
                              ),
                            ),
                          const SizedBox(height: 16),
                          if (deletionRequest.isActive)
                            const Text(
                              'Сейчас уже есть активный запрос. Повторный запрос до завершения текущего не требуется.',
                              style: TextStyle(
                                color: AppColors.textSecondary,
                                height: 1.42,
                              ),
                            )
                          else
                            SizedBox(
                              width: double.infinity,
                              child: FilledButton.icon(
                                onPressed: _creatingDeletionRequest
                                    ? null
                                    : _createDeletionRequest,
                                icon: _creatingDeletionRequest
                                    ? const SizedBox(
                                        width: 16,
                                        height: 16,
                                        child: CircularProgressIndicator(strokeWidth: 2),
                                      )
                                    : const Icon(Icons.refresh_rounded),
                                label: Text(
                                  _creatingDeletionRequest
                                      ? 'Отправляем...'
                                      : 'Отправить новый запрос',
                                ),
                              ),
                            ),
                        ],
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  InMomentSection(
                    title: 'Официальный privacy-канал',
                    child: Column(
                      children: [
                        InMomentActionTile(
                          icon: Icons.mail_outline_rounded,
                          title: 'Написать на privacy',
                          subtitle: AppContacts.privacyEmail,
                          onTap: _openPrivacyRequest,
                          compact: true,
                        ),
                        const SizedBox(height: 8),
                        InMomentActionTile(
                          icon: Icons.copy_rounded,
                          title: 'Скопировать email privacy',
                          subtitle: AppContacts.privacyEmail,
                          onTap: () => AppExternalActions.copyText(
                            context,
                            text: AppContacts.privacyEmail,
                            successMessage: 'Email privacy скопирован.',
                          ),
                          compact: true,
                        ),
                      ],
                    ),
                  ),
                ],
              ),
      ),
    );
  }
}

class _SummaryRow extends StatelessWidget {
  final String label;
  final String value;

  const _SummaryRow({
    required this.label,
    required this.value,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
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