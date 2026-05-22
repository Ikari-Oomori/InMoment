enum AppShellTab {
  memories,
  camera,
  profile,
}

extension AppShellTabX on AppShellTab {
  int get index {
    switch (this) {
      case AppShellTab.memories:
        return 0;
      case AppShellTab.camera:
        return 1;
      case AppShellTab.profile:
        return 2;
    }
  }

  static AppShellTab fromIndex(int index) {
    switch (index) {
      case 0:
        return AppShellTab.memories;
      case 1:
        return AppShellTab.camera;
      case 2:
        return AppShellTab.profile;
      default:
        return AppShellTab.camera;
    }
  }
}