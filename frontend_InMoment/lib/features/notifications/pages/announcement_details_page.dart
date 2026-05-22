import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/network_visual_media.dart';
import '../../../core/widgets/inmoment_responsive_content.dart';
import '../models/notification_item.dart';

class AnnouncementDetailsPage extends StatelessWidget {
  final NotificationItem item;

  const AnnouncementDetailsPage({
    super.key,
    required this.item,
  });

  @override
  Widget build(BuildContext context) {
    final text = item.announcementText?.trim().isNotEmpty == true
        ? item.announcementText!.trim()
        : item.previewText.trim();

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: InMomentResponsiveContent(
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 10, 16, 28),
            children: [
              Row(
                children: [
                  _CircleButton(
                    icon: Icons.close_rounded,
                    onTap: () => Navigator.of(context).pop(),
                  ),
                  const SizedBox(width: 12),
                  const Expanded(
                    child: Text(
                      'Системное уведомление',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 18,
                        fontWeight: FontWeight.w900,
                      ),
                    ),
                  ),
                  const SizedBox(width: 56),
                ],
              ),
              const SizedBox(height: 18),
              Container(
                padding: const EdgeInsets.all(18),
                decoration: BoxDecoration(
                  color: AppColors.surfaceDeep,
                  borderRadius: BorderRadius.circular(26),
                  border: Border.all(
                    color: AppColors.accent.withValues(alpha: 0.20),
                  ),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Icon(
                      Icons.campaign_rounded,
                      color: AppColors.textPrimary,
                      size: 32,
                    ),
                    const SizedBox(height: 14),
                    Text(
                      text,
                      style: const TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 16,
                        height: 1.42,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 14),
                    Text(
                      item.createdAtHumanized,
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 12,
                      ),
                    ),
                  ],
                ),
              ),
              if (item.announcementMediaUrl != null &&
                  item.announcementMediaUrl!.trim().isNotEmpty) ...[
                const SizedBox(height: 14),
                _AnnouncementMediaView(
                  url: item.announcementMediaUrl!,
                  contentType: item.announcementMediaContentType,
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _CircleButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onTap;

  const _CircleButton({
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
          ),
        ),
      ),
    );
  }
}

bool _isVideoContentType(String? value) {
  final normalized = (value ?? '').toLowerCase();
  return normalized.startsWith('video/');
}

class _AnnouncementMediaView extends StatelessWidget {
  final String url;
  final String? contentType;

  const _AnnouncementMediaView({
    required this.url,
    required this.contentType,
  });

  @override
  Widget build(BuildContext context) {
    final isVideo = _isVideoContentType(contentType);

    return Container(
    decoration: BoxDecoration(
      borderRadius: BorderRadius.circular(26),
      border: Border.all(
        color: AppColors.accent.withValues(alpha: 0.35),
        width: 1.2,
      ),
      boxShadow: [
        BoxShadow(
          color: AppColors.accent.withValues(alpha: 0.18),
          blurRadius: 18,
          offset: const Offset(0, 8),
        ),
      ],
    ),
    child:Padding(
      padding: const EdgeInsets.all(6),
      child: ClipRRect(
      borderRadius: BorderRadius.circular(26),
      child: Container(
        width: double.infinity,
        color: Colors.black,
        child: isVideo
            ? ConstrainedBox(
                constraints: BoxConstraints(
                  minHeight: 180,
                  maxHeight: MediaQuery.sizeOf(context).height * 0.58,
                ),
                child: AspectRatio(
                  aspectRatio: 16 / 9,
                  child: NetworkVisualMedia(
                    url: url,
                    contentType: contentType ?? 'video/mp4',
                    allowInlineVideo: true,
                    autoplay: false,
                    looping: true,
                    startMuted: true,
                    showControls: true,
                    allowPlaybackSpeedChanging: false,
                    showVideoBadge: true,
                    fit: BoxFit.contain,
                    placeholderLabel: 'Не удалось загрузить медиа',
                  ),
                ),
              )
            : _AdaptiveAnnouncementImage(url: url),
          ),
        ),
      ),
    );
  }
}

class _AdaptiveAnnouncementImage extends StatelessWidget {
  final String url;

  const _AdaptiveAnnouncementImage({
    required this.url,
  });

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final screen = MediaQuery.sizeOf(context);
        final maxWidth = constraints.maxWidth.isFinite
            ? constraints.maxWidth
            : screen.width - 32;

        return Image.network(
          url,
          fit: BoxFit.contain,
          width: maxWidth,
          frameBuilder: (context, child, frame, wasSynchronouslyLoaded) {
            if (wasSynchronouslyLoaded || frame != null) {
              return InteractiveViewer(
                minScale: 1,
                maxScale: 4,
                child: Center(child: child),
              );
            }

            return const SizedBox(
              height: 220,
              child: Center(
                child: CircularProgressIndicator(),
              ),
            );
          },
          errorBuilder: (_, _, _) {
            return const SizedBox(
              height: 220,
              child: Center(
                child: Text(
                  'Не удалось загрузить изображение',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: AppColors.textSecondary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            );
          },
        );
      },
    );
  }
}