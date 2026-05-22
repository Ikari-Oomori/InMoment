import 'package:flutter/material.dart';

import '../../../../core/api/api_error.dart';
import '../../../../core/controllers/app_session_controller.dart';
import '../../../../core/storage/token_storage.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../home/home_screen.dart';
import '../../api/onboarding_api.dart';
import '../../controllers/onboarding_flow_controller.dart';
import '../widgets/onboarding_step_scaffold.dart';

class OnboardingFinishStepPage extends StatefulWidget {
  final OnboardingFlowController controller;

  const OnboardingFinishStepPage({
    super.key,
    required this.controller,
  });

  @override
  State<OnboardingFinishStepPage> createState() =>
      _OnboardingFinishStepPageState();
}

class _OnboardingFinishStepPageState extends State<OnboardingFinishStepPage> {
  final _api = OnboardingApi();
  final _tokenStorage = const TokenStorage();

  bool _submitting = false;
  String? _error;

  Future<void> _finish() async {
    if (_submitting) return;

    final draft = widget.controller.draft;

    setState(() {
      _submitting = true;
      _error = null;
    });

    try {
      await _api.register(
        email: draft.email,
        password: draft.password,
        firstName: draft.firstName,
        lastName: draft.lastName,
        userName: draft.userName,
      );

      final tokens = await _api.login(
        email: draft.email,
        password: draft.password,
      );

      await _tokenStorage.saveTokens(
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
      );

      await AppSessionController.instance.markAuthenticated();

      if (!mounted) return;

      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(
          builder: (_) => const HomeScreen(),
        ),
        (route) => false,
      );
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось завершить настройку приложения. Попробуйте ещё раз.',
        );
        _submitting = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final draft = widget.controller.draft;

    return OnboardingStepScaffold(
      step: 7,
      total: 7,
      topVisual: Container(
        width: 84,
        height: 84,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: AppColors.accent.withValues(alpha: 0.18),
          borderRadius: BorderRadius.circular(26),
          border: Border.all(
            color: AppColors.accent.withValues(alpha: 0.32),
          ),
        ),
        child: const Icon(
          Icons.check_rounded,
          color: AppColors.textPrimary,
          size: 38,
        ),
      ),
      title: 'Готово',
      subtitle:
          'Аккаунт почти создан. После завершения вы сразу попадёте на главный экран приложения.',
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(20),
              border: Border.all(color: AppColors.border),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _SummaryLine(title: 'Email', value: draft.email),
                const SizedBox(height: 8),
                _SummaryLine(
                  title: 'Имя',
                  value: draft.fullName.isEmpty ? '—' : draft.fullName,
                ),
                const SizedBox(height: 8),
                _SummaryLine(
                  title: 'Username',
                  value: draft.userName.isEmpty ? '—' : '@${draft.userName}',
                ),
                const SizedBox(height: 8),
                _SummaryLine(
                  title: 'Контакты',
                  value: draft.contactsEnabled ? 'Разрешены' : 'Пока пропущены',
                ),
                if (draft.importedContacts.isNotEmpty) ...[
                  const SizedBox(height: 12),
                  const Text(
                    'Превью контактов',
                    style: TextStyle(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 8),
                  ...draft.importedContacts.take(4).map(
                        (item) => Padding(
                          padding: const EdgeInsets.only(bottom: 6),
                          child: Text(
                            '• ${item.displayName}',
                            style: const TextStyle(
                              color: AppColors.textSecondary,
                              height: 1.35,
                            ),
                          ),
                        ),
                      ),
                ],
              ],
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
      bottomAction: SizedBox(
        width: double.infinity,
        child: FilledButton(
          onPressed: _submitting ? null : _finish,
          child: _submitting
              ? const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Text('Завершить'),
        ),
      ),
    );
  }
}

class _SummaryLine extends StatelessWidget {
  final String title;
  final String value;

  const _SummaryLine({
    required this.title,
    required this.value,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: Text(
            title,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 13,
            ),
          ),
        ),
        Flexible(
          child: Text(
            value,
            textAlign: TextAlign.right,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 13,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
      ],
    );
  }
}