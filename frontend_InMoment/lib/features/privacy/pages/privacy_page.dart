import 'package:flutter/material.dart';

import '../../../core/config/app_contacts.dart';
import '../../../core/config/app_links.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/app_external_actions.dart';
import '../../../core/widgets/inmoment_action_tile.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../support/pages/data_deletion_page.dart';
import '../../support/pages/policy_page.dart';

class PrivacyPage extends StatelessWidget {
  const PrivacyPage({super.key});

  @override
  Widget build(BuildContext context) {
    return InMomentPageShell(
      title: 'Конфиденциальность и данные',
      showSurface: false,
      scrollable: false,
      contentPadding: EdgeInsets.zero,
      child: ListView(
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 140),
        children: [
          const _ScreenText(
            'Здесь собраны настройки и документы, связанные с приватностью, обработкой данных и удалением аккаунта.',
          ),
          const SizedBox(height: 16),
          InMomentActionTile(
            icon: Icons.policy_outlined,
            title: 'Политика конфиденциальности',
            subtitle: 'Краткая версия внутри приложения',
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const PolicyPage()),
              );
            },
            compact: true,
          ),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.public_rounded,
            title: 'Открыть публичную privacy policy',
            subtitle: AppLinks.privacyPolicyUrl,
            onTap: () => AppExternalActions.openUrl(
              context,
              url: AppLinks.privacyPolicyUrl,
              errorMessage: 'Не удалось открыть privacy policy.',
            ),
            compact: true,
          ),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.delete_outline_rounded,
            title: 'Удаление аккаунта и данных',
            subtitle: 'Запрос на удаление данных аккаунта',
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const DataDeletionPage()),
              );
            },
            compact: true,
          ),
          const SizedBox(height: 18),
          const _SectionLabel('Связь по данным'),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.privacy_tip_outlined,
            title: 'Написать по вопросам приватности',
            subtitle: AppContacts.privacyEmail,
            onTap: () => AppExternalActions.openMail(
              context,
              email: AppContacts.privacyEmail,
              subject: 'InMoment — вопрос по приватности',
              body: 'Здравствуйте.\n\nОпишите ваш вопрос по приватности или данным:\n',
            ),
            compact: true,
          ),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.copy_rounded,
            title: 'Скопировать email',
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
        fontSize: 15,
        fontWeight: FontWeight.w800,
        height: 1.15,
      ),
    );
  }
}

class _ScreenText extends StatelessWidget {
  final String text;

  const _ScreenText(this.text);

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        color: AppColors.textSecondary,
        fontSize: 12.5,
        height: 1.35,
        fontWeight: FontWeight.w600,
      ),
    );
  }
}