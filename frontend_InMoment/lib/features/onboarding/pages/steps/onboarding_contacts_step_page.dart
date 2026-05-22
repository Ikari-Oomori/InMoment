import 'package:flutter/material.dart';
import 'package:flutter_contacts/flutter_contacts.dart';

import '../../../../core/theme/app_colors.dart';
import '../widgets/onboarding_step_scaffold.dart';
import '../../controllers/onboarding_flow_controller.dart';
import '../../models/onboarding_draft.dart';
import 'onboarding_finish_step_page.dart';

class OnboardingContactsStepPage extends StatefulWidget {
  final OnboardingFlowController controller;

  const OnboardingContactsStepPage({
    super.key,
    required this.controller,
  });

  @override
  State<OnboardingContactsStepPage> createState() =>
      _OnboardingContactsStepPageState();
}

class _OnboardingContactsStepPageState extends State<OnboardingContactsStepPage> {
  bool _loading = false;
  String? _error;

  Route<T> _route<T>(Widget page) {
    return PageRouteBuilder<T>(
      pageBuilder: (_, _, _) => page,
      transitionsBuilder: (_, animation, _, child) {
        final offsetAnimation = Tween<Offset>(
          begin: const Offset(0.08, 0),
          end: Offset.zero,
        ).animate(
          CurvedAnimation(parent: animation, curve: Curves.easeOutCubic),
        );

        final fadeAnimation = CurvedAnimation(
          parent: animation,
          curve: Curves.easeOut,
        );

        return FadeTransition(
          opacity: fadeAnimation,
          child: SlideTransition(
            position: offsetAnimation,
            child: child,
          ),
        );
      },
    );
  }

  Future<void> _continueWithContacts() async {
    if (_loading) return;

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final permission = await FlutterContacts.requestPermission(
        readonly: true,
      );

      if (!permission) {
        if (!mounted) return;

        setState(() {
          _loading = false;
          _error = 'Доступ к контактам не был предоставлен.';
        });
        return;
      }

      final contacts = await FlutterContacts.getContacts(
        withProperties: true,
      );

      final previews = <OnboardingContactPreview>[];

      for (final contact in contacts) {
        final phones = contact.phones;
        final emails = contact.emails;

        final phone = phones.isNotEmpty ? phones.first.number.trim() : null;
        final email = emails.isNotEmpty ? emails.first.address.trim() : null;

        final displayName = contact.displayName.trim().isNotEmpty
            ? contact.displayName.trim()
            : 'Контакт';

        if ((phone != null && phone.isNotEmpty) ||
            (email != null && email.isNotEmpty)) {
          previews.add(
            OnboardingContactPreview(
              displayName: displayName,
              phone: phone?.isEmpty == true ? null : phone,
              email: email?.isEmpty == true ? null : email,
            ),
          );
        }

        if (previews.length >= 8) {
          break;
        }
      }

      widget.controller.updateContacts(
        enabled: true,
        importedContacts: previews,
      );

      if (!mounted) return;

      Navigator.of(context).push(
        _route(
          OnboardingFinishStepPage(controller: widget.controller),
        ),
      );
    } catch (_) {
      if (!mounted) return;

      setState(() {
        _error = 'Не удалось прочитать контакты.';
        _loading = false;
      });
    }
  }

  Future<void> _continueWithoutContacts() async {
    final result = await showDialog<bool>(
      context: context,
      builder: (context) {
        return AlertDialog(
          backgroundColor: AppColors.surface,
          title: const Text(
            'Не сейчас',
            style: TextStyle(color: AppColors.textPrimary),
          ),
          content: const Text(
            'Вы сможете подключить контакты позже в приложении.',
            style: TextStyle(
              color: AppColors.textSecondary,
              height: 1.4,
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('Вернуться'),
            ),
            FilledButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: const Text('Пропустить'),
            ),
          ],
        );
      },
    );

    if (result != true || !mounted) return;

    widget.controller.updateContacts(
      enabled: false,
      importedContacts: const [],
    );

    Navigator.of(context).push(
      _route(
        OnboardingFinishStepPage(controller: widget.controller),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return OnboardingStepScaffold(
      step: 6,
      total: 7,
      title: 'Найдём знакомых',
      subtitle:
          'Разрешите доступ к контактам, чтобы быстрее находить знакомых и приглашать их в приватные группы.',
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(20),
              border: Border.all(color: AppColors.border),
            ),
            child: const Text(
              'Контакты можно подключить сейчас или позже. На этом шаге мы показываем только превью найденных контактов без тяжёлой синхронизации.',
              style: TextStyle(
                color: AppColors.textSecondary,
                fontSize: 13,
                height: 1.45,
              ),
            ),
          ),
          if (_error != null) ...[
            const SizedBox(height: 14),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: Colors.redAccent.withValues(alpha: 0.10),
                borderRadius: BorderRadius.circular(16),
                border: Border.all(
                  color: Colors.redAccent.withValues(alpha: 0.25),
                ),
              ),
              child: Text(
                _error!,
                style: const TextStyle(
                  color: Colors.redAccent,
                  fontSize: 13,
                  height: 1.4,
                ),
              ),
            ),
          ],
        ],
      ),
      bottomAction: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          FilledButton.icon(
            onPressed: _loading ? null : _continueWithContacts,
            icon: _loading
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.contacts_rounded),
            label: Text(_loading ? 'Подождите...' : 'Разрешить'),
          ),
          const SizedBox(height: 10),
          OutlinedButton.icon(
            onPressed: _loading ? null : _continueWithoutContacts,
            icon: const Icon(Icons.schedule_rounded),
            label: const Text('Не сейчас'),
          ),
        ],
      ),
    );
  }
}
