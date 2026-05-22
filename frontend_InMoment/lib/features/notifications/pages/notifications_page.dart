import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/network_visual_media.dart';
import '../../../core/widgets/inmoment_responsive_content.dart';
import '../services/notification_navigation.dart';
import '../controllers/notifications_controller.dart';
import '../models/notification_item.dart';
import '../../profile/pages/public_user_profile_page.dart';
import 'announcement_details_page.dart';

class NotificationsPage extends StatefulWidget {
  const NotificationsPage({super.key});

  @override
  State<NotificationsPage> createState() => _NotificationsPageState();
}

class _NotificationsPageState extends State<NotificationsPage> {
  final _controller = NotificationsController.instance;
  final _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();

    WidgetsBinding.instance.addPostFrameCallback((_) {
      _controller.loadInitial(force: true);
    });

    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController
      ..removeListener(_onScroll)
      ..dispose();
    super.dispose();
  }

  void _onScroll() {
    if (!_scrollController.hasClients) return;

    final position = _scrollController.position;
    if (position.pixels >= position.maxScrollExtent - 220) {
      _controller.loadMore();
    }
  }

  Future<void> _openNotification(NotificationItem item) async {
    if (!item.isRead) {
      await _controller.markRead(item.id);
    }

    if (!mounted) return;

    if (item.type == 20) {
      await Navigator.of(context).push(
        MaterialPageRoute(
          builder: (_) => AnnouncementDetailsPage(item: item),
        ),
      );
      return;
    }

    await NotificationNavigation.openFromItem(item);
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, _) {
        return Scaffold(
          backgroundColor: AppColors.background,
          body: SafeArea(
            child: InMomentResponsiveContent(
              child: Column(
                children: [
                  _NotificationsTopBar(
                    unreadCount: _controller.unreadCount,
                    canMarkAllRead: _controller.items.isNotEmpty &&
                        !_controller.markingAllRead,
                    markingAllRead: _controller.markingAllRead,
                    refreshing: _controller.loading,
                    onClose: () => Navigator.of(context).pop(),
                    onRefresh: _controller.refresh,
                    onMarkAllRead: _controller.markAllRead,
                  ),
                  Expanded(
                    child: _buildBody(),
                  ),
                ],
              ),
            ),
          ),
        );
      }
    );
  }

  Widget _buildBody() {
    if (_controller.loading && _controller.items.isEmpty) {
      return const Center(
        child: CircularProgressIndicator(),
      );
    }

    if (_controller.error != null && _controller.items.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(
                Icons.notifications_off_outlined,
                color: AppColors.textSecondary,
                size: 46,
              ),
              const SizedBox(height: 16),
              Text(
                'Не удалось загрузить уведомления.\n\n${_controller.error}',
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: AppColors.textPrimary,
                  height: 1.4,
                ),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: _controller.loading
                    ? null
                    : () => _controller.loadInitial(force: true),
                child: _controller.loading
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Повторить'),
              ),
            ],
          ),
        ),
      );
    }

    if (_controller.items.isEmpty) {
      return RefreshIndicator(
        onRefresh: _controller.refresh,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(24),
          children: const [
            SizedBox(height: 180),
            Icon(
              Icons.notifications_none_rounded,
              color: AppColors.textSecondary,
              size: 48,
            ),
            SizedBox(height: 14),
            Text(
              'Пока нет уведомлений',
              textAlign: TextAlign.center,
              style: TextStyle(
                color: AppColors.textPrimary,
                fontSize: 16,
                fontWeight: FontWeight.w700,
              ),
            ),
            SizedBox(height: 8),
            Text(
              'Когда кто-то отреагирует на фото, оставит комментарий, ответит вам или упомянет вас, это появится здесь.',
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

    final hasInlineError =
        _controller.error != null && _controller.error!.trim().isNotEmpty;

    return RefreshIndicator(
      onRefresh: _controller.refresh,
      child: ListView.separated(
        controller: _scrollController,
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(8, 6, 8, 24),
        itemCount: _controller.items.length +
            (_controller.loadingMore ? 1 : 0) +
            (hasInlineError ? 1 : 0),
        separatorBuilder: (_, _) => const SizedBox(height: 10),
        itemBuilder: (context, index) {
          if (index >= _controller.items.length) {
            if (_controller.loadingMore) {
              return const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Center(
                  child: CircularProgressIndicator(),
                ),
              );
            }

            return _NotificationsInlineError(
              message: _controller.error!,
              onRetry: _controller.loading
                  ? null
                  : () => _controller.loadInitial(force: true),
            );
          }

          final item = _controller.items[index];

          return _NotificationCard(
            item: item,
            onTap: item.isClickable ? () => _openNotification(item) : null,
            onActorTap: item.actorUserId == null || item.actorUserId!.isEmpty
                ? null
                : () {
                    Navigator.of(context).push(
                      MaterialPageRoute(
                        builder: (_) => PublicUserProfilePage(
                          userId: item.actorUserId!,
                        ),
                      ),
                    );
                  },
          );
        },
      ),
    );
  }
}

class _NotificationsInlineError extends StatelessWidget {
  final String message;
  final VoidCallback? onRetry;

  const _NotificationsInlineError({
    required this.message,
    required this.onRetry,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 14, 14, 14),
      decoration: BoxDecoration(
        color: AppColors.card.withValues(alpha: 0.72),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(
          color: AppColors.error.withValues(alpha: 0.18),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(
            Icons.error_outline_rounded,
            color: AppColors.textSecondary,
            size: 20,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              message,
              style: const TextStyle(
                color: AppColors.textSecondary,
                fontSize: 13,
                height: 1.35,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
          const SizedBox(width: 10),
          TextButton(
            onPressed: onRetry,
            child: const Text('Повторить'),
          ),
        ],
      ),
    );
  }
}

class _NotificationsTopBar extends StatelessWidget {
  final int unreadCount;
  final bool canMarkAllRead;
  final bool markingAllRead;
  final bool refreshing;
  final VoidCallback onClose;
  final Future<void> Function() onRefresh;
  final Future<void> Function() onMarkAllRead;

  const _NotificationsTopBar({
    required this.unreadCount,
    required this.canMarkAllRead,
    required this.markingAllRead,
    required this.refreshing,
    required this.onClose,
    required this.onRefresh,
    required this.onMarkAllRead,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 8),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          SizedBox(
            height: 48,
            child: Row(
              children: [
                _NotificationCircleButton(
                  icon: Icons.arrow_back_ios_new_rounded,
                  onTap: onClose,
                ),
                const SizedBox(width: 12),
                const Expanded(
                  child: Text(
                    'Уведомления',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    softWrap: false,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: AppColors.textPrimary,
                      fontSize: 24,
                      fontWeight: FontWeight.w800,
                      letterSpacing: -0.15,
                      height: 1.08,
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                refreshing
                    ? const SizedBox(
                        width: 40,
                        height: 40,
                        child: Center(
                          child: SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          ),
                        ),
                      )
                    : _NotificationCircleButton(
                        icon: Icons.refresh_rounded,
                        onTap: onRefresh,
                      ),
              ],
            ),
          ),
          if (canMarkAllRead || markingAllRead)
            Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  onPressed: canMarkAllRead ? onMarkAllRead : null,
                  style: TextButton.styleFrom(
                    padding: const EdgeInsets.symmetric(horizontal: 6),
                    minimumSize: const Size(0, 28),
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                    foregroundColor: AppColors.textSecondary,
                    disabledForegroundColor:
                        AppColors.textSecondary.withValues(alpha: 0.45),
                  ),
                  child: markingAllRead
                      ? const SizedBox(
                          width: 14,
                          height: 14,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text(
                          'Прочитать все',
                          maxLines: 1,
                          softWrap: false,
                          style: TextStyle(
                            color: AppColors.textSecondary,
                            fontSize: 12.5,
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _NotificationCircleButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onTap;

  const _NotificationCircleButton({
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.surface.withValues(alpha: 0.88),
      shape: const CircleBorder(),
      child: InkWell(
        customBorder: const CircleBorder(),
        onTap: onTap,
        child: SizedBox(
          width: 44,
          height: 44,
          child: Icon(
            icon,
            color: AppColors.textPrimary,
            size: 22,
          ),
        ),
      ),
    );
  }
}

class _NotificationCard extends StatelessWidget {
  final NotificationItem item;
  final VoidCallback? onTap;
  final VoidCallback? onActorTap;

  const _NotificationCard({
    required this.item,
    required this.onTap,
    required this.onActorTap,
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
    final actorName = item.actorDisplayName?.trim().isNotEmpty == true
        ? item.actorDisplayName!.trim()
        : (item.actorUserName?.trim().isNotEmpty == true
            ? '@${item.actorUserName!.trim()}'
            : 'Пользователь');
    final isAnnouncement = item.type == 20;
    final announcementMediaUrl = item.announcementMediaUrl;
    final mediaUrl = isAnnouncement
        ? announcementMediaUrl
        : (item.thumbnailUrl ?? item.photoUrl);

    return Material(
      color: isAnnouncement
          ? AppColors.surfaceDeep
          : (item.isRead ? AppColors.card : AppColors.surface),
      borderRadius: BorderRadius.circular(22),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(22),
        child: Container(
          padding: EdgeInsets.fromLTRB(
            isAnnouncement ? 16 : 14,
            14,
            isAnnouncement ? 16 : 14,
            14,
          ),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(22),
            border: Border.all(
              color: item.isRead
                  ? AppColors.border
                  : AppColors.accent.withValues(alpha: 0.45),
            ),
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
             isAnnouncement
                ? const CircleAvatar(
                    radius: 22,
                    backgroundColor: AppColors.background,
                    child: Icon(
                      Icons.campaign_rounded,
                      color: AppColors.textPrimary,
                      size: 22,
                    ),
                  )
                : GestureDetector(
                    onTap: onActorTap,
                    child: _ActorAvatar(
                      imageUrl: item.actorProfilePhotoUrl,
                      fallbackText: actorName,
                    ),
                  ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: GestureDetector(
                            onTap: onActorTap,
                            child: Text(
                              item.previewText,
                              maxLines: isAnnouncement ? 3 : 4,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                color: AppColors.textPrimary,
                                fontSize: 14,
                                fontWeight: FontWeight.w700,
                                height: 1.35,
                              ),
                            ),
                          ),
                        ),
                        if (!item.isRead) ...[
                          const SizedBox(width: 8),
                          Container(
                            width: 10,
                            height: 10,
                            margin: const EdgeInsets.only(top: 4),
                            decoration: const BoxDecoration(
                              color: AppColors.accentSecondary,
                              shape: BoxShape.circle,
                            ),
                          ),
                        ],
                      ],
                    ),
                    const SizedBox(height: 8),
                    Text(
                      item.createdAtHumanized,
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 12,
                      ),
                    ),
                    if (item.groupName != null &&
                        item.groupName!.trim().isNotEmpty) ...[
                      const SizedBox(height: 8),
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
                        child: Text(
                          item.groupName!,
                          style: const TextStyle(
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
              if (mediaUrl != null && mediaUrl.trim().isNotEmpty) ...[
                const SizedBox(width: 12),
                ClipRRect(
                  borderRadius: BorderRadius.circular(14),
                  child: SizedBox(
                    width: 58,
                    height: 58,
                    child: NetworkVisualMedia(
                      url: mediaUrl,
                      contentType: isAnnouncement
                          ? (item.announcementMediaContentType ?? _contentTypeFromUrl(mediaUrl))
                          : _contentTypeFromUrl(mediaUrl),
                      allowInlineVideo: true,
                      autoplay: false,
                      looping: true,
                      startMuted: true,
                      showControls: false,
                      allowPlaybackSpeedChanging: false,
                      showVideoBadge: false,
                      fit: BoxFit.cover,
                      placeholderLabel: 'Не удалось загрузить медиа',
                    ),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _ActorAvatar extends StatelessWidget {
  final String? imageUrl;
  final String fallbackText;

  const _ActorAvatar({
    required this.imageUrl,
    required this.fallbackText,
  });

  @override
  Widget build(BuildContext context) {
    if (imageUrl != null && imageUrl!.trim().isNotEmpty) {
      return CircleAvatar(
        radius: 22,
        backgroundImage: NetworkImage(imageUrl!),
      );
    }

    final normalized = fallbackText.trim();
    final letter = normalized.isNotEmpty ? normalized[0].toUpperCase() : 'U';

    return CircleAvatar(
      radius: 22,
      backgroundColor: AppColors.accent.withValues(alpha: 0.30),
      child: Text(
        letter,
        style: const TextStyle(
          color: AppColors.textPrimary,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}