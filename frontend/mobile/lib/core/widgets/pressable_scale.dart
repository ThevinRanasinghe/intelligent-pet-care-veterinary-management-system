import 'package:flutter/widgets.dart';
import '../theme/app_spacing.dart';

/// Lightweight press feedback: scales the child 1.0 → 0.97 → 1.0 while the
/// pointer is down (~150ms per the Beacon spec). The press animation runs
/// even when [onTap] is null so it can wrap cards that use an inner
/// InkWell; set [enabled] to false to opt out.
class PressableScale extends StatefulWidget {
  final Widget child;
  final VoidCallback? onTap;
  final bool enabled;

  /// Pressed scale — 0.97 for normal tappables, 0.985 for large focal
  /// surfaces like the dark booking CTA.
  final double pressScale;

  const PressableScale({
    super.key,
    required this.child,
    this.onTap,
    this.enabled = true,
    this.pressScale = 0.97,
  });

  @override
  State<PressableScale> createState() => _PressableScaleState();
}

class _PressableScaleState extends State<PressableScale> {
  bool _pressed = false;

  void _set(bool value) {
    if (_pressed == value) return;
    setState(() => _pressed = value);
  }

  @override
  Widget build(BuildContext context) {
    if (!widget.enabled) return widget.child;
    return GestureDetector(
      // deferredToSibling lets an inner InkWell win the tap arena while the
      // scale animation still tracks the pointer.
      behavior: HitTestBehavior.deferToChild,
      onTapDown: (_) => _set(true),
      onTapUp: (_) => _set(false),
      onTapCancel: () => _set(false),
      onTap: widget.onTap,
      child: AnimatedScale(
        scale: _pressed ? widget.pressScale : 1.0,
        duration: AppDurations.press,
        curve: Curves.easeOut,
        child: widget.child,
      ),
    );
  }
}
