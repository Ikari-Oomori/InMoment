import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../onboarding/pages/widgets/onboarding_step_scaffold.dart';
import '../models/auth_draft.dart';
import 'forgot_password_page.dart';
import 'password_login_page.dart';

class EmailLoginPage extends StatefulWidget {
  const EmailLoginPage({super.key});

  @override
  State<EmailLoginPage> createState() => _EmailLoginPageState();
}

class _EmailLoginPageState extends State<EmailLoginPage> {
  final _emailController = TextEditingController();
  String? _error;

  bool _isValidEmail(String value) {
    final email = value.trim();
    if (email.isEmpty) return false;

    final regex = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
    return regex.hasMatch(email);
  }

  bool get _canContinue => _isValidEmail(_emailController.text.trim());

  void _next() {
    final email = _emailController.text.trim();

    if (!_isValidEmail(email)) {
      setState(() {
        _error = 'Введите корректный email.';
      });
      return;
    }

    Navigator.of(context).push(
      _authStepRoute(
        PasswordLoginPage(
          draft: AuthDraft(email: email),
        ),
      ),
    );
  }

  void _openForgotPassword() {
    Navigator.of(context).push(
      _authStepRoute(
        ForgotPasswordPage(
          initialEmail: _emailController.text.trim(),
        ),
      ),
    );
  }

  @override
  void dispose() {
    _emailController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final emailText = _emailController.text.trim();
    final emailValid = emailText.isEmpty ? null : _isValidEmail(emailText);

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) {
            final bottomInset = MediaQuery.of(context).viewInsets.bottom;

            return AnimatedPadding(
              duration: const Duration(milliseconds: 180),
              curve: Curves.easeOut,
              padding: EdgeInsets.only(bottom: bottomInset),
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(20, 10, 20, 20),
                child: Center(
                  child: ConstrainedBox(
                    constraints: BoxConstraints(
                      maxWidth: 460,
                      minHeight: constraints.maxHeight - 30,
                    ),
                    child: IntrinsicHeight(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          OnboardingStepHeader(
                            step: 1,
                            total: 2,
                            showBackButton: Navigator.of(context).canPop(),
                          ),
                          const SizedBox(height: 24),
                          const Text(
                            'Вход',
                            style: TextStyle(
                              color: AppColors.textPrimary,
                              fontSize: 30,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                          const SizedBox(height: 10),
                          const Text(
                            'Введите email, чтобы перейти к паролю.',
                            style: TextStyle(
                              color: AppColors.textSecondary,
                              fontSize: 14,
                              height: 1.45,
                            ),
                          ),
                          const SizedBox(height: 22),
                          TextField(
                            controller: _emailController,
                            keyboardType: TextInputType.emailAddress,
                            style: const TextStyle(color: AppColors.textPrimary),
                            decoration: InputDecoration(
                              hintText: 'Email',
                              helperText: emailValid == null
                                  ? null
                                  : (emailValid
                                      ? 'Email выглядит корректно'
                                      : 'Проверьте адрес электронной почты'),
                              helperStyle: TextStyle(
                                color: emailValid == false
                                    ? Colors.redAccent
                                    : AppColors.accentLight,
                                fontWeight: FontWeight.w600,
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
                          if (_error != null) ...[
                            const SizedBox(height: 14),
                            Container(
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
                          const Spacer(),
                          const SizedBox(height: 16),
                          SizedBox(
                            width: double.infinity,
                            child: FilledButton(
                              onPressed: _canContinue ? _next : null,
                              child: const Text('Продолжить'),
                            ),
                          ),
                          const SizedBox(height: 10),
                          TextButton(
                            onPressed: _openForgotPassword,
                            child: const Text('Забыли пароль?'),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            );
          },
        ),
      ),
    );
  }
}

Route<T> _authStepRoute<T>(Widget page) {
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