import 'package:flutter/material.dart';

/// Beacon Pet Health design tokens — extracted from the React web app
/// (frontend/web/src/styles.css petcare-* custom properties + badge
/// classes). UI only; keep in sync with the web tokens.
abstract final class AppColors {
  // ---- Brand ------------------------------------------------------------
  /// Primary accent "petcare-yellow".
  static const Color primary = Color(0xFFFFC107);

  /// Hover/active deeper yellow.
  static const Color primaryDark = Color(0xFFE6AB00);

  /// Soft yellow tint (--petcare-yellow-light).
  static const Color primarySoft = Color(0xFFFFF3CC);

  /// "petcare-black": logo tiles, dark nav, primary text.
  static const Color black = Color(0xFF111111);

  /// Page/scaffold background (--petcare-cream).
  static const Color cream = Color(0xFFFFFBEF);

  /// Secondary text (--petcare-muted).
  static const Color muted = Color(0xFF6B6B6B);

  /// Eyebrow/accent labels (--petcare-orange).
  static const Color orange = Color(0xFFF26422);

  /// Neutral decorative (--petcare-neutral).
  static const Color neutral = Color(0xFFB8B49C);

  /// Near-black text used on the yellow primary buttons.
  static const Color onPrimary = Color(0xFF171717);

  /// Selected/highlighted card background (inline-styled web pages).
  static const Color selectedBg = Color(0xFFFFFBEA);

  /// Selected/highlighted card border.
  static const Color selectedBorder = Color(0xFFEADF9C);

  // ---- Surfaces & lines ---------------------------------------------------
  static const Color surface = Color(0xFFFFFFFF);

  /// Input fill / soft surface (--surface-soft, #fbfdfc/#f8fbfa).
  static const Color surfaceSoft = Color(0xFFFBFDFC);

  /// 1px card/input border (--line).
  static const Color line = Color(0xFFE8E4D8);

  /// Muted caption text on light surfaces.
  static const Color caption = Color(0xFF65736E);

  // ---- Semantic / status (styles.css .badge-*) -----------------------------
  static const Color successText = Color(0xFF2E7D32);
  static const Color successBg = Color(0xFFEDF8F2);
  static const Color successBorder = Color(0xFFD6ECDF);

  static const Color warningText = Color(0xFFA35D00);
  static const Color warningBg = Color(0xFFFFF5E6);
  static const Color warningBorder = Color(0xFFF5DFBA);

  static const Color dangerText = Color(0xFFD32F2F);
  static const Color dangerBg = Color(0xFFFFF0F0);
  static const Color dangerBorder = Color(0xFFF2D0D0);

  /// Destructive button red (--danger / .btn-danger).
  static const Color danger = Color(0xFFB33A3A);

  static const Color infoText = Color(0xFF3F6593);
  static const Color infoBg = Color(0xFFEEF4FF);
  static const Color infoBorder = Color(0xFFDBE5F6);

  static const Color neutralText = Color(0xFF66736F);
  static const Color neutralBg = Color(0xFFF0F4F3);
  static const Color neutralBorder = Color(0xFFE1E8E6);

  /// Inactive item colour on the dark #111 nav surface.
  static const Color navInactive = Color(0xB3FFFFFF); // white70

  /// Initials avatar background (web .avatar/.top-avatar).
  static const Color avatarBg = Color(0xFFD7EEE8);
  static const Color avatarText = Color(0xFF084E45);
}
