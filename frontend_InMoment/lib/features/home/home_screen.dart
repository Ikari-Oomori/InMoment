import 'package:flutter/material.dart';

import '../shell/models/app_shell_tab.dart';
import '../shell/pages/app_shell_page.dart';

class HomeScreen extends StatelessWidget {
  final AppShellTab initialTab;
  final String? initialSystemMemoryId;

  const HomeScreen({
    super.key,
    this.initialTab = AppShellTab.camera,
    this.initialSystemMemoryId,
  });

  @override
  Widget build(BuildContext context) {
    return AppShellPage(
      initialTab: initialTab,
      initialSystemMemoryId: initialSystemMemoryId,
    );
  }
}