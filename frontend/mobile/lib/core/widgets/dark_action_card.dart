import 'package:flutter/material.dart';
import '../motion/app_motion.dart';
import '../theme/app_colors.dart';
import '../theme/app_shadows.dart';
import '../theme/app_spacing.dart';
import '../theme/app_text_styles.dart';
import 'pop_in.dart';
import 'pressable_scale.dart';

/// Beacon DarkActionCard — #111 surface, 24px radius, 20px padding, yellow
/// eyebrow, white headline, muted caption and a yellow circular arrow
/// button. Used for major CTAs such as "Book a visit".
class DarkActionCard extends StatelessWidget {
  final String eyebrow;
  final String title;
  final String? subtitle;
  final VoidCallback? onTap;
  final IconData arrowIcon;
  final String? arrowTooltip;

  const DarkActionCard({
    super.key,
    required this.eyebrow,
    required this.title,
    this.subtitle,
    this.onTap,
    this.arrowIcon = Icons.arrow_forward,
    this.arrowTooltip,
  });

  @override
  Widget build(BuildContext context) {
    return PressableScale(
      onTap: onTap,
      // Large focal CTA — a softer 0.985 press (vs the default 0.97).
      pressScale: 0.985,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(AppSpacing.lg),
        decoration: BoxDecoration(
          color: AppColors.black,
          borderRadius: BorderRadius.circular(AppRadius.large),
          boxShadow: AppShadows.cardList,
        ),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(eyebrow.toUpperCase(), style: AppTextStyles.eyebrow),
                  const SizedBox(height: 6),
                  Text(
                    title,
                    style: const TextStyle(
                      fontSize: 17,
                      fontWeight: FontWeight.w800,
                      letterSpacing: -0.3,
                      color: Colors.white,
                    ),
                  ),
                  if (subtitle != null) ...[
                    const SizedBox(height: 4),
                    Text(
                      subtitle!,
                      style: const TextStyle(
                        fontSize: 12,
                        color: Color(0xB3FFFFFF),
                      ),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: AppSpacing.sm),
            // Yellow arrow button pops in a beat after the card appears.
            PopIn(
              beginScale: 0.6,
              peakScale: 1.06,
              duration: AppMotion.standard,
              delay: const Duration(milliseconds: 120),
              child: Container(
                width: 44,
                height: 44,
                decoration: const BoxDecoration(
                  color: AppColors.primary,
                  shape: BoxShape.circle,
                ),
                child: Icon(arrowIcon, color: AppColors.black, size: 20),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
