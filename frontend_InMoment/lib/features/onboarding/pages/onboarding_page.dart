import 'package:flutter/material.dart';

import '../controllers/onboarding_flow_controller.dart';
import 'steps/onboarding_welcome_step_page.dart';

class OnboardingPage extends StatefulWidget {
  const OnboardingPage({super.key});

  @override
  State<OnboardingPage> createState() => _OnboardingPageState();
}

class _OnboardingPageState extends State<OnboardingPage> {
  final OnboardingFlowController _controller = OnboardingFlowController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return OnboardingWelcomeStepPage(controller: _controller);
  }
}