import 'package:flutter/material.dart';

import '../core/controllers/app_session_controller.dart';
import '../core/navigation/app_navigator.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import '../features/auth/login_page.dart';
import '../features/auth/services/password_reset_link_handler.dart';
import '../features/groups/services/group_invite_link_handler.dart';
import '../features/home/home_screen.dart';
import '../features/widget/services/widget_sync_service.dart';

class InMomentApp extends StatelessWidget {
  const InMomentApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'InMoment',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.darkTheme(),
      navigatorKey: appNavigatorKey,
      builder: (context, child) {
        return _ResponsiveAppFrame(
          child: child ?? const SizedBox.shrink(),
        );
      },
      home: const AppBootstrap(),
    );
  }
}

class AppBootstrap extends StatefulWidget {
  const AppBootstrap({super.key});

  @override
  State<AppBootstrap> createState() => _AppBootstrapState();
}

class _AppBootstrapState extends State<AppBootstrap> {
  final _session = AppSessionController.instance;

  @override
  void initState() {
    super.initState();
    _session.bootstrap();
    PasswordResetLinkHandler.instance.start();
    GroupInviteLinkHandler.instance.start();
    WidgetSyncService.instance.initializeWidgetNavigation();
  }

  @override
  void dispose() {
    PasswordResetLinkHandler.instance.dispose();
    GroupInviteLinkHandler.instance.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _session,
      builder: (context, _) {
        if (_session.isUnknown) {
          return const _BootstrapSplash();
        }

        if (_session.isAuthenticated) {
          return const HomeScreen();
        }

        return const LoginPage();
      },
    );
  }
}

class _BootstrapSplash extends StatelessWidget {
  const _BootstrapSplash();

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      backgroundColor: AppColors.background,
      body: Stack(
        fit: StackFit.expand,
        children: [
          Image(
            image: AssetImage('lib/assets/branding/splash.png'),
            fit: BoxFit.cover,
          ),
          _SplashOverlay(),
        ],
      ),
    );
  }
}

class _SplashOverlay extends StatelessWidget {
  const _SplashOverlay();

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [
            Color(0x330C080D),
            Color(0x110C080D),
            Color(0x660C080D),
          ],
        ),
      ),
      child: Align(
        alignment: Alignment(0, 0.72),
        child: SizedBox(
          width: 30,
          height: 30,
          child: CircularProgressIndicator(
            strokeWidth: 2.4,
            color: Color(0xFFA78BDF),
          ),
        ),
      ),
    );
  }
}

class _ResponsiveAppFrame extends StatelessWidget {
  final Widget child;

  const _ResponsiveAppFrame({
    required this.child,
  });

  static const double _tabletBreakpoint = 600;
  static const double _desktopBreakpoint = 1100;
  static const double _tabletMaxWidth = 900;
  static const double _desktopMaxWidth = 1180;

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    final width = media.size.width;

    if (width < _tabletBreakpoint) {
      return child;
    }

    final maxWidth =
        width >= _desktopBreakpoint ? _desktopMaxWidth : _tabletMaxWidth;

    final framedWidth = width > maxWidth ? maxWidth : width;

    return DecoratedBox(
      decoration: const BoxDecoration(
        gradient: AppColors.pageBackgroundGradient,
      ),
      child: Center(
        child: SizedBox(
          width: framedWidth,
          height: media.size.height,
          child: MediaQuery(
            data: media.copyWith(
              size: Size(framedWidth, media.size.height),
            ),
            child: child,
          ),
        ),
      ),
    );
  }
}