import 'package:flutter/material.dart';
import '../motion/app_motion.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';

/// Booking-wizard step indicator per the Beacon spec: ~28px circles
/// connected by a line. Completed steps are yellow with a dark check, the
/// current step is dark with a light number, future steps are neutral.
class StepDots extends StatelessWidget {
  /// 0-based index of the current step.
  final int currentStep;
  final List<String> labels;

  const StepDots({super.key, required this.currentStep, required this.labels});

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            for (var i = 0; i < labels.length; i++) ...[
              _Dot(index: i, current: currentStep),
              if (i < labels.length - 1)
                Expanded(
                  child: AnimatedContainer(
                    duration: AppMotion.standard,
                    curve: AppMotion.easeInOut,
                    height: 2,
                    color: i < currentStep ? AppColors.primary : AppColors.line,
                  ),
                ),
            ],
          ],
        ),
        const SizedBox(height: AppSpacing.xxs),
        Row(
          children: [
            for (var i = 0; i < labels.length; i++)
              Expanded(
                child: Text(
                  labels[i],
                  textAlign: i == 0
                      ? TextAlign.start
                      : i == labels.length - 1
                          ? TextAlign.end
                          : TextAlign.center,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight:
                        i == currentStep ? FontWeight.w700 : FontWeight.w400,
                    color:
                        i <= currentStep ? AppColors.black : AppColors.caption,
                  ),
                ),
              ),
          ],
        ),
      ],
    );
  }
}

class _Dot extends StatelessWidget {
  final int index;
  final int current;

  const _Dot({required this.index, required this.current});

  @override
  Widget build(BuildContext context) {
    final done = index < current;
    final active = index == current;
    // The active dot breathes out to 1.08×; completed dots animate the
    // number → check swap instead of snapping.
    return AnimatedScale(
      scale: active ? 1.08 : 1.0,
      duration: AppMotion.standard,
      curve: AppMotion.easeInOut,
      child: AnimatedContainer(
        duration: AppMotion.standard,
        curve: AppMotion.easeInOut,
        width: 28,
        height: 28,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: done
              ? AppColors.primary
              : active
                  ? AppColors.black
                  : AppColors.surface,
          border: Border.all(
            color: done
                ? AppColors.primaryDark
                : active
                    ? AppColors.black
                    : AppColors.line,
            width: 2,
          ),
        ),
        child: Center(
          child: AnimatedSwitcher(
            duration: AppMotion.fast,
            transitionBuilder: (child, animation) => FadeTransition(
              opacity: animation,
              child: ScaleTransition(scale: animation, child: child),
            ),
            child: done
                ? const Icon(Icons.check,
                    key: ValueKey('done'), size: 14, color: AppColors.black)
                : Text(
                    '${index + 1}',
                    key: ValueKey('num-$index'),
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w800,
                      color: active ? Colors.white : AppColors.caption,
                    ),
                  ),
          ),
        ),
      ),
    );
  }
}
