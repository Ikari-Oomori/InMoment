import 'package:flutter/material.dart';

import '../../../core/config/app_contacts.dart';
import '../../../core/config/app_legal.dart';
import '../../../core/config/app_links.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/app_external_actions.dart';
import '../../../core/widgets/inmoment_action_tile.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../../core/widgets/inmoment_surface.dart';
import 'policy_page.dart';
import 'terms_page.dart';

class DataDeletionPage extends StatelessWidget {
  const DataDeletionPage({super.key});

  Future<void> _openDeletionMail(BuildContext context) async {
    await AppExternalActions.openMail(
      context,
      email: AppContacts.privacyEmail,
      subject: 'InMoment — запрос на удаление аккаунта и данных',
      body: [
        'Здравствуйте.',
        '',
        'Хочу отправить запрос на удаление аккаунта и связанных данных.',
        '',
        'Email аккаунта:',
        '',
        'Username:',
        '',
        'Причина запроса (необязательно):',
        '',
        'Дополнительные сведения:',
        '',
      ].join('\n'),
    );
  }

  @override
  Widget build(BuildContext context) {
    return InMomentPageShell(
      title: 'Удаление аккаунта и данных',
      showSurface: false,
      scrollable: false,
      contentPadding: EdgeInsets.zero,
      child: ListView(
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(10, 8, 10, 140),
        children: [
          const _LegalHeader(
            label:
                'Версия ${AppLegal.dataDeletionVersion} · ${AppLegal.dataDeletionEffectiveDate}',
            text: AppLegal.deletionSummary,
          ),
          const SizedBox(height: 16),
          const _DocumentBlock(
            title: 'Как это работает',
            text:
                'Запрос на удаление аккаунта и данных рассматривается как отдельный процесс. В отдельных случаях может понадобиться дополнительная проверка, чтобы подтвердить право на удаление и избежать злоупотреблений.',
          ),
          const SizedBox(height: 10),
          const _DocumentBlock(
            title: 'Что может храниться дольше',
            text: AppLegal.accountDeletionProcessSummary,
          ),
          const SizedBox(height: 18),
          const _SectionLabel('Действия'),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.mail_outline_rounded,
            title: 'Отправить запрос по email',
            subtitle: AppContacts.privacyEmail,
            onTap: () => _openDeletionMail(context),
            compact: true,
          ),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.public_rounded,
            title: 'Открыть публичную страницу удаления данных',
            subtitle: AppLinks.dataDeletionUrl,
            onTap: () => AppExternalActions.openUrl(
              context,
              url: AppLinks.dataDeletionUrl,
              errorMessage: 'Не удалось открыть страницу удаления данных.',
            ),
            compact: true,
          ),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.copy_rounded,
            title: 'Скопировать email для запросов',
            subtitle: AppContacts.privacyEmail,
            onTap: () => AppExternalActions.copyText(
              context,
              text: AppContacts.privacyEmail,
              successMessage: 'Email для запросов скопирован.',
            ),
            compact: true,
          ),
          const SizedBox(height: 18),
          const _SectionLabel('Связанные документы'),
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
            icon: Icons.description_outlined,
            title: 'Условия использования',
            subtitle: 'Открыть внутри приложения',
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const TermsPage()),
              );
            },
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
