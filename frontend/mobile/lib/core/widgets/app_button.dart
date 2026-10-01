import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import 'pressable_scale.dart';

/// Beacon button hierarchy.
enum AppButtonVariant {
  /// Dark #111 surface, white text — primary CTAs (Sign in, Save, Next).
  dark,

  /// Yellow #FFC107, dark text — brand/important workflow actions.
  yellow,

  /// Soft neutral surface, dark text — Cancel and neutral controls.
  secondary,

  /// Red — irreversible confirmations only.
  destructive,
}

/// Reusable Beacon button: radius 12, 14px bold label, ~48px tall,
/// optional leading icon and loading spinner.
class AppButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final AppButtonVariant variant;
  final bool loading;
  final bool expand;

  const AppButton({
    super.key,
    required this.label,
    this.onPressed,
    this.icon,
    this.variant = AppButtonVariant.dark,
    this.loading = false,
    this.expand = true,
  });

  @override
  Widget build(BuildContext context) {
    final (bg, fg, border) = switch (variant) {
      AppButtonVariant.dark => (AppColors.black, Colors.white, AppColors.black),
      AppButtonVariant.yellow => (
          AppColors.primary,
          AppColors.onPrimary,
          AppColors.primaryDark
        ),
      AppButtonVariant.secondary => (
          AppColors.surface,
          AppColors.black,
          AppColors.line
        ),
      AppButtonVariant.destructive => (
          AppColors.dangerText,
          Colors.white,
          AppColors.dangerText
        ),
    };

    final effectiveOnPressed = loading ? null : onPressed;
    final child = ElevatedButton(
      onPressed: effectiveOnPressed,
      style: ElevatedButton.styleFrom(
        backgroundColor: bg,
        foregroundColor: fg,
        disabledBackgroundColor: bg.withValues(alpha: 0.45),
        disabledForegroundColor: fg.withValues(alpha: 0.7),
        elevation: 0,
        minimumSize: const Size(0, 48),
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(12),
          side: BorderSide(color: border),
        ),
        textStyle: const TextStyle(fontSize: 14, fontWeight: FontWeight.w800),
      ),
      child: loading
          ? SizedBox(
              height: 18,
              width: 18,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: fg,
              ),
            )
          : Row(
              mainAxisSize: MainAxisSize.min,
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                if (icon != null) ...[
                  Icon(icon, size: 18),
                  const SizedBox(width: 8),
                ],
                Flexible(
                  child: Text(
                    label,
                    overflow: TextOverflow.ellipsis,
                    maxLines: 1,
                  ),
                ),
              ],
            ),
    );

    final sized =
        expand ? SizedBox(width: double.infinity, child: child) : child;
    return PressableScale(child: sized);
  }
}
