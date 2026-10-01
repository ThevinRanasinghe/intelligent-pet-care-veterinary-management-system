import 'package:flutter/material.dart';

/// Central motion tokens for the Beacon app — one consistent animation
/// language: fast micro-feedback (~150ms), standard transitions (~250ms)
/// and short emphasis moments (~400ms). All custom widgets should use
/// these instead of ad-hoc durations.
class AppMotion {
  AppMotion._();

  /// Press feedback, indicator fills, tiny state flips.
  static const Duration fast = Duration(milliseconds: 150);

  /// Page transitions, selection/interpolation, AnimatedSwitchers.
  static const Duration standard = Duration(milliseconds: 250);

  /// Bottom-nav horizontal page slide (~280ms) — both pages move
  /// simultaneously like a carousel.
  static const Duration tabSlide = Duration(milliseconds: 280);

  /// Focal entrances (success icon, pop-ins) — used sparingly.
  static const Duration emphasis = Duration(milliseconds: 400);

  /// Primary ease — decelerating entrances.
  static const Curve easeOut = Curves.easeOutCubic;

  /// Secondary ease — symmetric moves (step swaps, selection).
  static const Curve easeInOut = Curves.easeInOutCubic;

  /// Expressive ease — restrained overshoot for focal elements only.
  static const Curve expressive = Curves.easeOutBack;

  /// Accessibility: honours the platform reduce-motion flag.
  static bool reduceMotion(BuildContext context) =>
      MediaQuery.disableAnimationsOf(context);

  /// Standard forward/backward page transition (~250ms).
  ///
  /// Forward: the incoming page fades 0→1, slides +12px→0 and scales
  /// 0.985→1 while the covered page drifts −8px and dims.
  /// Back: the popped page replays forward in reverse (slides right,
  /// fades out) while the revealed page drifts in from −8px — giving
  /// directional distinction without a separate builder per page.
  static Widget pageTransition(
    BuildContext context,
    Animation<double> animation,
    Animation<double> secondaryAnimation,
    Widget child,
  ) {
    if (reduceMotion(context)) return child;
    final curved = CurvedAnimation(parent: animation, curve: easeOut);
    return AnimatedBuilder(
      animation: Listenable.merge([curved, secondaryAnimation]),
      child: child,
      builder: (context, child) {
        final t = curved.value; // 0 entering → 1 fully visible
        final s = secondaryAnimation.value; // 0 visible → 1 covered
        final opacity = (t * (1 - 0.45 * s)).clamp(0.0, 1.0);
        final dx = 12 * (1 - t) - 8 * s;
        final scale = 1 - 0.015 * (1 - t);
        return Opacity(
          opacity: opacity,
          child: Transform.translate(
            offset: Offset(dx, 0),
            child: Transform.scale(scale: scale, child: child),
          ),
        );
      },
    );
  }

  /// Fade-through used for app-level swaps (splash→home, logout→login).
  static Widget fadeThroughTransition(
      Widget child, Animation<double> animation) {
    return FadeTransition(
      opacity: CurvedAnimation(parent: animation, curve: easeOut),
      child: ScaleTransition(
        scale: Tween<double>(begin: 0.985, end: 1.0).animate(
          CurvedAnimation(parent: animation, curve: easeOut),
        ),
        child: child,
      ),
    );
  }
}

/// PageRoute using the shared Beacon transition — replaces
/// MaterialPageRoute for in-app pushes so every screen enters with the
/// same fade + 12px slide + 0.985 scale.
class MotionPageRoute<T> extends PageRouteBuilder<T> {
  MotionPageRoute({required Widget page, super.settings})
      : super(
          transitionDuration: AppMotion.standard,
          reverseTransitionDuration: const Duration(milliseconds: 220),
          pageBuilder: (_, __, ___) => page,
          transitionsBuilder: AppMotion.pageTransition,
        );
}
