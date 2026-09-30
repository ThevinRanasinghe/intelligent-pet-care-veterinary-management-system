import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/theme/app_text_styles.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/brand_logo.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/section_header.dart';
import '../../core/widgets/status_badge.dart';
import '../auth/auth_provider.dart';
import '../history/medical_history_page.dart';
import '../scheduling/models/appointment_slot.dart';
import '../scheduling/scheduling_provider.dart';

/// Owner home tab: greeting, primary booking CTA, next upcoming
/// appointment card, quick actions and recent activity.
class OwnerHomeTab extends StatefulWidget {
  /// Switches the MainShell bottom-nav tab (1 = My Pets, 2 = Appointments,
  /// 3 = Bills).
  final void Function(int index) onNavigateToTab;

  const OwnerHomeTab({super.key, required this.onNavigateToTab});

  @override
  State<OwnerHomeTab> createState() => _OwnerHomeTabState();
}

class _OwnerHomeTabState extends State<OwnerHomeTab> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<SchedulingProvider>().loadMyAppointments();
    });
  }

  String get _greeting {
    final hour = DateTime.now().hour;
    if (hour < 12) return 'Good Morning';
    if (hour < 17) return 'Good Afternoon';
    return 'Good Evening';
  }

  String _firstName(String name) =>
      name.trim().isEmpty ? 'Pet Owner' : name.trim().split(' ').first;

  /// Nearest Confirmed/Reserved appointment still in the future.
  Appointment? _nextAppointment(List<Appointment> appointments) {
    final now = DateTime.now();
    final upcoming = appointments.where((a) {
      if (!const {'Confirmed', 'Reserved', 'Scheduled'}.contains(a.status)) {
        return false;
      }
      final start = DateTime.tryParse(a.scheduledStart);
      return start == null || start.isAfter(now);
    }).toList()
      ..sort((a, b) => a.scheduledStart.compareTo(b.scheduledStart));
    return upcoming.isEmpty ? null : upcoming.first;
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final scheduling = context.watch<SchedulingProvider>();
    final name = _firstName(auth.session?.name ?? '');
    final next = _nextAppointment(scheduling.myAppointments);
    final recent = scheduling.myAppointments.toList()
      ..sort((a, b) => b.scheduledStart.compareTo(a.scheduledStart));

    return Scaffold(
      appBar: AppBar(
        title: const Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            BrandLogoTile(size: 30),
            SizedBox(width: 10),
            Text('Beacon Pet Health'),
          ],
        ),
      ),
      body: RefreshIndicator(
        onRefresh: scheduling.loadMyAppointments,
        child: ListView(
          padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.pageHorizontal, vertical: AppSpacing.md),
          children: [
            Text(
              '$_greeting, $name',
              style: AppTextStyles.display,
            ),
            const SizedBox(height: 4),
            const Text(
              'How can we help your pet today?',
              style: AppTextStyles.bodyMuted,
            ),
            const SizedBox(height: 20),
            // Dark hero card with a yellow CTA — echoes the web dashboard
            // booking call-to-action on the Beacon black/yellow system.
            AppCard(
              color: AppColors.black,
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Need a vet visit?',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w800,
                      color: Colors.white,
                      letterSpacing: -0.2,
                    ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'Book a consultation at a clinic near you.',
                    style: TextStyle(fontSize: 12, color: AppColors.navInactive),
                  ),
                  const SizedBox(height: 14),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton.icon(
                      onPressed: () =>
                          Navigator.of(context).pushNamed('/book-consultation'),
                      icon: const Icon(Icons.add_circle_outline),
                      label: const Text('Book a Consultation'),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),
            const SectionHeader('Next Appointment'),
            const SizedBox(height: 8),
            if (scheduling.mineState == LoadState.loading)
              const Padding(
                padding: EdgeInsets.all(16),
                child: AppLoading(),
              )
            else if (next == null)
              const AppCard(
                child: Row(
                  children: [
                    Icon(Icons.event_busy, color: AppColors.neutral),
                    SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'No upcoming appointments',
                            style: TextStyle(
                              fontWeight: FontWeight.w700,
                              color: AppColors.black,
                            ),
                          ),
                          SizedBox(height: 2),
                          Text(
                            'Book a consultation to get started',
                            style: TextStyle(
                                fontSize: 12, color: AppColors.muted),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              )
            else
              _NextAppointmentCard(
                appointment: next,
                onTap: () => widget.onNavigateToTab(2),
              ),
            const SizedBox(height: 24),
            const SectionHeader('Quick Actions'),
            const SizedBox(height: 8),
            Row(
              children: [
                _QuickAction(
                  icon: Icons.pets,
                  label: 'My Pets',
                  onTap: () => widget.onNavigateToTab(1),
                ),
                const SizedBox(width: 8),
                _QuickAction(
                  icon: Icons.add_circle_outline,
                  label: 'Book Visit',
                  onTap: () => Navigator.of(context)
                      .pushNamed('/book-consultation'),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                _QuickAction(
                  icon: Icons.medical_information_outlined,
                  label: 'Medical History',
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(
                        builder: (_) => const MedicalHistoryPage()),
                  ),
                ),
                const SizedBox(width: 8),
                _QuickAction(
                  icon: Icons.receipt_long,
                  label: 'Bills',
                  onTap: () => widget.onNavigateToTab(3),
                ),
              ],
            ),
            if (recent.isNotEmpty) ...[
              const SizedBox(height: 24),
              const SectionHeader('Recent Activity'),
              const SizedBox(height: 8),
              for (var i = 0; i < recent.length && i < 3; i++)
                FadeSlideIn(
                  delayIndex: i,
                  child: _RecentActivityTile(appointment: recent[i]),
                ),
            ],
          ],
        ),
      ),
    );
  }
}

class _NextAppointmentCard extends StatelessWidget {
  final Appointment appointment;
  final VoidCallback onTap;

  const _NextAppointmentCard({required this.appointment, required this.onTap});

  String _date(String iso) => iso.length >= 10 ? iso.substring(0, 10) : iso;
  String _time(String iso) => iso.length >= 16 ? iso.substring(11, 16) : '';

  @override
  Widget build(BuildContext context) {
    return AppCard(
      color: AppColors.primarySoft,
      onTap: onTap,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.event_available, color: AppColors.black),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  appointment.petName ?? 'Your pet',
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    color: AppColors.black,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  '${_date(appointment.scheduledStart)}  ${_time(appointment.scheduledStart)}'
                  '${_time(appointment.scheduledEnd).isNotEmpty ? ' – ${_time(appointment.scheduledEnd)}' : ''}\n'
                  '${appointment.veterinarianName ?? 'Veterinarian'}'
                  '${appointment.type == 'FollowUp' ? ' · Follow-up' : ''}',
                  style:
                      const TextStyle(fontSize: 12, color: AppColors.muted),
                ),
              ],
            ),
          ),
          StatusBadge(appointment.status),
        ],
      ),
    );
  }
}

/// Compact row in the Recent Activity section: pet, date, status.
class _RecentActivityTile extends StatelessWidget {
  final Appointment appointment;

  const _RecentActivityTile({required this.appointment});

  String _date(String iso) => iso.length >= 10 ? iso.substring(0, 10) : iso;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      margin: const EdgeInsets.only(bottom: AppSpacing.xs),
      padding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.md, vertical: AppSpacing.sm),
      child: Row(
        children: [
          const Icon(Icons.history, size: 20, color: AppColors.black),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  appointment.petName ?? 'Your pet',
                  style: AppTextStyles.cardTitle,
                ),
                Text(
                  _date(appointment.scheduledStart),
                  style: AppTextStyles.caption,
                ),
              ],
            ),
          ),
          StatusBadge(appointment.status),
        ],
      ),
    );
  }
}

class _QuickAction extends StatelessWidget {
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  const _QuickAction({required this.icon, required this.label, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: AppCard(
        padding: const EdgeInsets.symmetric(vertical: 16),
        onTap: onTap,
        child: Column(
          children: [
            Container(
              width: 36,
              height: 36,
              decoration: BoxDecoration(
                color: AppColors.primarySoft,
                borderRadius: BorderRadius.circular(11),
              ),
              child: Icon(icon, color: AppColors.black, size: 20),
            ),
            const SizedBox(height: 6),
            Text(
              label,
              style: const TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w700,
                color: AppColors.black,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
