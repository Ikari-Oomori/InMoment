import 'dart:math' as math;

class InMomentMediaFrame {
  final double width;
  final double height;

  const InMomentMediaFrame({
    required this.width,
    required this.height,
  });

  static const double minAdaptiveContentWidth = 320.0;
  static const double maxAdaptiveContentWidth = 760.0;

  static const double feedGap = 12.0;
  static const double minFeedCardWidth = 136.0;
  static const double maxFeedCardWidth = 220.0;

  static const double bottomNavigationReserve = 122.0;
  static const double cameraTopReserve = 72.0;
  static const double cameraControlsReserve = 180.0;

  static bool isCompactPhoneWidth(double viewportWidth) => viewportWidth < 480.0;

  static double resolveAdaptiveContentWidth(double viewportWidth) {
    final sideReserve = isCompactPhoneWidth(viewportWidth) ? 24.0 : 32.0;
    final safeWidth = math.max(0.0, viewportWidth - sideReserve);

    return safeWidth
        .clamp(0.0, maxAdaptiveContentWidth)
        .toDouble();
  }

  static double resolveTabletContentWidth(double viewportWidth) {
    return resolveAdaptiveContentWidth(viewportWidth);
  }

  static int resolveFeedColumnCount(double contentWidth) {
    if (contentWidth <= 0) return 2;

    final rawCount =
        ((contentWidth + feedGap) / (minFeedCardWidth + feedGap)).floor();

    return rawCount.clamp(2, 4);
  }

  static double resolveFeedCardWidth({
    required double contentWidth,
    required int columnCount,
  }) {
    final gapsWidth = feedGap * (columnCount - 1);

    final rawWidth = (contentWidth - gapsWidth) / columnCount;
    return rawWidth.clamp(0.0, maxFeedCardWidth).toDouble();
  }

  static InMomentMediaFrame resolveHomeSquare({
    required double viewportWidth,
    required double viewportHeight,
    double? availableHeight,
  }) {
    final contentWidth = resolveAdaptiveContentWidth(viewportWidth);

    final effectiveAvailableHeight = availableHeight ??
        viewportHeight -
            cameraTopReserve -
            cameraControlsReserve -
            bottomNavigationReserve;

    final minSize = viewportWidth < 380 || viewportHeight < 720 ? 220.0 : 280.0;

    final size = math
        .min(contentWidth, effectiveAvailableHeight)
        .clamp(minSize, contentWidth)
        .toDouble();

    return InMomentMediaFrame(width: size, height: size);
  }

  static InMomentMediaFrame resolveShellFrame({
    required double viewportWidth,
    required double viewportHeight,
    double? availableHeight,
  }) {
    return resolveHomeSquare(
      viewportWidth: viewportWidth,
      viewportHeight: viewportHeight,
      availableHeight: availableHeight,
    );
  }

  static double resolveCompactAuthWidth(double viewportWidth) {
    final safeWidth = math.max(0.0, viewportWidth - 16.0);
    return safeWidth.clamp(320.0, 460.0).toDouble();
  }

  static double resolveSnackBarWidth(double viewportWidth) {
    final safeWidth = math.max(0.0, viewportWidth - 16.0);
    return safeWidth.clamp(300.0, 560.0).toDouble();
  }

  static double resolveMediaViewerWidth(double viewportWidth) {
    final safeWidth = math.max(0.0, viewportWidth - 4.0);
    return safeWidth.clamp(320.0, 1040.0).toDouble();
  }

  static double resolveDialogWidth(double viewportWidth) {
    final safeWidth = math.max(0.0, viewportWidth - 20.0);
    return safeWidth.clamp(300.0, 540.0).toDouble();
  }

  static double resolveBottomSheetWidth(double viewportWidth) {
    final safeWidth = math.max(0.0, viewportWidth - 12.0);
    return safeWidth.clamp(320.0, 620.0).toDouble();
  }
}