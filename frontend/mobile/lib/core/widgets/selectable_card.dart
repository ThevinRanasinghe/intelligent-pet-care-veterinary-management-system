import 'package:flutter/material.dart';
import '../motion/app_motion.dart';
import '../theme/app_colors.dart';
import '../theme/app_shadows.dart';
import '../theme/app_spacing.dart';
import 'pressable_scale.dart';

/// Selectable list card used by the booking wizard (pet + clinic
/// pickers): white card that interpolates to a soft-yellow fill and
/// yellow ring when [selected], with the check mark popping in
/// (scale 0.6 → 1 + fade) instead of appearing instantly.
class SelectableCard extends StatelessWidget {
  final Widget child;
  final bool selected;
  final VoidCallback? onTap;
  final EdgeInsetsGeometry margin;
  final EdgeInsetsGeometry padding;
  final double radius;

  const SelectableCard({
    super.key,
    required this.child,
    this.selected = false,
    this.onTap,
    this.margin = const EdgeInsets.only(bottom: AppSpacing.xs),
    this.padding = const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
    this.radius = AppRadius.card,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: margin,
      child: PressableScale(
        onTap: onTap,
        child: AnimatedContainer(
          duration: AppMotion.standard,
          curve: AppMotion.easeInOut,
          padding: padding,
          decoration: BoxDecoration(
            color: selected ? AppColors.selectedBg : AppColors.surface,
            borderRadius: BorderRadius.circular(radius),
            border: Border.all(
              color: selected ? AppColors.primaryDark : AppColors.line,
              width: selected ? 1.5 : 1,
            ),
            boxShadow: selected ? AppShadows.cardList : const [],
          ),
          child: Row(
            children: [
              Expanded(child: child),
              const SizedBox(width: AppSpacing.sm),
              AnimatedSwitcher(
                duration: AppMotion.fast,
                transitionBuilder: (child, animation) => FadeTransition(
                  opacity: animation,
                  child: ScaleTransition(
                    scale: Tween<double>(begin: 0.6, end: 1.0).animate(
                      CurvedAnimation(
                          parent: animation, curve: AppMotion.expressive),
                    ),
                    child: child,
                  ),
                ),
                child: selected
                    ? const Icon(Icons.check_circle,
                        key: ValueKey('sel'), color: AppColors.primaryDark)
                    : const Icon(Icons.circle_outlined,
                        key: ValueKey('unsel'), color: AppColors.neutral),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
