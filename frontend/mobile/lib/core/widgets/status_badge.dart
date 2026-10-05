import 'package:flutter/material.dart';
import '../motion/app_motion.dart';
import '../theme/app_colors.dart';

/// Semantic colour pair for a status (styles.css .badge-* classes).
class _BadgeColors {
  final Color text;
  final Color bg;
  final Color border;
  const _BadgeColors(this.text, this.bg, this.border);
}

/// Pill status badge matching the web app's .badge-* classes and the
/// Beacon StatusChip spec (pill shape, 6px status dot, compact padding,
/// ~11px bold text on a tinted background).
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
        return const _BadgeColors(AppColors.successText, AppColors.successBg,
            AppColors.successBorder);
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
        return const _BadgeColors(AppColors.warningText, AppColors.warningBg,
            AppColors.warningBorder);
      case 'cancelled':
      case 'canceled':
      case 'rejected':
      case 'declined':
      case 'failed':
      case 'expired':
        return const _BadgeColors(
            AppColors.dangerText, AppColors.dangerBg, AppColors.dangerBorder);
      default:
        return const _BadgeColors(AppColors.neutralText, AppColors.neutralBg,
            AppColors.neutralBorder);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = _colorsFor(status);
    // Quick crossfade when the status changes (e.g. after a refresh).
    return AnimatedSwitcher(
      duration: AppMotion.fast,
      child: Container(
        key: ValueKey(status),
        constraints: const BoxConstraints(minHeight: 23),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
        decoration: BoxDecoration(
          color: c.bg,
          borderRadius: BorderRadius.circular(99),
          border: Border.all(color: c.border),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 6,
              height: 6,
              decoration: BoxDecoration(color: c.text, shape: BoxShape.circle),
            ),
            const SizedBox(width: 5),
            Text(
              status,
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w700,
                color: c.text,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
