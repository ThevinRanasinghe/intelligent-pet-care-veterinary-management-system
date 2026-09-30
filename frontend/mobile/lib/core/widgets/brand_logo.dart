import 'package:flutter/material.dart';
import '../theme/app_colors.dart';

/// Beacon logo mark: rounded-square black tile with a paw icon —
/// mirrors the web auth-brand-logo-icon (black tile on the yellow
/// brand panel). Use [light] on dark surfaces for a yellow tile.
class BrandLogoTile extends StatelessWidget {
  final double size;
  final bool light;

  const BrandLogoTile({super.key, this.size = 56, this.light = false});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: light ? AppColors.primary : AppColors.black,
        borderRadius: BorderRadius.circular(size * 0.24),
      ),
      child: Icon(
        Icons.pets,
        size: size * 0.55,
        color: light ? AppColors.black : AppColors.primary,
      ),
    );
  }
}

/// "BEACON PET HEALTH" eyebrow + optional wordmark used on splash and
/// auth screens (mirrors .auth-brand-logo / .auth-form-eyebrow).
class BrandLockup extends StatelessWidget {
  final double logoSize;
  final Color? textColor;
  final bool center;

  const BrandLockup({
    super.key,
    this.logoSize = 56,
    this.textColor,
    this.center = true,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment:
          center ? CrossAxisAlignment.center : CrossAxisAlignment.start,
      children: [
        BrandLogoTile(size: logoSize),
        const SizedBox(height: 12),
        Text(
          'BEACON PET HEALTH',
          textAlign: center ? TextAlign.center : TextAlign.start,
          style: TextStyle(
            color: textColor ?? AppColors.black,
            fontSize: 16,
            fontWeight: FontWeight.w800,
            letterSpacing: 2,
          ),
        ),
      ],
    );
  }
}
