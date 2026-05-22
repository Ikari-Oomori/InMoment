import 'package:flutter/material.dart';

import '../../../../core/theme/app_colors.dart';
import '../widgets/onboarding_step_scaffold.dart';
import '../../controllers/onboarding_flow_controller.dart';
import 'onboarding_username_step_page.dart';

class OnboardingNameStepPage extends StatefulWidget {
  final OnboardingFlowController controller;

  const OnboardingNameStepPage({
    super.key,
    required this.controller,
  });

  @override
  State<OnboardingNameStepPage> createState() => _OnboardingNameStepPageState();
}

class _OnboardingNameStepPageState extends State<OnboardingNameStepPage> {
  late final TextEditingController _firstNameController;
  late final TextEditingController _lastNameController;

  @override
  void initState() {
    super.initState();
    _firstNameController =
        TextEditingController(text: widget.controller.draft.firstName);
    _lastNameController =
        TextEditingController(text: widget.controller.draft.lastName);
  }

  bool get _canContinue =>
      _firstNameController.text.trim().isNotEmpty &&
      _lastNameController.text.trim().isNotEmpty;

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
    widget.controller.updateName(
      firstName: _firstNameController.text.trim(),
      lastName: _lastNameController.text.trim(),
    );

    Navigator.of(context).push(
      _route(
        OnboardingUsernameStepPage(controller: widget.controller),
      ),
    );
  }

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    super.dispose();
  }

    @override
  Widget build(BuildContext context) {
    return OnboardingStepScaffold(
      step: 4,
      total: 7,
      title: 'Как вас зовут?',
      subtitle:
          'Имя и фамилия нужны для отображения в группах и публикациях.',
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _firstNameController,
            style: const TextStyle(color: AppColors.textPrimary),
            decoration: const InputDecoration(
              hintText: 'Имя',
            ),
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _lastNameController,
            style: const TextStyle(color: AppColors.textPrimary),
            decoration: const InputDecoration(
              hintText: 'Фамилия',
            ),
            onChanged: (_) => setState(() {}),
          ),
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