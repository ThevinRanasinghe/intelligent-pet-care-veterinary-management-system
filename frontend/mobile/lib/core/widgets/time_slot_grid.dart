import 'package:flutter/material.dart';
import '../motion/app_motion.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';
import 'fade_slide_in.dart';
import 'pressable_scale.dart';

/// One selectable time slot in [TimeSlotGrid].
class TimeSlot {
  final String label;
  final String value;
  final bool available;

  const TimeSlot({
    required this.label,
    required this.value,
    this.available = true,
  });
}

/// Beacon two-column time grid. Available slots are white cards; the
/// selected slot is yellow; unavailable slots are muted with a struck
/// label and ignore taps.
class TimeSlotGrid extends StatelessWidget {
  final List<TimeSlot> slots;
  final String? selected;
  final ValueChanged<String> onSelected;

  const TimeSlotGrid({
    super.key,
    required this.slots,
    required this.onSelected,
    this.selected,
  });

  @override
  Widget build(BuildContext context) {
    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: AppSpacing.xs,
      crossAxisSpacing: AppSpacing.xs,
      childAspectRatio: 3.4,
      children: [
        // Stagger only the first visible set — capped at 6 tiles so the
        // total entrance stays under ~350 ms.
        for (var i = 0; i < slots.length; i++)
          FadeSlideIn(
            delay: Duration(milliseconds: 40 * i.clamp(0, 6)),
            distance: 8,
            child: TimeSlotTile(
              slot: slots[i],
              selected: selected == slots[i].value,
              onTap:
                  slots[i].available ? () => onSelected(slots[i].value) : null,
            ),
          ),
      ],
    );
  }
}

/// Single time-slot tile inside [TimeSlotGrid].
class TimeSlotTile extends StatelessWidget {
  final TimeSlot slot;
  final bool selected;
  final VoidCallback? onTap;

  const TimeSlotTile({
    super.key,
    required this.slot,
    required this.selected,
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final enabled = slot.available;
    return PressableScale(
      onTap: onTap,
      enabled: enabled,
      // Selecting a tile plays a small 0.98 → 1 settle; deselecting
      // plays nothing (identity tween).
      child: TweenAnimationBuilder<double>(
        key: ValueKey(selected),
        tween: selected
            ? Tween(begin: 0.98, end: 1.0)
            : Tween(begin: 1.0, end: 1.0),
        duration: AppMotion.fast,
        curve: AppMotion.easeOut,
        builder: (context, scale, child) =>
            Transform.scale(scale: scale, child: child),
        child: AnimatedContainer(
          duration: AppMotion.standard,
          curve: AppMotion.easeInOut,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: selected
                ? AppColors.primary
                : enabled
                    ? AppColors.surface
                    : AppColors.neutralBg,
            borderRadius: BorderRadius.circular(12),
            border: Border.all(
              color: selected
                  ? AppColors.primaryDark
                  : enabled
                      ? AppColors.line
                      : AppColors.neutralBorder,
            ),
          ),
          child: Text(
            slot.label,
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w700,
              color: enabled ? AppColors.black : AppColors.neutral,
              decoration: enabled ? null : TextDecoration.lineThrough,
            ),
          ),
        ),
      ),
    );
  }
}
