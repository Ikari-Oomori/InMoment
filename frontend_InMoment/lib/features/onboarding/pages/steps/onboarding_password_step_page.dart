import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../widgets/onboarding_step_scaffold.dart';
import '../../controllers/onboarding_flow_controller.dart';
import 'onboarding_name_step_page.dart';

class OnboardingPasswordStepPage extends StatefulWidget {
  final OnboardingFlowController controller;

  const OnboardingPasswordStepPage({
    super.key,
    required this.controller,
  });

  @override
  State<OnboardingPasswordStepPage> createState() =>
      _OnboardingPasswordStepPageState();
}

class _OnboardingPasswordStepPageState
    extends State<OnboardingPasswordStepPage> {
  late final TextEditingController _controller;
  bool _obscureText = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _controller = TextEditingController(text: widget.controller.draft.password);
  }

  bool _isValidPassword(String value) {
    return value.trim().length >= 6;
  }

  bool get _canContinue => _isValidPassword(_controller.text.trim());

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
    final password = _controller.text.trim();

    if (!_isValidPassword(password)) {
      setState(() {
        _error = 'Минимум 6 символов.';
      });
      return;
    }

    widget.controller.updatePassword(password);

    Navigator.of(context).push(
      _route(
        OnboardingNameStepPage(controller: widget.controller),
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
        : (_isValidPassword(_controller.text.trim())
            ? 'Пароль подходит'
            : 'Минимум 6 символов');
    final positive = helperText == 'Пароль подходит';

    return OnboardingStepScaffold(
      step: 3,
      total: 7,
      title: 'Пароль',
      subtitle: 'Минимум 6 символов. Лучше использовать буквы и цифры.',
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _controller,
            obscureText: _obscureText,
            style: const TextStyle(color: AppColors.textPrimary),
            decoration: InputDecoration(
              hintText: 'Введите пароль',
              suffixIcon: IconButton(
                onPressed: () {
                  setState(() {
                    _obscureText = !_obscureText;
                  });
                },
                icon: Icon(
                  _obscureText
                      ? Icons.visibility_outlined
                      : Icons.visibility_off_outlined,
                ),
              ),
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