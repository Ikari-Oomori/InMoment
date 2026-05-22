import 'package:flutter/material.dart';

import '../../../core/config/app_legal.dart';
import '../../../core/config/app_links.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/app_external_actions.dart';
import '../../../core/widgets/inmoment_action_tile.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import 'policy_page.dart';
import 'terms_page.dart';

class AboutAppPage extends StatelessWidget {
  const AboutAppPage({super.key});

  @override
  Widget build(BuildContext context) {
    return InMomentPageShell(
      title: 'О приложении и документы',
      showSurface: false,
      scrollable: false,
      contentPadding: EdgeInsets.zero,
      child: ListView(
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(10, 8, 10, 140),
        children: [
          const Text(
            AppLegal.productTagline,
            style: TextStyle(
              color: AppColors.accentLight,
              fontSize: 13,
              fontWeight: FontWeight.w700,
              height: 1.25,
            ),
          ),
          const SizedBox(height: 6),
          const Text(
            'Версия ${AppLegal.appVersion}',
            style: TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12.5,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 18),
          const _SectionLabel('Документы'),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.policy_outlined,
            title: 'Политика конфиденциальности',
            subtitle:
                'Версия ${AppLegal.privacyPolicyVersion} · ${AppLegal.privacyPolicyEffectiveDate}',
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
            subtitle:
                'Версия ${AppLegal.termsVersion} · ${AppLegal.termsEffectiveDate}',
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const TermsPage()),
              );
            },
            compact: true,
          ),
          const SizedBox(height: 18),
          const _SectionLabel('Публичные ссылки'),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.public_rounded,
            title: 'Privacy policy',
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
            icon: Icons.public_rounded,
            title: 'Terms',
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
            icon: Icons.public_rounded,
            title: 'Data deletion',
            subtitle: AppLinks.dataDeletionUrl,
            onTap: () => AppExternalActions.openUrl(
              context,
              url: AppLinks.dataDeletionUrl,
              errorMessage: 'Не удалось открыть страницу удаления данных.',
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