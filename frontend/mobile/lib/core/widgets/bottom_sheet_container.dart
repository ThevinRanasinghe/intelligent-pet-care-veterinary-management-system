import 'package:flutter/material.dart';
import '../motion/app_motion.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';

/// Beacon bottom sheet surface: white, 24px top corners (themed), drag
/// handle, 24px horizontal padding and safe-area bottom padding.
class BottomSheetContainer extends StatelessWidget {
  final Widget child;
  final String? title;

  const BottomSheetContainer({super.key, required this.child, this.title});

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      top: false,
      child: Padding(
        padding: EdgeInsets.only(
          left: AppSpacing.xl,
          right: AppSpacing.xl,
          top: AppSpacing.xs,
          bottom: MediaQuery.of(context).viewInsets.bottom + AppSpacing.xl,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Center(
              child: Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: AppColors.neutral,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            if (title != null) ...[
              Text(
                title!,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w800,
                  letterSpacing: -0.3,
                  color: AppColors.black,
                ),
              ),
              const SizedBox(height: AppSpacing.md),
            ],
            Flexible(child: child),
          ],
        ),
      ),
    );
  }
}

/// Shows a Beacon-styled modal bottom sheet constrained to the 448px
/// mobile rail (matching the centred-shell behaviour on wide screens).
Future<T?> showAppBottomSheet<T>(BuildContext context, Widget child) {
  return showModalBottomSheet<T>(
    context: context,
    isScrollControlled: true,
    constraints: const BoxConstraints(maxWidth: 448),
    // Entry ~250 ms ease-out slide, slightly quicker 180 ms exit.
    sheetAnimationStyle: const AnimationStyle(
      duration: AppMotion.standard,
      reverseDuration: Duration(milliseconds: 180),
      curve: AppMotion.easeOut,
    ),
    builder: (_) => child,
  );
}
