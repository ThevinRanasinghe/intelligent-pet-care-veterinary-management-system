import 'package:flutter/material.dart';
import 'app_colors.dart';

/// Type scale for the mobile PetCare UI.
///
/// Large heading 26–28 · section heading 18–20 · body 14–16 ·
/// caption 12–13 · button 14–16. Bold/semi-bold is reserved for
/// headings; body text stays regular for readability.
class AppTextStyles {
  AppTextStyles._();

  /// Hero text — ~30px black weight (login greeting, focal screens).
  static const TextStyle hero = TextStyle(
    fontSize: 30,
    fontWeight: FontWeight.w900,
    letterSpacing: -0.8,
    color: AppColors.black,
    height: 1.15,
  );

  /// Page title — ~18px extra-bold (TopBar titles).
  static const TextStyle pageTitle = TextStyle(
    fontSize: 18,
    fontWeight: FontWeight.w800,
    letterSpacing: -0.4,
    color: AppColors.black,
  );

  /// Money totals — ~18px extra-bold.
  static const TextStyle money = TextStyle(
    fontSize: 18,
    fontWeight: FontWeight.w800,
    letterSpacing: -0.3,
    color: AppColors.black,
  );

  /// Status pill text — 10–11px bold.
  static const TextStyle status = TextStyle(
    fontSize: 11,
    fontWeight: FontWeight.w800,
  );

  /// Field label above inputs — 12px semibold.
  static const TextStyle fieldLabel = TextStyle(
    fontSize: 12,
    fontWeight: FontWeight.w700,
    color: AppColors.black,
  );

  /// Small yellow eyebrow label used on the dark action card.
  static const TextStyle eyebrow = TextStyle(
    fontSize: 11,
    fontWeight: FontWeight.w800,
    letterSpacing: 1.2,
    color: AppColors.primary,
  );

  static const TextStyle display = TextStyle(
    fontSize: 26,
    fontWeight: FontWeight.w800,
    letterSpacing: -0.6,
    color: AppColors.black,
    height: 1.2,
  );

  static const TextStyle heading = TextStyle(
    fontSize: 20,
    fontWeight: FontWeight.w700,
    letterSpacing: -0.3,
    color: AppColors.black,
  );

  static const TextStyle sectionTitle = TextStyle(
    fontSize: 18,
    fontWeight: FontWeight.w700,
    letterSpacing: -0.2,
    color: AppColors.black,
  );

  static const TextStyle cardTitle = TextStyle(
    fontSize: 15,
    fontWeight: FontWeight.w700,
    color: AppColors.black,
  );

  static const TextStyle body = TextStyle(
    fontSize: 15,
    fontWeight: FontWeight.w400,
    color: AppColors.black,
    height: 1.4,
  );

  static const TextStyle bodyMuted = TextStyle(
    fontSize: 13,
    fontWeight: FontWeight.w400,
    color: AppColors.muted,
    height: 1.35,
  );

  static const TextStyle caption = TextStyle(
    fontSize: 12,
    fontWeight: FontWeight.w400,
    color: AppColors.caption,
  );

  static const TextStyle button = TextStyle(
    fontSize: 15,
    fontWeight: FontWeight.w700,
  );
}
