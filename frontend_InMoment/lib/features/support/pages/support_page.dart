import 'package:flutter/material.dart';

import '../../../core/config/app_contacts.dart';
import '../../../core/config/app_legal.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/app_external_actions.dart';
import '../../../core/widgets/inmoment_action_tile.dart';
import '../../../core/widgets/inmoment_page_shell.dart';

enum SupportFlowType {
  hub,
  problem,
  suggestion,
}

class SupportPage extends StatelessWidget {
  final SupportFlowType initialFlow;

  const SupportPage({super.key}) : initialFlow = SupportFlowType.hub;

  const SupportPage.problem({super.key})
      : initialFlow = SupportFlowType.problem;

  const SupportPage.suggestion({super.key})
      : initialFlow = SupportFlowType.suggestion;

  Future<void> _openProblemMail(BuildContext context) async {
    await AppExternalActions.openMail(
      context,
      email: AppContacts.supportEmail,
      subject: 'InMoment — проблема в приложении',
      body: [
        'Здравствуйте.',
        '',
        'Хочу сообщить о проблеме в приложении InMoment.',
        '',
        'Что произошло:',
        '',
        'Где это произошло:',
        '',
        'Как повторить:',
        '',
        'Ожидаемое поведение:',
        '',
        'Фактическое поведение:',
        '',
        'Устройство / ОС:',
        '',
        'Версия приложения:',
        AppLegal.appVersion,
        '',
      ].join('\n'),
    );
  }

  Future<void> _openSuggestionMail(BuildContext context) async {
    await AppExternalActions.openMail(
      context,
      email: AppContacts.supportEmail,
      subject: 'InMoment — предложение по улучшению',
      body: [
        'Здравствуйте.',
        '',
        'Хочу предложить улучшение для InMoment.',
        '',
        'Что хочется добавить или изменить:',
        '',
        'Где это должно работать:',
        '',
        'Почему это полезно:',
        '',
      ].join('\n'),
    );
  }

  Future<void> _openGeneralSupportMail(BuildContext context) async {
    await AppExternalActions.openMail(
      context,
      email: AppContacts.supportEmail,
      subject: 'InMoment — обращение в поддержку',
      body: [
        'Здравствуйте.',
        '',
        'Опишите ваш вопрос:',
        '',
        'Версия приложения:',
        AppLegal.appVersion,
        '',
      ].join('\n'),
    );
  }

  @override
  Widget build(BuildContext context) {
    switch (initialFlow) {
      case SupportFlowType.problem:
        return _SupportActionPage(
          title: 'Сообщить о проблеме',
          description:
              'Опишите, что произошло, где это случилось и как повторить проблему.',
          primaryTitle: 'Открыть почтовое приложение',
          primarySubtitle: AppContacts.supportEmail,
          onPrimaryTap: () => _openProblemMail(context),
          secondaryTitle: 'Скопировать email поддержки',
          secondarySubtitle: AppContacts.supportEmail,
          onSecondaryTap: () => AppExternalActions.copyText(
            context,
            text: AppContacts.supportEmail,
            successMessage: 'Email поддержки скопирован.',
          ),
        );

      case SupportFlowType.suggestion:
        return _SupportActionPage(
          title: 'Внести предложение',
          description:
              'Коротко опишите идею, где она должна работать и зачем нужна.',
          primaryTitle: 'Открыть почтовое приложение',
          primarySubtitle: AppContacts.supportEmail,
          onPrimaryTap: () => _openSuggestionMail(context),
          secondaryTitle: 'Скопировать email поддержки',
          secondarySubtitle: AppContacts.supportEmail,
          onSecondaryTap: () => AppExternalActions.copyText(
            context,
            text: AppContacts.supportEmail,
            successMessage: 'Email поддержки скопирован.',
          ),
        );

      case SupportFlowType.hub:
        return InMomentPageShell(
          title: 'Поддержка',
          showSurface: false,
          scrollable: false,
          contentPadding: EdgeInsets.zero,
          child: ListView(
            physics: const BouncingScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(10, 8, 10, 140),
            children: [
              const _SupportIntro(),
              const SizedBox(height: 16),
              InMomentActionTile(
                icon: Icons.bug_report_outlined,
                title: 'Сообщить о проблеме',
                subtitle: 'Ошибка, сбой, некорректная работа экрана',
                onTap: () {
                  Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => const SupportPage.problem(),
                    ),
                  );
                },
                compact: true,
              ),
              const SizedBox(height: 10),
              InMomentActionTile(
                icon: Icons.lightbulb_outline_rounded,
                title: 'Внести предложение',
                subtitle: 'Идея функции или улучшения интерфейса',
                onTap: () {
                  Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => const SupportPage.suggestion(),
                    ),
                  );
                },
                compact: true,
              ),
              const SizedBox(height: 10),
              InMomentActionTile(
                icon: Icons.mail_outline_rounded,
                title: 'Написать в поддержку',
                subtitle: AppContacts.supportEmail,
                onTap: () => _openGeneralSupportMail(context),
                compact: true,
              ),
            ],
          ),
        );
    }
  }
}

class _SupportActionPage extends StatelessWidget {
  final String title;
  final String description;
  final String primaryTitle;
  final String primarySubtitle;
  final VoidCallback onPrimaryTap;
  final String secondaryTitle;
  final String secondarySubtitle;
  final VoidCallback onSecondaryTap;

  const _SupportActionPage({
    required this.title,
    required this.description,
    required this.primaryTitle,
    required this.primarySubtitle,
    required this.onPrimaryTap,
    required this.secondaryTitle,
    required this.secondarySubtitle,
    required this.onSecondaryTap,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentPageShell(
      title: title,
      showSurface: false,
      scrollable: false,
      contentPadding: EdgeInsets.zero,
      child: ListView(
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(10, 8, 10, 140),
        children: [
          _SupportCaption(text: description),
          const SizedBox(height: 16),
          InMomentActionTile(
            icon: Icons.mail_outline_rounded,
            title: primaryTitle,
            subtitle: primarySubtitle,
            onTap: onPrimaryTap,
            compact: true,
          ),
          const SizedBox(height: 10),
          InMomentActionTile(
            icon: Icons.copy_rounded,
            title: secondaryTitle,
            subtitle: secondarySubtitle,
            onTap: onSecondaryTap,
            compact: true,
          ),
        ],
      ),
    );
  }
}

class _SupportIntro extends StatelessWidget {
  const _SupportIntro();

  @override
  Widget build(BuildContext context) {
    return const Text(
      'Здесь можно сообщить о проблеме, предложить улучшение или написать в поддержку.',
      style: TextStyle(
        color: AppColors.textSecondary,
        fontSize: 13,
        height: 1.35,
        fontWeight: FontWeight.w600,
      ),
    );
  }
}

class _SupportCaption extends StatelessWidget {
  final String text;

  const _SupportCaption({
    required this.text,
  });

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