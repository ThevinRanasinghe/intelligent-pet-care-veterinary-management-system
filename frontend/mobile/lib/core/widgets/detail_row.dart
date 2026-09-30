import 'package:flutter/material.dart';
import '../theme/app_colors.dart';

/// Label / value row used on detail pages — label is a fixed-width
/// 11px-bold muted column (web form-label style), value is flexible.
class DetailRow extends StatelessWidget {
  final String label;
  final String? value;
  final Widget? child;
  final double labelWidth;

  const DetailRow(
    this.label, {
    super.key,
    this.value,
    this.child,
    this.labelWidth = 130,
  }) : assert(value != null || child != null);

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: labelWidth,
            child: Text(
              label,
              style: const TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w700,
                color: AppColors.caption,
              ),
            ),
          ),
          Expanded(
            child: child ??
                Text(value!, style: const TextStyle(fontSize: 13)),
          ),
        ],
      ),
    );
  }
}
