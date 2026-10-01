import 'dart:async';

import 'package:flutter/material.dart';

/// Focal pop-in animation — scale 0.4 → 1.12 → 1 with a fade, ~420ms.
/// Reserved for important focal elements (splash logo, success check).
/// Honours the platform's disable-animations accessibility flag.
class PopIn extends StatefulWidget {
  final Widget child;
  final Duration duration;

  /// Starting scale (default 0.4).
  final double beginScale;

  /// Overshoot peak before settling (default 1.12).
  final double peakScale;

  /// Optional delay before the pop starts.
  final Duration delay;

  const PopIn({
    super.key,
    required this.child,
    this.duration = const Duration(milliseconds: 420),
    this.beginScale = 0.4,
    this.peakScale = 1.12,
    this.delay = Duration.zero,
  });

  @override
  State<PopIn> createState() => _PopInState();
}

class _PopInState extends State<PopIn> with SingleTickerProviderStateMixin {
  late final AnimationController _controller =
      AnimationController(vsync: this, duration: widget.duration);

  late final Animation<double> _scale = TweenSequence<double>([
    TweenSequenceItem(
      tween: Tween(begin: widget.beginScale, end: widget.peakScale)
          .chain(CurveTween(curve: Curves.easeOut)),
      weight: 60,
    ),
    TweenSequenceItem(
      tween: Tween(begin: widget.peakScale, end: 1.0)
          .chain(CurveTween(curve: Curves.easeOut)),
      weight: 40,
    ),
  ]).animate(_controller);

  late final Animation<double> _opacity = Tween(begin: 0.0, end: 1.0).animate(
    CurvedAnimation(
      parent: _controller,
      curve: const Interval(0.0, 0.35, curve: Curves.easeOut),
    ),
  );

  Timer? _delayTimer;

  @override
  void initState() {
    super.initState();
    if (widget.delay == Duration.zero) {
      _controller.forward();
    } else {
      _delayTimer = Timer(widget.delay, _controller.forward);
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
    if (MediaQuery.of(context).disableAnimations) return widget.child;
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) => Opacity(
        opacity: _opacity.value,
        child: Transform.scale(scale: _scale.value, child: child),
      ),
      child: widget.child,
    );
  }
}
