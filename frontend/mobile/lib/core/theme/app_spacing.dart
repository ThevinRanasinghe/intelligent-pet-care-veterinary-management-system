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

/// Corner radii per the design spec: cards 16–20, buttons/fields 12–14.
class AppRadius {
  AppRadius._();

  static const double card = 18;
  static const double cardSmall = 14;
  static const double control = 13;
  static const double chip = 999;
}

/// Durations for the "animate changes, not everything" animation spec.
class AppDurations {
  AppDurations._();

  static const Duration press = Duration(milliseconds: 120);
  static const Duration entrance = Duration(milliseconds: 250);
  static const Duration step = Duration(milliseconds: 200);
}
