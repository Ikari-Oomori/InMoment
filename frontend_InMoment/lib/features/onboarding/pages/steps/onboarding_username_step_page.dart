import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../widgets/onboarding_step_scaffold.dart';
import '../../controllers/onboarding_flow_controller.dart';
import 'onboarding_contacts_step_page.dart';

class OnboardingUsernameStepPage extends StatefulWidget {
  final OnboardingFlowController controller;

  const OnboardingUsernameStepPage({
    super.key,
    required this.controller,
  });

  @override
  State<OnboardingUsernameStepPage> createState() =>
      _OnboardingUsernameStepPageState();
}

class _OnboardingUsernameStepPageState
    extends State<OnboardingUsernameStepPage> {
  late final TextEditingController _controller;
  String? _error;

  @override
  void initState() {
    super.initState();
    _controller = TextEditingController(text: widget.controller.draft.userName);
  }

  String? _validateUserName(String value) {
    final normalized = value.trim();

    if (normalized.length < 3) {
      return 'Минимум 3 символа';
    }

    if (normalized.length > 20) {
      return 'Максимум 20 символов';
    }

    final regex = RegExp(r'^[a-zA-Z0-9._]+$');
    if (!regex.hasMatch(normalized)) {
      return 'Только латинские буквы, цифры, точка и _';
    }

    if (normalized.startsWith('.') ||
        normalized.startsWith('_') ||
        normalized.endsWith('.') ||
        normalized.endsWith('_')) {
      return 'Username не должен начинаться или заканчиваться точкой / _';
    }

    return null;
  }

  bool get _canContinue => _validateUserName(_controller.text.trim()) == null;

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

  void _next() {
    final userName = _controller.text.trim();
    final validation = _validateUserName(userName);

    if (validation != null) {
      setState(() {
        _error = validation;
      });
      return;
    }

    widget.controller.updateUserName(userName);

    Navigator.of(context).push(
      _route(
        OnboardingContactsStepPage(controller: widget.controller),
      ),
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final helperText = _controller.text.trim().isEmpty
        ? null
        : _validateUserName(_controller.text.trim()) ?? 'Username подходит';
    final positive = helperText == 'Username подходит';

    return OnboardingStepScaffold(
      step: 5,
      total: 7,
      title: 'Username',
      subtitle:
          'Он будет использоваться в приглашениях и отображении профиля.',
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _controller,
            style: const TextStyle(color: AppColors.textPrimary),
            decoration: const InputDecoration(
              hintText: 'username',
              prefixText: '@',
            ),
            onChanged: (_) {
              setState(() {
                _error = null;
              });
            },
            onSubmitted: (_) {
              if (_canContinue) {
                _next();
              }
            },
          ),
          if (helperText != null) ...[
            const SizedBox(height: 10),
            Text(
              helperText,
              style: TextStyle(
                color: positive ? AppColors.accentLight : Colors.redAccent,
                fontSize: 12,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
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
          onPressed: _canContinue ? _next : null,
          child: const Text('Продолжить'),
        ),
      ),
    );
  }
}