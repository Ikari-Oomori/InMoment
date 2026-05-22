import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../../core/widgets/inmoment_surface.dart';
import '../api/notifications_api.dart';
import '../models/notification_settings.dart';

class NotificationSettingsPage extends StatefulWidget {
  const NotificationSettingsPage({super.key});

  @override
  State<NotificationSettingsPage> createState() =>
      _NotificationSettingsPageState();
}

class _NotificationSettingsPageState extends State<NotificationSettingsPage> {
  final NotificationsApi _api = NotificationsApi();

  bool _loading = true;
  bool _saving = false;
  String? _error;
  NotificationSettingsModel? _settings;

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
      final data = await _api.getSettings();

      if (!mounted) return;
      setState(() {
        _settings = data;
        _loading = false;
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить. Попробуйте ещё раз.',
        );
      });
    }
  }

  Future<void> _save(
    NotificationSettingsModel next, {
    required String successMessage,
  }) async {
    if (_saving) return;

    final previous = _settings;

    setState(() {
      _saving = true;
      _settings = next;
    });

    try {
      final saved = await _api.updateSettings(next);

      if (!mounted) return;
      setState(() {
        _settings = saved;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(successMessage)),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _settings = previous;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось сохранить настройки уведомлений.',
            ),
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _saving = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final settings = _settings;

    if (_loading) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: Center(child: CircularProgressIndicator()),
      );
    }

    if (_error != null || settings == null) {
      return InMomentPageShell(
        title: 'Уведомления',
        showSurface: false,
        child: _NotificationErrorState(
          message: _error ?? 'Не удалось загрузить настройки уведомлений.',
          onRetry: _load,
        ),
      );
    }

    final pushEnabled = settings.pushEnabled;

    return InMomentPageShell(
      title: 'Уведомления',
      showSurface: false,
      scrollable: false,
      contentPadding: EdgeInsets.zero,
      actions: [
        if (_saving)
          const SizedBox(
            width: 18,
            height: 18,
            child: CircularProgressIndicator(strokeWidth: 2),
          ),
      ],
      child: RefreshIndicator(
        onRefresh: () => _load(silent: true),
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(
            parent: BouncingScrollPhysics(),
          ),
          padding: const EdgeInsets.fromLTRB(10, 6, 10, 140),
          children: [
            _SwitchTile(
              title: 'Push-уведомления',
              subtitle: 'Общие уведомления приложения',
              value: settings.pushEnabled,
              enabled: !_saving,
              onChanged: (value) async {
                final next = settings.copyWith(
                  pushEnabled: value,
                  pushGroupInvitations: value ? settings.pushGroupInvitations : false,
                  pushComments: value ? settings.pushComments : false,
                  pushReactions: value ? settings.pushReactions : false,
                  pushReplies: value ? settings.pushReplies : false,
                  pushMentions: value ? settings.pushMentions : false,
                  pushPosts: value ? settings.pushPosts : false,
                  pushRetention: value ? settings.pushRetention : false,
                  pushProductUpdates: value ? settings.pushProductUpdates : false,
                );

                await _save(
                  next,
                  successMessage: value
                      ? 'Push-уведомления включены'
                      : 'Push-уведомления отключены',
                );
              },
            ),
            _SwitchTile(
              title: 'Приглашения',
              subtitle: 'Новые приглашения в группы',
              value: settings.pushGroupInvitations,
              enabled: !_saving && pushEnabled,
              onChanged: (value) async {
                await _save(
                  settings.copyWith(pushGroupInvitations: value),
                  successMessage: 'Настройка приглашений сохранена',
                );
              },
            ),
            _SwitchTile(
              title: 'Комментарии',
              subtitle: 'Комментарии под публикациями',
              value: settings.pushComments,
              enabled: !_saving && pushEnabled,
              onChanged: (value) async {
                await _save(
                  settings.copyWith(pushComments: value),
                  successMessage: 'Настройка комментариев сохранена',
                );
              },
            ),
            _SwitchTile(
              title: 'Ответы',
              subtitle: 'Ответы на ваши комментарии',
              value: settings.pushReplies,
              enabled: !_saving && pushEnabled,
              onChanged: (value) async {
                await _save(
                  settings.copyWith(pushReplies: value),
                  successMessage: 'Настройка ответов сохранена',
                );
              },
            ),
            _SwitchTile(
              title: 'Реакции',
              subtitle: 'Реакции на публикации и комментарии',
              value: settings.pushReactions,
              enabled: !_saving && pushEnabled,
              onChanged: (value) async {
                await _save(
                  settings.copyWith(pushReactions: value),
                  successMessage: 'Настройка реакций сохранена',
                );
              },
            ),
            _SwitchTile(
              title: 'Упоминания',
              subtitle: 'Уведомления об упоминаниях',
              value: settings.pushMentions,
              enabled: !_saving && pushEnabled,
              onChanged: (value) async {
                await _save(
                  settings.copyWith(pushMentions: value),
                  successMessage: 'Настройка упоминаний сохранена',
                );
              },
            ),
            _SwitchTile(
              title: 'Новые публикации',
              subtitle: 'Когда в группах появляются новые фото',
              value: settings.pushPosts,
              enabled: !_saving && pushEnabled,
              onChanged: (value) async {
                await _save(
                  settings.copyWith(pushPosts: value),
                  successMessage: 'Настройка публикаций сохранена',
                );
              },
            ),
            _SwitchTile(
              title: 'Напоминания',
              subtitle: 'Возвращение в приложение и важные поводы',
              value: settings.pushRetention,
              enabled: !_saving && pushEnabled,
              onChanged: (value) async {
                await _save(
                  settings.copyWith(pushRetention: value),
                  successMessage: 'Настройка напоминаний сохранена',
                );
              },
            ),
            _SwitchTile(
              title: 'Обновления приложения',
              subtitle: 'Новые версии и важные изменения',
              value: settings.pushProductUpdates,
              enabled: !_saving && pushEnabled,
              isLast: true,
              onChanged: (value) async {
                await _save(
                  settings.copyWith(pushProductUpdates: value),
                  successMessage: 'Настройка обновлений сохранена',
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}

class _SwitchTile extends StatelessWidget {
  final String title;
  final String subtitle;
  final bool value;
  final bool enabled;
  final bool isLast;
  final ValueChanged<bool> onChanged;

  const _SwitchTile({
    required this.title,
    required this.subtitle,
    required this.value,
    required this.enabled,
    required this.onChanged,
    this.isLast = false,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: isLast ? 0 : 10),
      child: InMomentSurface(
        tone: InMomentSurfaceTone.base,
        borderRadius: BorderRadius.circular(20),
        padding: const EdgeInsets.fromLTRB(14, 10, 8, 10),
        showGlow: false,
        child: Row(
          children: [
            Expanded(
              child: Opacity(
                opacity: enabled ? 1 : 0.5,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 13.2,
                        fontWeight: FontWeight.w700,
                        height: 1.1,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      subtitle,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 11.2,
                        height: 1.22,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(width: 8),
            Transform.scale(
              scale: 0.72,
              child: Switch(
                value: value,
                onChanged: enabled ? onChanged : null,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _NotificationErrorState extends StatelessWidget {
  final String message;
  final VoidCallback onRetry;

  const _NotificationErrorState({
    required this.message,
    required this.onRetry,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: InMomentSurfaceTone.elevated,
      borderRadius: BorderRadius.circular(24),
      padding: const EdgeInsets.all(18),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 14,
              height: 1.35,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 14),
          FilledButton(
            onPressed: onRetry,
            child: const Text('Повторить'),
          ),
        ],
      ),
    );
  }
}