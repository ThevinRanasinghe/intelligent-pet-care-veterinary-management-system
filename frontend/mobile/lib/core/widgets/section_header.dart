import 'package:flutter/material.dart';
import '../theme/app_colors.dart';

/// Section heading: 16px w700 black title + optional trailing widget
/// (mirrors the web .card-header layout).
class SectionHeader extends StatelessWidget {
  final String title;
  final Widget? trailing;

  const SectionHeader(this.title, {super.key, this.trailing});

  @override
  Widget build(BuildContext context) {
    final titleWidget = Text(
      title,
      style: const TextStyle(
        fontSize: 16,
        fontWeight: FontWeight.w700,
        color: AppColors.black,
        letterSpacing: -0.2,
      ),
    );
    if (trailing == null) return titleWidget;
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [titleWidget, trailing!],
    );
  }
}
