import 'package:flutter/material.dart';

/// Shadow tokens per the Beacon spec: cards use a subtle `shadow-sm`
/// equivalent; floating elements (FAB, bottom sheets, dialogs) use the
/// stronger `shadow-lg` equivalent. No heavy Material elevation.
class AppShadows {
  AppShadows._();

  /// Subtle card shadow.
  static const BoxShadow card = BoxShadow(
    blurRadius: 8,
    offset: Offset(0, 2),
    color: Color(0x14000000),
  );

  /// Stronger shadow for floating elements.
  static const BoxShadow floating = BoxShadow(
    blurRadius: 18,
    offset: Offset(0, 6),
    color: Color(0x24000000),
  );

  static const List<BoxShadow> cardList = [card];
  static const List<BoxShadow> floatingList = [floating];
}
