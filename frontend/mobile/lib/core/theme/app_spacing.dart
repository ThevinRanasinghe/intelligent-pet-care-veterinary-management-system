import 'package:flutter/widgets.dart';

/// Spacing and shape tokens for the mobile-first PetCare UI.
/// Mirrors the web design system: an 4/8/12/16/20/24/32 spacing scale,
/// 20 px page padding, 16–20 px card radius and 12–14 px control radius.
class AppSpacing {
  AppSpacing._();

  static const double xxs = 4;
  static const double xs = 8;
  static const double sm = 12;
  static const double md = 16;
  static const double lg = 20;
  static const double xl = 24;
  static const double xxl = 32;

  /// Standard horizontal page padding (20 px).
  static const double pageHorizontal = lg;
  static const EdgeInsetsGeometry pagePadding =
      EdgeInsets.symmetric(horizontal: pageHorizontal);
}

/// Corner radii per the Beacon spec: inputs/buttons 12, normal cards 16,
/// hero/detail cards & bottom sheets 24, pills fully rounded.
class AppRadius {
  AppRadius._();

  static const double control = 12;
  static const double card = 16;
  static const double cardSmall = 14;
  static const double large = 24;
  static const double chip = 999;
  static const double pill = 999;
}

/// Durations for the "animate changes, not everything" animation spec.
class AppDurations {
  AppDurations._();

  static const Duration press = Duration(milliseconds: 120);
  static const Duration entrance = Duration(milliseconds: 250);
  static const Duration step = Duration(milliseconds: 200);
}
