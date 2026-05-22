import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/controllers/app_session_controller.dart';
import '../../../core/storage/token_storage.dart';
import '../../../core/theme/app_colors.dart';
import '../../home/home_screen.dart';
import '../../onboarding/pages/widgets/onboarding_step_scaffold.dart';
import '../api/auth_api.dart';
import '../models/auth_draft.dart';
import 'forgot_password_page.dart';

class PasswordLoginPage extends StatefulWidget {
  final AuthDraft draft;

  const PasswordLoginPage({
    super.key,
    required this.draft,
  });

  @override
  State<PasswordLoginPage> createState() => _PasswordLoginPageState();
}

class _PasswordLoginPageState extends State<PasswordLoginPage> {
  final _passwordController = TextEditingController();
  final _api = AuthApi();
  final _tokenStorage = const TokenStorage();

  bool _loading = false;
  bool _obscurePassword = true;
  String? _error;

  bool get _canContinue =>
      !_loading && _passwordController.text.trim().isNotEmpty;

  Future<void> _login() async {
    if (_loading) return;

    final password = _passwordController.text.trim();

    if (password.isEmpty) {
      setState(() {
        _error = 'Введите пароль.';
      });
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final tokens = await _api.login(
        email: widget.draft.email,
        password: password,
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
          fallback: 'Не удалось выполнить вход. Проверьте данные и попробуйте ещё раз.',
        );
      });
    } finally {
      if (mounted) {
        setState(() {
          _loading = false;
        });
      }
    }
  }

  void _openForgotPassword() {
    Navigator.of(context).push(
      _authStepRoute(
        ForgotPasswordPage(
          initialEmail: widget.draft.email,
        ),
      ),
    );
  }

  @override
  void dispose() {
    _passwordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
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
                            step: 2,
                            total: 2,
                            showBackButton: Navigator.of(context).canPop(),
                          ),
                          const SizedBox(height: 24),
                          const Text(
                            'Пароль',
                            style: TextStyle(
                              color: AppColors.textPrimary,
                              fontSize: 30,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                          const SizedBox(height: 10),
                          Text(
                            widget.draft.email,
                            style: const TextStyle(
                              color: AppColors.accentLight,
                              fontSize: 15,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 22),
                          TextField(
                            controller: _passwordController,
                            enabled: !_loading,
                            obscureText: _obscurePassword,
                            style: const TextStyle(color: AppColors.textPrimary),
                            decoration: InputDecoration(
                              hintText: 'Введите пароль',
                              suffixIcon: IconButton(
                                onPressed: _loading
                                    ? null
                                    : () {
                                        setState(() {
                                          _obscurePassword = !_obscurePassword;
                                        });
                                      },
                                icon: Icon(
                                  _obscurePassword
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
                                _login();
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
                              onPressed: _canContinue ? _login : null,
                              child: _loading
                                  ? const SizedBox(
                                      width: 18,
                                      height: 18,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                      ),
                                    )
                                  : const Text('Войти'),
                            ),
                          ),
                          const SizedBox(height: 10),
                          TextButton(
                            onPressed: _loading ? null : _openForgotPassword,
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