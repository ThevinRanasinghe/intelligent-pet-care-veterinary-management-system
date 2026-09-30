import 'package:flutter/widgets.dart';
import '../theme/app_spacing.dart';

/// One-shot entrance animation: fade + 8 px upward slide, per the design
/// spec ("animate changes, not everything"). [delayIndex] staggers list
/// items; the delay is capped so deep lists stay snappy.
class FadeSlideIn extends StatefulWidget {
  final Widget child;

  /// Stagger index — each step adds 40 ms, capped at 5.
  final int delayIndex;

  const FadeSlideIn({super.key, required this.child, this.delayIndex = 0});

  @override
  State<FadeSlideIn> createState() => _FadeSlideInState();
}

class _FadeSlideInState extends State<FadeSlideIn>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: AppDurations.entrance,
  );
  late final Animation<double> _opacity =
      CurvedAnimation(parent: _controller, curve: Curves.easeOut);
  late final Animation<Offset> _offset = Tween<Offset>(
    begin: const Offset(0, 8),
    end: Offset.zero,
  ).animate(CurvedAnimation(parent: _controller, curve: Curves.easeOut));

  @override
  void initState() {
    super.initState();
    final delay = Duration(milliseconds: 40 * widget.delayIndex.clamp(0, 5));
    if (delay == Duration.zero) {
      _controller.forward();
    } else {
      Future.delayed(delay, () {
        if (mounted) _controller.forward();
      });
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
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
