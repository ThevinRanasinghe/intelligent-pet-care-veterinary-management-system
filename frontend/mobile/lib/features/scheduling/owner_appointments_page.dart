import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/motion/app_motion.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/list_icon_tile.dart';
import '../../core/widgets/segmented_control.dart';
import '../../core/widgets/status_badge.dart';
import '../../core/widgets/top_bar.dart';
import '../consultations/consultation_detail_page.dart';
import '../consultations/consultation_provider.dart';
import '../consultations/models/consultation_request.dart';
import 'models/appointment_slot.dart';
import 'owner_appointment_detail_page.dart';
import 'scheduling_provider.dart';

/// Owner Appointments tab: merges the owner's consultation requests
/// (GET /consultations) with their pets' appointments
/// (GET /appointments/mine) under a Pending | Upcoming | History filter.
class OwnerAppointmentsPage extends StatefulWidget {
  const OwnerAppointmentsPage({super.key});

  @override
  State<OwnerAppointmentsPage> createState() => _OwnerAppointmentsPageState();
}

class _OwnerAppointmentsPageState extends State<OwnerAppointmentsPage> {
  int _segment = 1; // default: Upcoming

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _refresh());
  }

  Future<void> _refresh() async {
    await Future.wait([
      context.read<SchedulingProvider>().loadMyAppointments(),
      context.read<ConsultationProvider>().loadMyConsultations(),
    ]);
  }

  @override
  Widget build(BuildContext context) {
    final scheduling = context.watch<SchedulingProvider>();
    final consultations = context.watch<ConsultationProvider>();

    final pending =
        consultations.myConsultations.where((c) => c.isPending).toList();
    final upcoming = scheduling.myAppointments
        .where((a) =>
            const {'Reserved', 'Confirmed', 'Scheduled'}.contains(a.status))
        .toList()
      ..sort((a, b) => a.scheduledStart.compareTo(b.scheduledStart));
    final historyAppointments = scheduling.myAppointments
        .where((a) => const {'Completed', 'Cancelled'}.contains(a.status))
        .toList()
      ..sort((a, b) => b.scheduledStart.compareTo(a.scheduledStart));
    final historyConsultations = consultations.myConsultations
        .where((c) => const {'Cancelled', 'Rejected'}.contains(c.status))
        .toList();

    final loading = scheduling.mineState == LoadState.loading ||
        consultations.listState == LoadState.loading;

    return Scaffold(
      appBar: TopBar(
        title: 'Appointments',
        showBackButton: false,
        actions: [
          IconButton(
            icon: const Icon(Icons.add, size: 20),
            tooltip: 'Book a Consultation',
            onPressed: () =>
                Navigator.of(context).pushNamed('/book-consultation'),
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(AppSpacing.pageHorizontal,
                AppSpacing.sm, AppSpacing.pageHorizontal, AppSpacing.xs),
            child: AppSegmentedControl(
              labels: [
                'Pending (${pending.length})',
                'Upcoming (${upcoming.length})',
                'History',
              ],
              selectedIndex: _segment,
              onSelected: (i) => setState(() => _segment = i),
            ),
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _refresh,
              // Segment/loading swaps crossfade instead of flashing.
              child: AnimatedSwitcher(
                duration: AppMotion.standard,
                child: loading
                    ? const AppLoading()
                    : KeyedSubtree(
                        key: ValueKey(_segment),
                        child: _buildList(pending, upcoming,
                            historyAppointments, historyConsultations),
                      ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildList(
    List<ConsultationRequest> pending,
    List<Appointment> upcoming,
    List<Appointment> historyAppointments,
    List<ConsultationRequest> historyConsultations,
  ) {
    final children = <Widget>[];
    var stagger = 0;
    Widget staggered(Widget child) => FadeSlideIn(
          delay: Duration(milliseconds: 50 * (stagger++).clamp(0, 6)),
          distance: 8,
          child: child,
        );

    if (_segment == 0) {
      if (pending.isEmpty) {
        children.add(const AppEmptyState(
          message: 'No pending consultation requests',
          icon: Icons.assignment_outlined,
        ));
      }
      for (final c in pending) {
        children.add(staggered(_ConsultationTile(request: c)));
      }
    } else if (_segment == 1) {
      if (upcoming.isEmpty) {
        children.add(const AppEmptyState(
          message: 'No upcoming appointments',
          icon: Icons.event_available,
        ));
      }
      for (final a in upcoming) {
        children.add(staggered(_OwnerAppointmentTile(appointment: a)));
      }
    } else {
      if (historyAppointments.isEmpty && historyConsultations.isEmpty) {
        children.add(const AppEmptyState(
          message: 'No past appointments',
          icon: Icons.history,
        ));
      }
      for (final a in historyAppointments) {
        children.add(staggered(_OwnerAppointmentTile(appointment: a)));
      }
      for (final c in historyConsultations) {
        children.add(staggered(_ConsultationTile(request: c)));
      }
    }

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.only(bottom: 24),
      children: children,
    );
  }
}

class _OwnerAppointmentTile extends StatelessWidget {
  final Appointment appointment;

  const _OwnerAppointmentTile({required this.appointment});

  String _date(String iso) => iso.length >= 10 ? iso.substring(0, 10) : iso;
  String _time(String iso) => iso.length >= 16 ? iso.substring(11, 16) : '';

  @override
  Widget build(BuildContext context) {
    return AppCard(
      margin: const EdgeInsets.symmetric(
          horizontal: AppSpacing.pageHorizontal, vertical: AppSpacing.xxs + 2),
      padding: EdgeInsets.zero,
      onTap: () {
        Navigator.of(context).push(
          MotionPageRoute(
            page: OwnerAppointmentDetailPage(appointment: appointment),
          ),
        );
      },
      child: ListTile(
        leading: const ListIconTile(icon: Icons.pets),
        title: Text(appointment.petName ?? appointment.petId),
        subtitle: Text(
          '${_date(appointment.scheduledStart)}  ${_time(appointment.scheduledStart)}'
          '${_time(appointment.scheduledEnd).isNotEmpty ? ' – ${_time(appointment.scheduledEnd)}' : ''}\n'
          '${appointment.veterinarianName ?? 'Veterinarian'}'
          '${appointment.type == 'FollowUp' ? '  ·  Follow-up' : ''}',
        ),
        isThreeLine: true,
        trailing: StatusBadge(appointment.status),
      ),
    );
  }
}

class _ConsultationTile extends StatelessWidget {
  final ConsultationRequest request;

  const _ConsultationTile({required this.request});

  @override
  Widget build(BuildContext context) {
    return AppCard(
      margin: const EdgeInsets.symmetric(
          horizontal: AppSpacing.pageHorizontal, vertical: AppSpacing.xxs + 2),
      padding: EdgeInsets.zero,
      onTap: () {
        Navigator.of(context).push(
          MotionPageRoute(
            page: OwnerConsultationDetailPage(request: request),
          ),
        );
      },
      child: ListTile(
        leading: const ListIconTile(icon: Icons.assignment_outlined),
        title: Text(request.petName ?? request.petId),
        subtitle: Text(
          '${request.preferredDateShort ?? 'Date TBC'}'
          '${request.preferredTimeShort != null ? '  ${request.preferredTimeShort}' : ''}\n'
          '${request.organizationName ?? 'Clinic'}'
          '${request.requestType == 'FollowUp' ? '  ·  Follow-up request' : ''}',
        ),
        isThreeLine: true,
        trailing: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            StatusBadge(request.status),
            if (request.agentWorkflowStatusLabel != null) ...[
              const SizedBox(height: 4),
              StatusBadge(request.agentWorkflowStatusLabel!),
            ],
          ],
        ),
      ),
    );
  }
}
