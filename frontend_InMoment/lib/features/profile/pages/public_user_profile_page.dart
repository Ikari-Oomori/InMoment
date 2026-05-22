import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_responsive_content.dart';
import '../../blocks/api/blocks_api.dart';
import '../../reports/models/report_reason_option.dart';
import '../../reports/pages/create_report_page.dart';
import '../api/profile_api.dart';
import '../models/public_user_profile.dart';

class _PublicProfilePageHeader extends StatelessWidget {
  final String title;
  final VoidCallback onBack;

  const _PublicProfilePageHeader({
    required this.title,
    required this.onBack,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 58,
      child: Row(
        children: [
          Material(
            color: AppColors.surfaceGlass(0.28),
            shape: const CircleBorder(),
            child: InkWell(
              onTap: onBack,
              customBorder: const CircleBorder(),
              child: const SizedBox(
                width: 44,
                height: 44,
                child: Icon(
                  Icons.arrow_back_ios_new_rounded,
                  size: 19,
                  color: AppColors.textPrimary,
                ),
              ),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              title,
              textAlign: TextAlign.center,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontSize: 18,
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


class PublicUserProfilePage extends StatefulWidget {
  final String userId;

  const PublicUserProfilePage({
    super.key,
    required this.userId,
  });

  @override
  State<PublicUserProfilePage> createState() => _PublicUserProfilePageState();
}

class _PublicUserProfilePageState extends State<PublicUserProfilePage> {
  final ProfileApi _profileApi = ProfileApi();
  final BlocksApi _blocksApi = BlocksApi();

  bool _loading = true;
  bool _busy = false;
  String? _error;
  PublicUserProfile? _profile;

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
      final profile = await _profileApi.getPublicProfile(widget.userId);

      if (!mounted) return;
      setState(() {
        _profile = profile;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить профиль пользователя.',
        );
      });
    }
  }

  Future<void> _toggleBlock() async {
    final profile = _profile;
    if (profile == null || _busy || !profile.canBlock || !profile.isActive) {
      return;
    }

    setState(() {
      _busy = true;
    });

    try {
      if (profile.isBlockedByMe) {
        await _blocksApi.unblockUser(profile.id);
        if (!mounted) return;
        setState(() {
          _profile = profile.copyWith(isBlockedByMe: false);
        });
        _showSnack('Пользователь разблокирован');
      } else {
        await _blocksApi.blockUser(profile.id);
        if (!mounted) return;
        setState(() {
          _profile = profile.copyWith(isBlockedByMe: true);
        });
        _showSnack('Пользователь заблокирован');
      }
    } catch (e) {
      if (!mounted) return;
      _showSnack(
        ApiError.normalize(
          e,
          fallback: 'Не удалось выполнить действие. Попробуйте ещё раз.',
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
        });
      }
    }
  }

  Future<void> _reportUser() async {
    final profile = _profile;
    if (profile == null || !profile.canReport || !profile.isActive) return;

    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => CreateReportPage(
          targetType: ReportTargetType.user,
          targetId: profile.id,
          titleText: 'Пожаловаться на пользователя',
          subtitleText:
              'Жалоба будет отправлена на проверку. Выберите причину и при необходимости добавьте описание.',
        ),
      ),
    );
  }

  void _showSnack(String text) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(text)),
    );
  }

  String _formatDate(DateTime dateTime) {
    final local = dateTime.toLocal();

    String two(int n) => n.toString().padLeft(2, '0');

    return '${two(local.day)}.${two(local.month)}.${local.year}';
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: Center(child: CircularProgressIndicator()),
      );
    }

    if (_error != null || _profile == null) {
      return Scaffold(
        backgroundColor: AppColors.background,
        body: SafeArea(
          child: InMomentResponsiveContent(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
              child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(
                  Icons.person_off_outlined,
                  color: AppColors.textSecondary,
                  size: 54,
                ),
                const SizedBox(height: 16),
                Text(
                  _error ?? 'Не удалось открыть профиль.',
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

    final profile = _profile!;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Container(
        decoration: const BoxDecoration(
          gradient: AppColors.pageBackgroundGradient,
        ),
        child: SafeArea(
          child: InMomentResponsiveContent(
            child: RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
                children: [
                  _PublicProfilePageHeader(
                    title: 'Профиль пользователя',
                    onBack: () => Navigator.of(context).maybePop(),
                  ),
                  const SizedBox(height: 22),
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: AppColors.surface,
                borderRadius: BorderRadius.circular(26),
                border: Border.all(color: AppColors.border),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  CircleAvatar(
                    radius: 34,
                    backgroundColor: AppColors.accent.withValues(alpha: 0.28),
                    backgroundImage: profile.isActive &&
                            profile.profilePhotoUrl != null &&
                            profile.profilePhotoUrl!.trim().isNotEmpty
                        ? NetworkImage(profile.profilePhotoUrl!)
                        : null,
                    child: !profile.isActive ||
                            profile.profilePhotoUrl == null ||
                            profile.profilePhotoUrl!.trim().isEmpty
                        ? Icon(
                            profile.isActive
                                ? Icons.person_outline_rounded
                                : Icons.person_off_rounded,
                            color: AppColors.textPrimary,
                            size: 28,
                          )
                        : null,
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Padding(
                      padding: const EdgeInsets.only(top: 2),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            profile.displayName,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: AppColors.textPrimary,
                              fontSize: 18,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                          if (profile.isActive &&
                              profile.userName.trim().isNotEmpty) ...[
                            const SizedBox(height: 4),
                            Text(
                              '@${profile.userName}',
                              style: const TextStyle(
                                color: AppColors.textSecondary,
                                fontSize: 13,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ],
                          const SizedBox(height: 8),
                          Text(
                            'С нами с ${_formatDate(profile.createdAt)}',
                            style: const TextStyle(
                              color: AppColors.textSecondary,
                              fontSize: 12,
                            ),
                          ),
                          if (!profile.isActive) ...[
                            const SizedBox(height: 10),
                            Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 10,
                                vertical: 6,
                              ),
                              decoration: BoxDecoration(
                                color: AppColors.surfaceLight,
                                borderRadius: BorderRadius.circular(999),
                                border: Border.all(color: AppColors.border),
                              ),
                              child: const Text(
                                'Аккаунт деактивирован',
                                style: TextStyle(
                                  color: AppColors.textSecondary,
                                  fontSize: 11,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                          ] else if (profile.hasBlockedMe) ...[
                            const SizedBox(height: 10),
                            Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 10,
                                vertical: 6,
                              ),
                              decoration: BoxDecoration(
                                color: AppColors.surfaceLight,
                                borderRadius: BorderRadius.circular(999),
                                border: Border.all(color: AppColors.border),
                              ),
                              child: const Text(
                                'Этот пользователь ограничил взаимодействие с вами',
                                style: TextStyle(
                                  color: AppColors.textSecondary,
                                  fontSize: 11,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 18),
            if (!profile.isActive)
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(24),
                  border: Border.all(color: AppColors.border),
                ),
                child: const Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Профиль недоступен',
                      style: TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 16,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    SizedBox(height: 10),
                    Text(
                      'Этот аккаунт деактивирован. Просмотр профиля и обычные действия с пользователем больше недоступны.',
                      style: TextStyle(
                        color: AppColors.textSecondary,
                        height: 1.45,
                      ),
                    ),
                  ],
                ),
              )
            else
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(24),
                  border: Border.all(color: AppColors.border),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Действия',
                      style: TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 16,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 14),
                    SizedBox(
                      width: double.infinity,
                      child: FilledButton.icon(
                        onPressed: _busy ? null : _toggleBlock,
                        icon: Icon(
                          profile.isBlockedByMe
                              ? Icons.lock_open_rounded
                              : Icons.block_outlined,
                        ),
                        label: Text(
                          profile.isBlockedByMe
                              ? 'Разблокировать'
                              : 'Заблокировать',
                        ),
                      ),
                    ),
                    const SizedBox(height: 10),
                    SizedBox(
                      width: double.infinity,
                      child: OutlinedButton.icon(
                        onPressed: _reportUser,
                        icon: const Icon(Icons.flag_outlined),
                        label: const Text('Пожаловаться'),
                      ),
                    ),
                  ],
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