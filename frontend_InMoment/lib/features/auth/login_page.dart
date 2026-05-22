import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';
import '../../core/layout/inmoment_media_frame.dart';
import '../onboarding/pages/onboarding_page.dart';
import 'pages/email_login_page.dart';

class LoginPage extends StatefulWidget {
  const LoginPage({super.key});

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  bool _openingSignIn = false;
  bool _openingOnboarding = false;

  Future<void> _openSignIn() async {
    if (_openingSignIn || _openingOnboarding) return;

    setState(() {
      _openingSignIn = true;
    });

    try {
      await Navigator.of(context).push(
        _authRoute(const EmailLoginPage()),
      );
    } finally {
      if (mounted) {
        setState(() {
          _openingSignIn = false;
        });
      }
    }
  }

  Future<void> _openOnboarding() async {
    if (_openingSignIn || _openingOnboarding) return;

    setState(() {
      _openingOnboarding = true;
    });

    try {
      await Navigator.of(context).push(
        _authRoute(const OnboardingPage()),
      );
    } finally {
      if (mounted) {
        setState(() {
          _openingOnboarding = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Center(
          child: SizedBox(
            width: InMomentMediaFrame.resolveCompactAuthWidth(
              MediaQuery.sizeOf(context).width,
            ),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(8, 20, 8, 24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Spacer(),
                  const SizedBox(height: 16),
                  const Text(
                    'InMoment',
                    style: TextStyle(
                      color: AppColors.textPrimary,
                      fontSize: 36,
                      fontWeight: FontWeight.w900,
                      letterSpacing: 0.2,
                    ),
                  ),
                  const SizedBox(height: 10),
                  const Text(
                    'Моменты для тех, кто близок',
                    textAlign: TextAlign.left,
                    style: TextStyle(
                      color: AppColors.accentLight,
                      fontSize: 18,
                      fontWeight: FontWeight.w700,
                      height: 1.35,
                    ),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Приватные группы, фото, видео, реакции и обсуждения — без лишнего шума.',
                    style: TextStyle(
                      color: AppColors.textSecondary,
                      fontSize: 14,
                      height: 1.45,
                    ),
                  ),
                  const SizedBox(height: 28),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      onPressed: _openingSignIn ? null : _openSignIn,
                      child: _openingSignIn
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Text('Войти'),
                    ),
                  ),
                  const SizedBox(height: 8),
                  Align(
                    alignment: Alignment.center,
                    child: TextButton(
                      onPressed: _openingOnboarding ? null : _openOnboarding,
                      child: const Text('Создать аккаунт'),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

Route<T> _authRoute<T>(Widget page) {
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