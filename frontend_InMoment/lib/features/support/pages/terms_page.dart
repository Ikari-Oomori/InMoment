import 'package:flutter/material.dart';

import '../../../core/config/app_contacts.dart';
import '../../../core/config/app_legal.dart';
import '../../../core/config/app_links.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/app_external_actions.dart';
import '../../../core/widgets/inmoment_action_tile.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../../core/widgets/inmoment_surface.dart';
import 'data_deletion_page.dart';
import 'policy_page.dart';

class TermsPage extends StatelessWidget {
  const TermsPage({super.key});

  @override
  Widget build(BuildContext context) {
    return InMomentPageShell(
      title: 'Условия использования',
      showSurface: false,
      scrollable: false,
      contentPadding: EdgeInsets.zero,
      child: ListView(
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(10, 8, 10, 140),
        children: [
          const _LegalHeader(
            label: 'Версия ${AppLegal.termsVersion} · ${AppLegal.termsEffectiveDate}',
            text:
                'InMoment — приватное приложение для обмена визуальным контентом внутри закрытых групп. Использование сервиса означает согласие с базовыми правилами взаимодействия, ограничениями доступа, требованиями к контенту и мерами по безопасности.',
          ),
          const SizedBox(height: 16),
          const _DocumentBlock(
            title: 'Основные правила',
            text:
                'Пользователь обязан использовать приложение добросовестно, не публиковать запрещённый контент, не пытаться обходить ограничения доступа, не нарушать приватность других участников и не использовать сервис для злоупотреблений, спама или атак на инфраструктуру.',
          ),
          const SizedBox(height: 10),
          const _DocumentBlock(
            title: 'Контент и группы',
            text:
                'Контент внутри InMoment предназначен для участников закрытых групп. Права доступа определяются членством в группе, приглашениями и внутренними правилами сервиса. Нарушения могут приводить к ограничениям, удалению контента, блокировкам и дополнительной проверке.',
          ),
          const SizedBox(height: 10),
          const _DocumentBlock(
            title: 'Ограничение ответственности',
            text:
                'Сервис предоставляется по мере развития и доработки продукта. Некоторые функции, ссылки, процессы поддержки и публикационные материалы могут находиться в промежуточном состоянии, особенно в тестовых и предрелизных сборках.',
          ),
          const SizedBox(height: 18),
          const _SectionLabel('Переходы'),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.public_rounded,
            title: 'Открыть публичные terms',
            subtitle: AppLinks.termsUrl,
            onTap: () => AppExternalActions.openUrl(
              context,
              url: AppLinks.termsUrl,
              errorMessage: 'Не удалось открыть terms.',
            ),
            compact: true,
          ),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.policy_outlined,
            title: 'Политика конфиденциальности',
            subtitle: 'Открыть внутри приложения',
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const PolicyPage()),
              );
            },
            compact: true,
          ),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.delete_outline_rounded,
            title: 'Удаление аккаунта и данных',
            subtitle: 'Открыть отдельный процесс',
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const DataDeletionPage()),
              );
            },
            compact: true,
          ),
          const SizedBox(height: 18),
          const _SectionLabel('Контакты'),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.gavel_rounded,
            title: AppContacts.legalLabel,
            subtitle: AppContacts.legalEmail,
            onTap: () => AppExternalActions.openMail(
              context,
              email: AppContacts.legalEmail,
              subject: 'InMoment — вопрос по условиям использования',
              body: [
                'Здравствуйте.',
                '',
                'Опишите ваш вопрос по условиям использования:',
                '',
              ].join('\n'),
            ),
            compact: true,
          ),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.mail_outline_rounded,
            title: AppContacts.supportLabel,
            subtitle: AppContacts.supportEmail,
            onTap: () => AppExternalActions.openMail(
              context,
              email: AppContacts.supportEmail,
              subject: 'InMoment — вопрос по правилам сервиса',
              body: [
                'Здравствуйте.',
                '',
                'Опишите ваш вопрос по правилам сервиса:',
                '',
              ].join('\n'),
            ),
            compact: true,
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

class _SectionLabel extends StatelessWidget {
  final String text;

  const _SectionLabel(this.text);

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        color: AppColors.textPrimary,
        fontSize: 14.5,
        fontWeight: FontWeight.w700,
        height: 1.15,
        letterSpacing: -0.08,
      ),
    );
  }
}