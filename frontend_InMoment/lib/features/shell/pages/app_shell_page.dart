import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/app_bottom_nav_bar.dart';
import '../../camera/pages/camera_hub_page.dart';
import '../../feed/pages/feed_page.dart';
import '../../memories/pages/memories_page.dart';
import '../../profile/pages/profile_page.dart';
import '../models/app_shell_tab.dart';

class AppShellPage extends StatefulWidget {
  final AppShellTab initialTab;
  final String? initialSystemMemoryId;

  const AppShellPage({
    super.key,
    this.initialTab = AppShellTab.camera,
    this.initialSystemMemoryId,
  });

  @override
  State<AppShellPage> createState() => _AppShellPageState();
}

class _AppShellPageState extends State<AppShellPage> {
  late final PageController _pageController;
  late AppShellTab _selectedTab;

  @override
  void initState() {
    super.initState();
    _selectedTab = widget.initialTab;
    _pageController = PageController(
      initialPage: widget.initialTab.index,
      viewportFraction: 1,
    );
  }

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  void _selectTab(AppShellTab tab) {
    if (_selectedTab == tab) return;

    setState(() {
      _selectedTab = tab;
    });

    _pageController.animateToPage(
      tab.index,
      duration: const Duration(milliseconds: 260),
      curve: Curves.easeOutCubic,
    );
  }

  Future<void> _openGroupFeed() async {
    await Navigator.of(context).push(
      PageRouteBuilder(
        transitionDuration: const Duration(milliseconds: 260),
        reverseTransitionDuration: const Duration(milliseconds: 220),
        pageBuilder: (_, animation, _) {
          final curved = CurvedAnimation(
            parent: animation,
            curve: Curves.easeOutCubic,
          );

          return FadeTransition(
            opacity: curved,
            child: SlideTransition(
              position: Tween<Offset>(
                begin: const Offset(0, 0.06),
                end: Offset.zero,
              ).animate(curved),
              child: const FeedPage(),
            ),
          );
        },
      ),
    );
  }

  List<Widget> _buildPages() {
    return [
      MemoriesPage(
        key: const PageStorageKey<String>('memories_page'),
        initialSystemMemoryId: widget.initialSystemMemoryId,
      ),
      CameraHubPage(
        key: const PageStorageKey<String>('camera_hub_page'),
        onOpenGroupFeed: _openGroupFeed,
        onOpenProfile: () => _selectTab(AppShellTab.profile),
      ),
      const ProfilePage(
        key: PageStorageKey<String>('profile_page'),
      ),
    ];
  }

  @override
  Widget build(BuildContext context) {
    final pages = _buildPages();

    return Scaffold(
      backgroundColor: AppColors.background,
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: AppColors.pageBackgroundGradient,
        ),
        child: Stack(
          children: [
            SafeArea(
              top: false,
              bottom: false,
              child: PageView(
                controller: _pageController,
                physics: const BouncingScrollPhysics(),
                onPageChanged: (index) {
                  final tab = AppShellTabX.fromIndex(index);
                  if (_selectedTab != tab) {
                    setState(() {
                      _selectedTab = tab;
                    });
                  }
                },
                children: pages,
              ),
            ),
            Positioned(
              left: 0,
              right: 0,
              bottom: 0,
              child: AppBottomNavBar(
                selectedTab: _selectedTab,
                onTabSelected: _selectTab,
              ),
            ),
          ],
        ),
      ),
    );
  }
}