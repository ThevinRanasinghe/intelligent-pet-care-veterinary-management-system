import 'package:flutter/material.dart';
import '../../core/theme/app_colors.dart';
import '../../core/widgets/brand_logo.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/pop_in.dart';

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
            PopIn(
              beginScale: 0.85,
              peakScale: 1.04,
              child: BrandLogoTile(size: 80),
            ),
            SizedBox(height: 20),
            FadeSlideIn(
              delay: Duration(milliseconds: 150),
              distance: 10,
              child: Text(
                'BEACON PET HEALTH',
                style: TextStyle(
                  color: AppColors.black,
                  fontSize: 24,
                  fontWeight: FontWeight.w800,
                  letterSpacing: 2,
                ),
              ),
            ),
            SizedBox(height: 8),
            FadeSlideIn(
              delay: Duration(milliseconds: 260),
              distance: 8,
              child: Text(
                'Veterinary Care for Your Pets',
                style: TextStyle(color: AppColors.muted, fontSize: 14),
              ),
            ),
            SizedBox(height: 32),
            FadeSlideIn(
              delay: Duration(milliseconds: 380),
              distance: 0,
              child: CircularProgressIndicator(color: AppColors.black),
            ),
          ],
        ),
      ),
    );
  }
}
