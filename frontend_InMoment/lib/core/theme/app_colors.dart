import 'package:flutter/material.dart';

class AppColors {
  static const Color background = Color(0xFF0C080D);
  static const Color backgroundSecondary = Color(0xFF120D15);
  static const Color backgroundWarm = Color(0xFF170F1C);

  static const Color surface = Color(0xFF23162E);
  static const Color surfaceSoft = Color(0xFF1F1823);
  static const Color surfaceElevated = Color(0xFF2A1D34);
  static const Color surfaceDeep = Color(0xFF17101C);

  static const Color accent = Color(0xFF7A6284);
  static const Color accentSecondary = Color(0xFF815D91);
  static const Color accentStrong = Color(0xFFA178B7);
  static const Color accentSoft = Color(0xFFB895C7);

  static const Color textPrimary = Color(0xFFE6D7EA);
  static const Color textSecondary = Color(0xFFC2A7CC);
  static const Color textMuted = Color(0xFF937B99);

  static const Color error = Color(0xFFE57373);
  static const Color success = Color(0xFF81C784);
  static const Color warning = Color(0xFFFFC46B);

  static const Color border = Color(0xFF3A2B44);
  static const Color borderSoft = Color(0xFF4D395A);

  static const Color white = Colors.white;
  static const Color black = Colors.black;

  static const Color bg = background;
  static const Color card = surfaceSoft;
  static const Color cardAlt = surface;
  static const Color cardElevated = surfaceElevated;

  static const Color primary = accent;
  static const Color primaryLight = accentSecondary;

  static const Color onBackground = textPrimary;
  static const Color onCard = textPrimary;
  static const Color muted = textSecondary;

  static const Color accentLight = accentSecondary;
  static const Color surfaceLight = surfaceSoft;

  static Color surfaceGlass([double alpha = 0.72]) =>
      surface.withValues(alpha: alpha);

  static Color surfaceGlassStrong([double alpha = 0.88]) =>
      surfaceElevated.withValues(alpha: alpha);

  static Color overlayGlass([double alpha = 0.34]) =>
      black.withValues(alpha: alpha);

  static Color softStroke([double alpha = 0.14]) =>
      white.withValues(alpha: alpha);

  static Color purpleStroke([double alpha = 0.30]) =>
      accentSoft.withValues(alpha: alpha);

  static Color shadow([double alpha = 0.26]) =>
      black.withValues(alpha: alpha);

  static const LinearGradient pageBackgroundGradient = LinearGradient(
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
    colors: [
      Color(0xFF0C080D),
      Color(0xFF0C080D),
    ],
  );

  static const LinearGradient heroGlowGradient = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [
      Color(0x66A178B7),
      Color(0x33815D91),
      Color(0x00120D15),
    ],
  );

  static const LinearGradient accentButtonGradient = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [
      Color(0xFFA178B7),
      Color(0xFF815D91),
    ],
  );

  static const LinearGradient glassGradient = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [
      Color(0x442A1D34),
      Color(0xCC1F1823),
      Color(0x8823162E),
    ],
  );

  static const LinearGradient darkPhotoOverlayGradient = LinearGradient(
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
    colors: [
      Color(0x22000000),
      Color(0x00000000),
      Color(0xAA000000),
    ],
  );
}