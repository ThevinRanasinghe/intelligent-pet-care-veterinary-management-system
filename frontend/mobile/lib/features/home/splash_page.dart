import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/widgets/brand_logo.dart';

/// Branded splash shown while the saved auth session is being restored —
/// mirrors the web auth-brand-panel (yellow bg, black logo tile, black
/// wordmark, muted subtext).
class SplashPage extends StatelessWidget {
  const SplashPage({super.key});

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      backgroundColor: AppColors.primary,
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            BrandLogoTile(size: 80),
            SizedBox(height: 20),
            Text(
              'BEACON PET HEALTH',
              style: TextStyle(
                color: AppColors.black,
                fontSize: 24,
                fontWeight: FontWeight.w800,
                letterSpacing: 2,
              ),
            ),
            SizedBox(height: 8),
            Text(
              'Veterinary Care for Your Pets',
              style: TextStyle(color: AppColors.muted, fontSize: 14),
            ),
            SizedBox(height: 32),
            CircularProgressIndicator(color: AppColors.black),
          ],
        ),
      ),
    );
  }
}
