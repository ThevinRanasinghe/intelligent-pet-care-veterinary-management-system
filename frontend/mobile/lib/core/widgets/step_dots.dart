import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';

/// Step progress indicator for the booking wizard:
/// filled dot = done/current, hollow dot = pending, connected by a line.
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
                    duration: AppDurations.step,
                    height: 2,
                    color: i < currentStep
                        ? AppColors.primary
                        : AppColors.line,
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
                    color: i <= currentStep
                        ? AppColors.black
                        : AppColors.caption,
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
    return AnimatedContainer(
      duration: AppDurations.step,
      width: 22,
      height: 22,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: done || active ? AppColors.primary : AppColors.surface,
        border: Border.all(
          color: done || active ? AppColors.primary : AppColors.line,
          width: 2,
        ),
      ),
      child: Center(
        child: done
            ? const Icon(Icons.check, size: 12, color: AppColors.onPrimary)
            : Text(
                '${index + 1}',
                style: TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.w700,
                  color:
                      active ? AppColors.onPrimary : AppColors.caption,
                ),
              ),
      ),
    );
  }
}
