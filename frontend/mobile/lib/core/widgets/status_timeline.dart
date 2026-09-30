import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';

/// Vertical status timeline: ✓ done, ● current, ○ pending — used for the
/// appointment progress view (Request Submitted → … → Completed).
class StatusTimeline extends StatelessWidget {
  final List<String> steps;

  /// Index of the in-progress step. Steps before it render as done;
  /// steps after it render as pending. Pass [steps.length] when the
  /// whole flow is finished.
  final int currentIndex;

  const StatusTimeline({
    super.key,
    required this.steps,
    required this.currentIndex,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (var i = 0; i < steps.length; i++)
          _TimelineRow(
            label: steps[i],
            done: i < currentIndex,
            active: i == currentIndex,
            isLast: i == steps.length - 1,
          ),
      ],
    );
  }
}

class _TimelineRow extends StatelessWidget {
  final String label;
  final bool done;
  final bool active;
  final bool isLast;

  const _TimelineRow({
    required this.label,
    required this.done,
    required this.active,
    required this.isLast,
  });

  @override
  Widget build(BuildContext context) {
    final icon = done
        ? const Icon(Icons.check_circle, size: 20, color: AppColors.primary)
        : active
            ? const Icon(Icons.radio_button_checked,
                size: 20, color: AppColors.primary)
            : const Icon(Icons.circle_outlined,
                size: 20, color: AppColors.neutral);
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Column(
            children: [
              icon,
              if (!isLast)
                Expanded(
                  child: Container(
                    width: 2,
                    margin: const EdgeInsets.symmetric(vertical: 2),
                    color: done ? AppColors.primary : AppColors.line,
                  ),
                ),
            ],
          ),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(
                  bottom: isLast ? 0 : AppSpacing.lg, top: 1),
              child: Text(
                label,
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: active || done
                      ? FontWeight.w700
                      : FontWeight.w400,
                  color: done || active
                      ? AppColors.black
                      : AppColors.muted,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
