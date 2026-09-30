import 'package:flutter/material.dart';

/// Beacon card — renders a themed [Card] (white, radius 17, #e2e9e6
/// border, soft shadow via cardTheme) with standard ~16-20px padding.
/// An optional [onTap] wraps the content in an InkWell.
class AppCard extends StatelessWidget {
  final Widget child;
  final VoidCallback? onTap;
  final EdgeInsetsGeometry padding;
  final EdgeInsetsGeometry? margin;
  final Color? color;

  const AppCard({
    super.key,
    required this.child,
    this.onTap,
    this.padding = const EdgeInsets.all(16),
    this.margin,
    this.color,
  });

  @override
  Widget build(BuildContext context) {
    final content = Padding(padding: padding, child: child);
    return Card(
      margin: margin,
      color: color,
      clipBehavior: onTap != null ? Clip.antiAlias : Clip.none,
      child: onTap == null ? content : InkWell(onTap: onTap, child: content),
    );
  }
}
