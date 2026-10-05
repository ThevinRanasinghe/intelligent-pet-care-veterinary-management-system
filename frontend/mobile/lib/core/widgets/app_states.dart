import 'package:flutter/material.dart';
import '../motion/app_motion.dart';
import '../theme/app_colors.dart';
import 'fade_slide_in.dart';
import 'pop_in.dart';

/// Centred loading indicator (accent coloured via progressIndicatorTheme).
class AppLoading extends StatelessWidget {
  const AppLoading({super.key});

  @override
  Widget build(BuildContext context) =>
      const Center(child: CircularProgressIndicator());
}

/// Friendly empty state: muted icon + message (+ optional hint line and
/// a dark action button). Inside scrollables, use [padding] to push it
/// down like the previous `SizedBox(height: 200)` spacers did.
class AppEmptyState extends StatelessWidget {
  final String message;
  final IconData icon;
  final String? hint;
  final EdgeInsetsGeometry padding;

  /// Optional CTA rendered under the message (e.g. "Add Pet").
  final String? actionLabel;
  final VoidCallback? onAction;

  const AppEmptyState({
    super.key,
    required this.message,
    this.icon = Icons.inbox_outlined,
    this.hint,
    this.padding = const EdgeInsets.only(top: 120),
    this.actionLabel,
    this.onAction,
  });

  @override
  Widget build(BuildContext context) {
    // Gentle one-shot entrance: icon pops, message/hint/CTA stagger in.
    return Padding(
      padding: padding,
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            PopIn(
              beginScale: 0.7,
              peakScale: 1.04,
              duration: AppMotion.standard,
              child: Icon(icon, size: 40, color: AppColors.neutral),
            ),
            const SizedBox(height: 8),
            FadeSlideIn(
              delay: const Duration(milliseconds: 80),
              distance: 8,
              child: Text(
                message,
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 13, color: AppColors.muted),
              ),
            ),
            if (hint != null) ...[
              const SizedBox(height: 4),
              FadeSlideIn(
                delay: const Duration(milliseconds: 130),
                distance: 6,
                child: Text(
                  hint!,
                  textAlign: TextAlign.center,
                  style:
                      const TextStyle(fontSize: 11, color: AppColors.caption),
                ),
              ),
            ],
            if (actionLabel != null && onAction != null) ...[
              const SizedBox(height: 16),
              FadeSlideIn(
                delay: const Duration(milliseconds: 180),
                distance: 8,
                child: FilledButton(
                    onPressed: onAction, child: Text(actionLabel!)),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// Error state: danger icon + message + Retry button.
class AppErrorState extends StatelessWidget {
  final String message;
  final VoidCallback? onRetry;

  const AppErrorState({super.key, required this.message, this.onRetry});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: FadeSlideIn(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.error_outline,
                  size: 48, color: AppColors.dangerText),
              const SizedBox(height: 8),
              Text(message, textAlign: TextAlign.center),
              if (onRetry != null) ...[
                const SizedBox(height: 16),
                FilledButton(onPressed: onRetry, child: const Text('Retry')),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
