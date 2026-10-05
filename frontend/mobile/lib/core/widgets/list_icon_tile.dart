import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';

/// Small soft-yellow icon tile used as the leading avatar on list cards
/// (appointments, consultations, bills).
class ListIconTile extends StatelessWidget {
  final IconData icon;
  final double size;

  const ListIconTile({super.key, required this.icon, this.size = 40});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: AppColors.primarySoft,
        borderRadius: BorderRadius.circular(AppRadius.cardSmall - 3),
      ),
      child: Icon(icon, color: AppColors.black, size: size / 2),
    );
  }
}
