import 'dart:async';

import 'package:flutter/widgets.dart';
import '../motion/app_motion.dart';

/// One-shot entrance animation: fade + small upward slide, per the
/// "animate changes, not everything" spec. [delayIndex] staggers list
/// items in 40 ms steps (capped at 5); use [delay] for explicit,
/// per-section timing on page entrances.
class FadeSlideIn extends StatefulWidget {
  final Widget child;

  /// Stagger index — each step adds 40 ms, capped at 5.
  final int delayIndex;

  /// Explicit delay override (takes precedence over [delayIndex]).
  final Duration delay;

  /// Vertical translation distance in px (default 10).
  final double distance;

  final Duration duration;
  final Curve curve;

  const FadeSlideIn({
    super.key,
    required this.child,
    this.delayIndex = 0,
    this.delay = Duration.zero,
    this.distance = 10,
    this.duration = AppMotion.standard,
    this.curve = AppMotion.easeOut,
  });

  @override
  State<FadeSlideIn> createState() => _FadeSlideInState();
}

class _FadeSlideInState extends State<FadeSlideIn>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: widget.duration,
  );
  late final Animation<double> _opacity =
      CurvedAnimation(parent: _controller, curve: widget.curve);
  late final Animation<Offset> _offset = Tween<Offset>(
    begin: Offset(0, widget.distance),
    end: Offset.zero,
  ).animate(CurvedAnimation(parent: _controller, curve: widget.curve));

  Timer? _delayTimer;

  @override
  void initState() {
    super.initState();
    final delay = widget.delay != Duration.zero
        ? widget.delay
        : Duration(milliseconds: 40 * widget.delayIndex.clamp(0, 5));
    if (delay == Duration.zero) {
      _controller.forward();
    } else {
      _delayTimer = Timer(delay, _controller.forward);
    }
  }

  @override
  void dispose() {
    _delayTimer?.cancel();
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (AppMotion.reduceMotion(context)) return widget.child;
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) => Opacity(
        opacity: _opacity.value,
        child: Transform.translate(offset: _offset.value, child: child),
      ),
      child: widget.child,
    );
  }
}
