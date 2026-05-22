import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../widgets/onboarding_step_scaffold.dart';
import '../../controllers/onboarding_flow_controller.dart';
import 'onboarding_password_step_page.dart';

class OnboardingEmailStepPage extends StatefulWidget {
  final OnboardingFlowController controller;

  const OnboardingEmailStepPage({
    super.key,
    required this.controller,
  });

  @override
  State<OnboardingEmailStepPage> createState() =>
      _OnboardingEmailStepPageState();
}

class _OnboardingEmailStepPageState extends State<OnboardingEmailStepPage> {
  late final TextEditingController _controller;
  String? _error;

  @override
  void initState() {
    super.initState();
    _controller = TextEditingController(text: widget.controller.draft.email);
  }

  bool _isValidEmail(String value) {
    final email = value.trim();
    if (email.isEmpty) return false;

    final regex = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
    return regex.hasMatch(email);
  }

  bool get _canContinue => _isValidEmail(_controller.text.trim());

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
    final email = _controller.text.trim();

    if (!_isValidEmail(email)) {
      setState(() {
        _error = 'Введите корректный email.';
      });
      return;
    }

    widget.controller.updateEmail(email);

    Navigator.of(context).push(
      _route(
        OnboardingPasswordStepPage(controller: widget.controller),
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
        : (_isValidEmail(_controller.text.trim())
            ? 'Email выглядит корректно'
            : 'Проверьте адрес электронной почты');

    final positive = helperText == 'Email выглядит корректно';

    return OnboardingStepScaffold(
      step: 2,
      total: 7,
      title: 'Email',
      subtitle: 'Он понадобится для входа и восстановления доступа.',
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _controller,
            keyboardType: TextInputType.emailAddress,
            style: const TextStyle(color: AppColors.textPrimary),
            decoration: const InputDecoration(
              hintText: 'example@mail.com',
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