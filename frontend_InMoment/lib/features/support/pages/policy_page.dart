import 'package:flutter/material.dart';

import '../../../core/config/app_legal.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../../core/widgets/inmoment_surface.dart';

class PolicyPage extends StatelessWidget {
  const PolicyPage({super.key});

  @override
  Widget build(BuildContext context) {
    return InMomentPageShell(
      title: 'Политика и данные',
      showSurface: false,
      scrollable: false,
      contentPadding: EdgeInsets.zero,
      child: ListView(
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(10, 8, 10, 140),
        children: const [
          _LegalHeader(
            label:
                'Версия ${AppLegal.privacyPolicyVersion} · ${AppLegal.privacyPolicyEffectiveDate}',
            text: AppLegal.privacySummary,
          ),
          SizedBox(height: 16),
          _DocumentBlock(
            title: 'Какие данные используются',
            text:
                'Для работы приложения могут использоваться данные аккаунта, профиля, публикаций, комментариев, реакций, приглашений, уведомлений, сессий, устройств, push-токенов, обращений в поддержку и технических журналов.',
          ),
          SizedBox(height: 10),
          _DocumentBlock(
            title: 'Зачем это нужно',
            text:
                'Эти данные используются для аутентификации, показа контента внутри закрытых групп, доставки уведомлений, восстановления доступа, модерации, предотвращения злоупотреблений, обработки жалоб и технической поддержки.',
          ),
          SizedBox(height: 10),
          _DocumentBlock(
            title: 'Хранение и безопасность',
            text: AppLegal.retentionSummary,
          ),
        ],
      ),
    );
  }
}

class _LegalHeader extends StatelessWidget {
  final String label;
  final String text;

  const _LegalHeader({
    required this.label,
    required this.text,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            color: AppColors.accentLight,
            fontSize: 12.5,
            fontWeight: FontWeight.w700,
            height: 1.25,
          ),
        ),
        const SizedBox(height: 8),
        Text(
          text,
          style: const TextStyle(
            color: AppColors.textSecondary,
            fontSize: 13,
            fontWeight: FontWeight.w500,
            height: 1.42,
          ),
        ),
      ],
    );
  }
}

class _DocumentBlock extends StatelessWidget {
  final String title;
  final String text;

  const _DocumentBlock({
    required this.title,
    required this.text,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      blur: true,
      showGlow: false,
      tone: InMomentSurfaceTone.overlay,
      borderRadius: BorderRadius.circular(22),
      padding: const EdgeInsets.fromLTRB(15, 14, 15, 15),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 14.2,
              fontWeight: FontWeight.w700,
              height: 1.16,
              letterSpacing: -0.08,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            text,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12.8,
              fontWeight: FontWeight.w500,
              height: 1.42,
            ),
          ),
        ],
      ),
    );
  }
}
