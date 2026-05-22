import 'package:flutter/material.dart';

import '../../../app/app.dart';
import '../../../core/api/api_error.dart';
import '../../../core/controllers/app_session_controller.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_action_tile.dart';
import '../../../core/widgets/inmoment_async_state.dart';
import '../../../core/widgets/inmoment_feedback.dart';
import '../../../core/widgets/inmoment_glass_dialog.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../../core/widgets/inmoment_surface.dart';
import '../api/sessions_api.dart';
import '../models/session_item.dart';

class SessionsPage extends StatefulWidget {
  const SessionsPage({super.key});

  @override
  State<SessionsPage> createState() => _SessionsPageState();
}

class _SessionsPageState extends State<SessionsPage> {
  final _api = SessionsApi();

  bool _loading = true;
  String? _error;
  String? _revokingSessionId;
  List<SessionItem> _sessions = const [];

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
      final sessions = await _api.getSessions();

      if (!mounted) return;

      sessions.sort((a, b) {
        if (a.isCurrent && !b.isCurrent) return -1;
        if (!a.isCurrent && b.isCurrent) return 1;

        final aDate = a.lastActivityAt ?? DateTime.fromMillisecondsSinceEpoch(0);
        final bDate = b.lastActivityAt ?? DateTime.fromMillisecondsSinceEpoch(0);
        return bDate.compareTo(aDate);
      });

      setState(() {
        _sessions = sessions;
        _loading = false;
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _loading = false;
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить активные сессии.',
        );
      });
    }
  }

  Future<void> _revokeOthers() async {
    if (_revokingSessionId != null) return;

    final others = _otherSessions;
    if (others.isEmpty) return;

    final confirm = await showInMomentConfirmDialog(
      context: context,
      title: 'Завершить все остальные сессии',
      message:
          'Все сессии на других устройствах будут завершены. Текущее устройство останется активным.',
      confirmText: 'Завершить',
      danger: true,
    );

    if (confirm != true || !mounted) return;

    setState(() {
      _revokingSessionId = 'bulk';
    });

    try {
      final revokedCount = await _api.revokeOtherSessions();

      if (!mounted) return;

      await _load(silent: true);

      if (!mounted) return;

      InMomentFeedback.showSuccess(
        context,
        revokedCount > 0
            ? 'Завершено сессий: $revokedCount.'
            : 'Других активных сессий уже не осталось.',
      );
    } catch (e) {
      if (!mounted) return;

      InMomentFeedback.showError(
        context,
        ApiError.normalize(
          e,
          fallback: 'Не удалось завершить все остальные сессии.',
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _revokingSessionId = null;
        });
      }
    }
  }

  Future<void> _revoke(SessionItem session) async {
    if (_revokingSessionId != null) return;

    final confirm = await showInMomentConfirmDialog(
      context: context,
      title: 'Завершить сессию',
      message: session.isCurrent
          ? 'Это текущая сессия. После её завершения вы выйдете из аккаунта на этом устройстве.'
          : 'Эта сессия будет завершена на выбранном устройстве.',
      confirmText: 'Завершить',
      danger: true,
    );

    if (confirm != true || !mounted) return;

    setState(() {
      _revokingSessionId = session.id;
    });

    try {
      await _api.revokeSession(session.id);

      if (!mounted) return;

      if (session.isCurrent) {
        await AppSessionController.instance.logout();

        if (!mounted) return;

        Navigator.of(context).pushAndRemoveUntil(
          MaterialPageRoute(
            builder: (_) => const AppBootstrap(),
          ),
          (route) => false,
        );
        return;
      }

      setState(() {
        _sessions = _sessions.where((item) => item.id != session.id).toList();
      });

      InMomentFeedback.showSuccess(
        context,
        'Сессия завершена.',
      );

      await _load(silent: true);
    } catch (e) {
      if (!mounted) return;

      InMomentFeedback.showError(
        context,
        ApiError.normalize(
          e,
          fallback: 'Не удалось завершить выбранную сессию.',
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _revokingSessionId = null;
        });
      }
    }
  }

  List<SessionItem> get _currentSessions =>
      _sessions.where((s) => s.isCurrent).toList(growable: false);

  List<SessionItem> get _otherSessions =>
      _sessions.where((s) => !s.isCurrent).toList(growable: false);

  String _formatDate(DateTime? value) {
    if (value == null) return '—';

    final local = value.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');

    return '${two(local.day)}.${two(local.month)}.${local.year} • ${two(local.hour)}:${two(local.minute)}';
  }

  @override
  Widget build(BuildContext context) {
    final currentSessions = _currentSessions;
    final otherSessions = _otherSessions;
    final totalCount = _sessions.length;

    return InMomentPageShell(
      title: 'Устройства и сессии',
      showSurface: false,
      scrollable: false,
      contentPadding: EdgeInsets.zero,
      actions: [
        if (_revokingSessionId != null)
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
      child: InMomentAsyncState(
        isLoading: _loading,
        error: _error,
        onRetry: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(
            parent: BouncingScrollPhysics(),
          ),
          padding: const EdgeInsets.fromLTRB(10, 8, 10, 140),
          children: [
            _SessionsSummaryCard(
              totalCount: totalCount,
              currentCount: currentSessions.length,
              otherCount: otherSessions.length,
            ),
            const SizedBox(height: 18),
            _SectionTitle(
              title: 'Безопасность',
              subtitle:
                  'Проверьте устройства с доступом к аккаунту и завершите лишние сессии.',
            ),
            const SizedBox(height: 10),
            InMomentActionTile(
              icon: Icons.logout_rounded,
              title: 'Завершить все кроме текущей',
              subtitle: otherSessions.isEmpty
                  ? 'Других активных сессий нет'
                  : 'Завершить остальные устройства одним действием',
              onTap: otherSessions.isEmpty || _revokingSessionId != null
                  ? null
                  : _revokeOthers,
              danger: true,
              compact: true,
            ),
            const SizedBox(height: 10),
            const _SecurityNote(),
            const SizedBox(height: 18),
            _SectionTitle(
              title: 'Текущее устройство',
              subtitle: currentSessions.isEmpty
                  ? 'Текущее устройство не удалось определить.'
                  : 'Сессия, с которой вы сейчас работаете.',
            ),
            const SizedBox(height: 10),
            if (currentSessions.isEmpty)
              const _PlainTextCard(text: 'Нет данных о текущем устройстве.')
            else
              ...currentSessions.map(
                (item) => Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: _SessionCard(
                    item: item,
                    busy: _revokingSessionId == item.id,
                    formatDate: _formatDate,
                    onRevoke: () => _revoke(item),
                  ),
                ),
              ),
            const SizedBox(height: 8),
            _SectionTitle(
              title: 'Другие устройства',
              subtitle: otherSessions.isEmpty
                  ? 'Других активных устройств сейчас нет.'
                  : 'Остальные активные сессии, привязанные к аккаунту.',
            ),
            const SizedBox(height: 10),
            if (otherSessions.isEmpty)
              const _PlainTextCard(text: 'Других активных сессий не найдено.')
            else
              ...otherSessions.map(
                (item) => Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: _SessionCard(
                    item: item,
                    busy: _revokingSessionId == item.id,
                    formatDate: _formatDate,
                    onRevoke: () => _revoke(item),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  final String title;
  final String subtitle;

  const _SectionTitle({
    required this.title,
    required this.subtitle,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(left: 2, right: 2),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 16,
              fontWeight: FontWeight.w800,
              height: 1.12,
            ),
          ),
          const SizedBox(height: 5),
          Text(
            subtitle,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12.5,
              fontWeight: FontWeight.w600,
              height: 1.32,
            ),
          ),
        ],
      ),
    );
  }
}

class _SecurityNote extends StatelessWidget {
  const _SecurityNote();

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: InMomentSurfaceTone.base,
      borderRadius: BorderRadius.circular(20),
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
      showGlow: false,
      child: const Text(
        'После смены пароля или ручного завершения сессий часть устройств может потребовать повторный вход. Это ожидаемое поведение безопасности.',
        style: TextStyle(
          color: AppColors.textSecondary,
          fontSize: 12,
          height: 1.36,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

class _PlainTextCard extends StatelessWidget {
  final String text;

  const _PlainTextCard({
    required this.text,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: InMomentSurfaceTone.base,
      borderRadius: BorderRadius.circular(20),
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
      showGlow: false,
      child: Text(
        text,
        style: const TextStyle(
          color: AppColors.textSecondary,
          fontSize: 12.5,
          height: 1.35,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

class _SessionsSummaryCard extends StatelessWidget {
  final int totalCount;
  final int currentCount;
  final int otherCount;

  const _SessionsSummaryCard({
    required this.totalCount,
    required this.currentCount,
    required this.otherCount,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: InMomentSurfaceTone.overlay,
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Сводка по доступу',
            style: TextStyle(
              color: AppColors.textPrimary,
              fontSize: 14,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 12),
          _SummaryCompactRow(
            label: 'Всего активных сессий',
            value: totalCount.toString(),
          ),
          const SizedBox(height: 8),
          _SummaryCompactRow(
            label: 'Текущее устройство',
            value: currentCount.toString(),
          ),
          const SizedBox(height: 8),
          _SummaryCompactRow(
            label: 'Другие устройства',
            value: otherCount.toString(),
          ),
        ],
      ),
    );
  }
}

class _SummaryCompactRow extends StatelessWidget {
  final String label;
  final String value;

  const _SummaryCompactRow({
    required this.label,
    required this.value,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
      decoration: BoxDecoration(
        color: AppColors.surface.withValues(alpha: 0.38),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: [
          Expanded(
            child: Text(
              label,
              style: const TextStyle(
                color: AppColors.textSecondary,
                fontSize: 13,
                height: 1.25,
              ),
            ),
          ),
          const SizedBox(width: 12),
          Text(
            value,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 16,
              fontWeight: FontWeight.w800,
            ),
          ),
        ],
      ),
    );
  }
}

class _SessionCard extends StatelessWidget {
  final SessionItem item;
  final bool busy;
  final String Function(DateTime?) formatDate;
  final VoidCallback onRevoke;

  const _SessionCard({
    required this.item,
    required this.busy,
    required this.formatDate,
    required this.onRevoke,
  });

  IconData _leadingIcon() {
    if (item.isCurrent) return Icons.verified_user_rounded;
    if (item.isLikelyMobile) return Icons.smartphone_rounded;
    if (item.isLikelyDesktop) return Icons.laptop_mac_rounded;
    if (item.isLikelyWeb) return Icons.language_rounded;
    return Icons.devices_other_rounded;
  }

  @override
  Widget build(BuildContext context) {
    final metaLines = item.buildMetaLines(formatDate);

    return InMomentSurface(
      tone: item.isCurrent
          ? InMomentSurfaceTone.elevated
          : InMomentSurfaceTone.base,
      borderRadius: BorderRadius.circular(22),
      padding: const EdgeInsets.fromLTRB(14, 14, 14, 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(
                _leadingIcon(),
                color: item.isCurrent
                    ? AppColors.accentLight
                    : AppColors.textSecondary,
                size: 22,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      item.isCurrent ? 'Текущее устройство' : item.title,
                      style: const TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 14,
                        fontWeight: FontWeight.w800,
                        height: 1.25,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      item.isCurrent
                          ? (item.title == 'Текущее устройство'
                              ? 'Устройство'
                              : item.title)
                          : (item.secondaryLabel ?? 'Устройство'),
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 12.5,
                        height: 1.3,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              _SessionBadge(
                icon: item.isCurrent
                    ? Icons.check_circle_outline_rounded
                    : Icons.shield_outlined,
                text: item.isCurrent ? 'Активна сейчас' : 'Отдельная сессия',
                accent: item.isCurrent,
              ),
              if (item.environmentBadge != null)
                _SessionBadge(
                  icon: Icons.memory_rounded,
                  text: item.environmentBadge!,
                ),
              if (item.locationLabel != null)
                _SessionBadge(
                  icon: Icons.place_outlined,
                  text: item.locationLabel!,
                ),
              if (item.publicIpLabel != null)
                _SessionBadge(
                  icon: Icons.router_outlined,
                  text: item.publicIpLabel!,
                ),
            ],
          ),
          if (metaLines.isNotEmpty) ...[
            const SizedBox(height: 12),
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: metaLines
                  .map(
                    (line) => Padding(
                      padding: const EdgeInsets.only(bottom: 6),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Padding(
                            padding: EdgeInsets.only(top: 4),
                            child: Icon(
                              Icons.fiber_manual_record_rounded,
                              size: 10,
                              color: AppColors.textSecondary,
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              line,
                              style: const TextStyle(
                                color: AppColors.textSecondary,
                                fontSize: 12.5,
                                height: 1.34,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  )
                  .toList(),
            ),
          ],
          const SizedBox(height: 12),
          item.isCurrent
              ? SizedBox(
                  width: double.infinity,
                  child: OutlinedButton.icon(
                    onPressed: busy ? null : onRevoke,
                    icon: busy
                        ? const SizedBox(
                            width: 14,
                            height: 14,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.logout_rounded, size: 18),
                    label: const Text('Выйти на этом устройстве'),
                    style: OutlinedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 14,
                        vertical: 11,
                      ),
                    ),
                  ),
                )
              : TextButton.icon(
                  onPressed: busy ? null : onRevoke,
                  icon: busy
                      ? const SizedBox(
                          width: 14,
                          height: 14,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.logout_rounded, size: 18),
                  label: const Text('Завершить'),
                  style: TextButton.styleFrom(
                    foregroundColor: AppColors.textPrimary,
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 6,
                    ),
                    minimumSize: Size.zero,
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                    alignment: Alignment.centerLeft,
                  ),
                ),
        ],
      ),
    );
  }
}

class _SessionBadge extends StatelessWidget {
  final IconData icon;
  final String text;
  final bool accent;

  const _SessionBadge({
    required this.icon,
    required this.text,
    this.accent = false,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(10, 7, 10, 7),
      decoration: BoxDecoration(
        color: accent
            ? AppColors.accent.withValues(alpha: 0.22)
            : AppColors.surface.withValues(alpha: 0.55),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(
          color: accent
              ? AppColors.accentLight.withValues(alpha: 0.40)
              : AppColors.border,
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            icon,
            size: 14,
            color: accent ? AppColors.accentLight : AppColors.textSecondary,
          ),
          const SizedBox(width: 6),
          Flexible(
            child: Text(
              text,
              style: TextStyle(
                color: accent ? AppColors.textPrimary : AppColors.textSecondary,
                fontSize: 12,
                fontWeight: FontWeight.w600,
                height: 1.2,
              ),
            ),
          ),
        ],
      ),
    );
  }
}