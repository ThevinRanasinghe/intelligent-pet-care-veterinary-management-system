import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';
import 'pressable_scale.dart';

/// Beacon card — renders a themed [Card] (white, radius 16, #E8E4D8
/// border, subtle shadow via cardTheme) with standard 16px padding.
/// `AppCard.detail` is the 24px-radius / 24px-padding hero-detail variant.
/// An optional [onTap] wraps the content in an InkWell and adds the
/// 0.97 press-scale feedback.
class AppCard extends StatelessWidget {
  final Widget child;
  final VoidCallback? onTap;
  final EdgeInsetsGeometry padding;
  final EdgeInsetsGeometry? margin;
  final Color? color;
  final double radius;

  const AppCard({
    super.key,
    required this.child,
    this.onTap,
    this.padding = const EdgeInsets.all(16),
    this.margin,
    this.color,
    this.radius = AppRadius.card,
  });

  /// Hero/detail card variant: 24px radius + 24px padding.
  const AppCard.detail({
    super.key,
    required this.child,
    this.onTap,
    this.margin,
    this.color,
    this.radius = AppRadius.large,
  }) : padding = const EdgeInsets.all(24);

  @override
  Widget build(BuildContext context) {
    final content = Padding(padding: padding, child: child);
    final card = Card(
      margin: margin,
      color: color,
      clipBehavior: onTap != null ? Clip.antiAlias : Clip.none,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(radius),
        side: const BorderSide(color: AppColors.line),
      ),
      child: onTap == null ? content : InkWell(onTap: onTap, child: content),
    );
    // PressableScale animates the press without claiming the tap — the
    // inner InkWell still fires onTap and shows the ripple.
    return PressableScale(child: card);
  }
}
