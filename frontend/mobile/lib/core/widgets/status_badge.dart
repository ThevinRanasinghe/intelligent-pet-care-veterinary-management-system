import 'package:flutter/material.dart';
import '../theme/app_colors.dart';

/// Semantic colour pair for a status (styles.css .badge-* classes).
class _BadgeColors {
  final Color text;
  final Color bg;
  final Color border;
  const _BadgeColors(this.text, this.bg, this.border);
}

/// Pill status badge matching the web app's .badge-* classes
/// (radius ~99, min-height 23, 11-12px w700 text on a tinted bg).
class StatusBadge extends StatelessWidget {
  final String status;

  const StatusBadge(this.status, {super.key});

  static _BadgeColors _colorsFor(String status) {
    switch (status.toLowerCase().replaceAll(' ', '')) {
      case 'confirmed':
      case 'completed':
      case 'approved':
      case 'paid':
      case 'finalised':
      case 'finalized':
      case 'available':
      case 'resolved':
        return const _BadgeColors(
            AppColors.successText, AppColors.successBg, AppColors.successBorder);
      case 'submitted':
      case 'scheduled':
      case 'reserved':
      case 'inprogress':
      case 'inreview':
      case 'booked':
        return const _BadgeColors(
            AppColors.infoText, AppColors.infoBg, AppColors.infoBorder);
      case 'pending':
      case 'pendingapproval':
      case 'draft':
      case 'unpaid':
      case 'partiallypaid':
      case 'overdue':
        return const _BadgeColors(
            AppColors.warningText, AppColors.warningBg, AppColors.warningBorder);
      case 'cancelled':
      case 'canceled':
      case 'rejected':
      case 'declined':
      case 'failed':
      case 'expired':
        return const _BadgeColors(
            AppColors.dangerText, AppColors.dangerBg, AppColors.dangerBorder);
      default:
        return const _BadgeColors(
            AppColors.neutralText, AppColors.neutralBg, AppColors.neutralBorder);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = _colorsFor(status);
    return Container(
      constraints: const BoxConstraints(minHeight: 23),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: c.bg,
        borderRadius: BorderRadius.circular(99),
        border: Border.all(color: c.border),
      ),
      child: Text(
        status,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w700,
          color: c.text,
        ),
      ),
    );
  }
}
