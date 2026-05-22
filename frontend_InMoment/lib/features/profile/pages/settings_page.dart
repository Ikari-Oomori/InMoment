import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/api/api_error.dart';
import '../../../core/widgets/inmoment_async_state.dart';
import '../../../core/widgets/inmoment_feedback.dart';
import '../../../core/widgets/inmoment_action_tile.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../../core/widgets/inmoment_section.dart';
import '../../../core/widgets/inmoment_surface.dart';
import '../../account/pages/delete_account_page.dart';
import '../../blocks/pages/blocked_users_page.dart';
import '../../notifications/api/notifications_api.dart';
import '../../notifications/models/notification_settings.dart';
import '../../privacy/api/privacy_api.dart';
import '../../privacy/models/privacy_settings.dart';
import '../../privacy/pages/privacy_page.dart';
import '../../sessions/pages/sessions_page.dart';
import '../../support/pages/policy_page.dart';
import '../../support/pages/support_page.dart';
import 'change_password_page.dart';
import '../../support/pages/about_app_page.dart';

class SettingsPage extends StatefulWidget {
  const SettingsPage({super.key});

  @override
  State<SettingsPage> createState() => _SettingsPageState();
}

class _SettingsPageState extends State<SettingsPage> {
  final NotificationsApi _notificationsApi = NotificationsApi();
  final PrivacyApi _privacyApi = PrivacyApi();

  bool _loading = true;
  bool _saving = false;
  String? _error;

  NotificationSettingsModel? _notificationSettings;
  PrivacySettingsModel? _privacySettings;

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
      final results = await Future.wait([
        _notificationsApi.getSettings(),
        _privacyApi.getPrivacy(),
      ]);

      if (!mounted) return;

      setState(() {
        _notificationSettings = results[0] as NotificationSettingsModel;
        _privacySettings = results[1] as PrivacySettingsModel;
        _loading = false;
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _loading = false;
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить настройки.',
        );
      });
    }
  }

  Future<void> _saveNotifications(
    NotificationSettingsModel next, {
    required String successMessage,
  }) async {
    if (_saving) return;

    final previous = _notificationSettings;

    setState(() {
      _saving = true;
      _notificationSettings = next;
    });

    try {
      final saved = await _notificationsApi.updateSettings(next);

      if (!mounted) return;

      setState(() {
        _notificationSettings = saved;
      });

      InMomentFeedback.showSuccess(context, successMessage);
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _notificationSettings = previous;
      });

      InMomentFeedback.showError(
        context,
        ApiError.normalize(
          e,
          fallback: 'Не удалось сохранить настройки.',
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

  Future<void> _openSessionsPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const SessionsPage(),
      ),
    );
  }

  Future<void> _openPrivacyPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const PrivacyPage(),
      ),
    );

    if (!mounted) return;
    await _load(silent: true);
  }

  Future<void> _openBlockedUsersPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const BlockedUsersPage(),
      ),
    );
  }

  Future<void> _openSupportPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const SupportPage(),
      ),
    );
  }

  Future<void> _openPolicyPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const PolicyPage(),
      ),
    );
  }

  Future<void> _openAboutAppPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const AboutAppPage(),
      ),
    );
  }

  Future<void> _openDeleteAccountPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const DeleteAccountPage(),
      ),
    );
  }

  Future<void> _openChangePasswordPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const ChangePasswordPage(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final notificationSettings = _notificationSettings;
    final privacySettings = _privacySettings;

    if (_loading || _error != null || notificationSettings == null || privacySettings == null) {
      return InMomentPageShell(
        title: 'Настройки',
        child: InMomentAsyncState(
          isLoading: _loading,
          error: _error ?? (notificationSettings == null || privacySettings == null
              ? 'Не удалось загрузить настройки.'
              : null),
          onRetry: _load,
          child: const SizedBox.shrink(),
        ),
      );
    }

    final pushEnabled = notificationSettings.pushEnabled;

    return InMomentPageShell(
      title: 'Настройки',
      actions: [
        if (_saving)
          const Padding(
            padding: EdgeInsets.only(right: 10),
            child: Center(
              child: SizedBox(
                width: 18,
                height: 18,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            ),
          )
        else
          IconButton(
            tooltip: 'Обновить',
            onPressed: () => _load(silent: false),
            icon: const Icon(Icons.refresh_rounded),
            color: AppColors.textPrimary,
          ),
      ],
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _SettingsSummaryCard(
            notificationSettings: notificationSettings,
            privacySettings: privacySettings,
          ),
          const SizedBox(height: 12),
          _SectionCard(
            title: 'Помощь и документы',
            child: Column(
              children: [
                _ActionTile(
                  icon: Icons.support_agent_rounded,
                  title: 'Поддержка',
                  onTap: _openSupportPage,
                ),
                const SizedBox(height: 8),
                _ActionTile(
                  icon: Icons.policy_outlined,
                  title: 'Политика и данные',
                  onTap: _openPolicyPage,
                ),
                const SizedBox(height: 8),
                _ActionTile(
                  icon: Icons.info_outline_rounded,
                  title: 'О приложении',
                  onTap: _openAboutAppPage,
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          _SectionCard(
            title: 'Уведомления',
            child: Column(
              children: [
                _SwitchTile(
                  icon: Icons.notifications_active_outlined,
                  title: 'Push-уведомления',
                  value: notificationSettings.pushEnabled,
                  enabled: !_saving,
                  onChanged: (value) async {
                    final next = notificationSettings.copyWith(
                      pushEnabled: value,
                      pushGroupInvitations:
                          value ? notificationSettings.pushGroupInvitations : false,
                      pushComments:
                          value ? notificationSettings.pushComments : false,
                      pushReactions:
                          value ? notificationSettings.pushReactions : false,
                      pushReplies:
                          value ? notificationSettings.pushReplies : false,
                      pushMentions:
                          value ? notificationSettings.pushMentions : false,
                      pushPosts: value ? notificationSettings.pushPosts : false,
                      pushRetention:
                          value ? notificationSettings.pushRetention : false,
                      pushProductUpdates:
                          value ? notificationSettings.pushProductUpdates : false,
                    );

                    await _saveNotifications(
                      next,
                      successMessage: value
                          ? 'Push включены'
                          : 'Push отключены',
                    );
                  },
                ),
                const SizedBox(height: 8),
                _SwitchTile(
                  icon: Icons.group_add_outlined,
                  title: 'Приглашения',
                  value: notificationSettings.pushGroupInvitations,
                  enabled: !_saving && pushEnabled,
                  onChanged: (value) async {
                    await _saveNotifications(
                      notificationSettings.copyWith(
                        pushGroupInvitations: value,
                      ),
                      successMessage: 'Сохранено',
                    );
                  },
                ),
                const SizedBox(height: 8),
                _SwitchTile(
                  icon: Icons.mode_comment_outlined,
                  title: 'Комментарии',
                  value: notificationSettings.pushComments,
                  enabled: !_saving && pushEnabled,
                  onChanged: (value) async {
                    await _saveNotifications(
                      notificationSettings.copyWith(pushComments: value),
                      successMessage: 'Сохранено',
                    );
                  },
                ),
                const SizedBox(height: 8),
                _SwitchTile(
                  icon: Icons.reply_rounded,
                  title: 'Ответы',
                  value: notificationSettings.pushReplies,
                  enabled: !_saving && pushEnabled,
                  onChanged: (value) async {
                    await _saveNotifications(
                      notificationSettings.copyWith(pushReplies: value),
                      successMessage: 'Сохранено',
                    );
                  },
                ),
                const SizedBox(height: 8),
                _SwitchTile(
                  icon: Icons.favorite_border_rounded,
                  title: 'Реакции',
                  value: notificationSettings.pushReactions,
                  enabled: !_saving && pushEnabled,
                  onChanged: (value) async {
                    await _saveNotifications(
                      notificationSettings.copyWith(pushReactions: value),
                      successMessage: 'Сохранено',
                    );
                  },
                ),
                const SizedBox(height: 8),
                _SwitchTile(
                  icon: Icons.alternate_email_rounded,
                  title: 'Упоминания',
                  value: notificationSettings.pushMentions,
                  enabled: !_saving && pushEnabled,
                  onChanged: (value) async {
                    await _saveNotifications(
                      notificationSettings.copyWith(pushMentions: value),
                      successMessage: 'Сохранено',
                    );
                  },
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          _SectionCard(
            title: 'Профиль и безопасность',
            child: Column(
              children: [
                _ActionTile(
                  icon: Icons.shield_outlined,
                  title: 'Конфиденциальность',
                  onTap: _openPrivacyPage,
                ),
                const SizedBox(height: 8),
                _ActionTile(
                  icon: Icons.smartphone_rounded,
                  title: 'Устройства и сессии',
                  onTap: _openSessionsPage,
                ),
                const SizedBox(height: 8),
                _ActionTile(
                  icon: Icons.block_rounded,
                  title: 'Блокировки',
                  onTap: _openBlockedUsersPage,
                ),
                const SizedBox(height: 8),
                _ActionTile(
                  icon: Icons.password_rounded,
                  title: 'Сменить пароль',
                  onTap: _openChangePasswordPage,
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          _SectionCard(
            title: 'Опасная зона',
            child: Column(
              children: [
                _DangerTile(
                  icon: Icons.delete_outline_rounded,
                  title: 'Удалить аккаунт',
                  onTap: _openDeleteAccountPage,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SettingsSummaryCard extends StatelessWidget {
  final NotificationSettingsModel notificationSettings;
  final PrivacySettingsModel privacySettings;

  const _SettingsSummaryCard({
    required this.notificationSettings,
    required this.privacySettings,
  });

  @override
  Widget build(BuildContext context) {
    final notificationState = notificationSettings.pushEnabled
        ? 'Push включены'
        : 'Push отключены';

    final privacyState =
        'Поиск: ${privacySettings.discoverableBySearch ? 'вкл' : 'выкл'}';

    return InMomentSection(
      title: 'Краткий статус',
      child: Column(
        children: [
          _SummaryRow(label: 'Уведомления', value: notificationState),
          const SizedBox(height: 8),
          _SummaryRow(label: 'Приватность', value: privacyState),
          const SizedBox(height: 8),
          _SummaryRow(
            label: 'Приглашения',
            value: privacySettings.allowGroupInvitesFrom.label,
          ),
        ],
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
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 98,
          child: Text(
            label,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 13,
            ),
          ),
        ),
        Expanded(
          child: Text(
            value,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 14,
              height: 1.4,
            ),
          ),
        ),
      ],
    );
  }
}

class _SectionCard extends StatelessWidget {
  final String title;
  final Widget child;

  const _SectionCard({
    required this.title,
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSection(
      title: title,
      child: child,
    );
  }
}

class _SwitchTile extends StatelessWidget {
  final IconData icon;
  final String title;
  final bool value;
  final bool enabled;
  final ValueChanged<bool> onChanged;

  const _SwitchTile({
    required this.icon,
    required this.title,
    required this.value,
    required this.enabled,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: InMomentSurfaceTone.base,
      borderRadius: BorderRadius.circular(18),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      child: Row(
        children: [
          Icon(icon, color: AppColors.textSecondary, size: 19),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              title,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w700,
                fontSize: 14,
              ),
            ),
          ),
          Switch(
            value: value,
            onChanged: enabled ? onChanged : null,
          ),
        ],
      ),
    );
  }
}

class _ActionTile extends StatelessWidget {
  final IconData icon;
  final String title;
  final VoidCallback onTap;

  const _ActionTile({
    required this.icon,
    required this.title,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentActionTile(
      icon: icon,
      title: title,
      onTap: onTap,
      compact: true,
    );
  }
}

class _DangerTile extends StatelessWidget {
  final IconData icon;
  final String title;
  final VoidCallback onTap;

  const _DangerTile({
    required this.icon,
    required this.title,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentActionTile(
      icon: icon,
      title: title,
      onTap: onTap,
      danger: true,
      compact: true,
    );
  }
}