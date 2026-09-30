import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/status_badge.dart';
import 'scheduling_provider.dart';
import 'models/appointment_slot.dart';

/// Role-aware appointment list backed by GET /appointments/mine —
/// Veterinarian sees their own appointments, PetOwner their pets'.
class MyAppointmentsPage extends StatefulWidget {
  /// When embedded inside HomePage's tab body the page drops its own
  /// Scaffold/AppBar (HomePage already provides them).
  final bool embedded;

  const MyAppointmentsPage({super.key, this.embedded = false});

  @override
  State<MyAppointmentsPage> createState() => _MyAppointmentsPageState();
}

class _MyAppointmentsPageState extends State<MyAppointmentsPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<SchedulingProvider>().loadMyAppointments();
    });
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<SchedulingProvider>();
    final body = RefreshIndicator(
      onRefresh: provider.loadMyAppointments,
      child: _buildBody(provider),
    );
    if (widget.embedded) return body;
    return Scaffold(
      appBar: AppBar(title: const Text('My Appointments')),
      body: body,
    );
  }

  Widget _buildBody(SchedulingProvider provider) {
    switch (provider.mineState) {
      case LoadState.idle:
      case LoadState.loading:
        return const AppLoading();
      case LoadState.error:
        return AppErrorState(
          message: provider.errorMessage,
          onRetry: provider.loadMyAppointments,
        );
      case LoadState.success:
        if (provider.myAppointments.isEmpty) {
          return ListView(
            children: const [
              AppEmptyState(
                message: 'No appointments found',
                icon: Icons.event_busy,
                padding: EdgeInsets.only(top: 200),
              ),
            ],
          );
        }
        return ListView.builder(
          itemCount: provider.myAppointments.length,
          itemBuilder: (context, index) =>
              _AppointmentTile(appointment: provider.myAppointments[index]),
        );
    }
  }
}

class _AppointmentTile extends StatelessWidget {
  final Appointment appointment;

  const _AppointmentTile({required this.appointment});

  String _date(String iso) => iso.length >= 10 ? iso.substring(0, 10) : iso;
  String _time(String iso) => iso.length >= 16 ? iso.substring(11, 16) : '';

  @override
  Widget build(BuildContext context) {
    return AppCard(
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      padding: EdgeInsets.zero,
      onTap: () {
        Navigator.of(context).pushNamed('/appointment', arguments: appointment.id);
      },
      child: ListTile(
        leading: Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color: AppColors.primarySoft,
            borderRadius: BorderRadius.circular(11),
          ),
          child: const Icon(Icons.pets, color: AppColors.black, size: 20),
        ),
        title: Text(appointment.petName ?? appointment.petId),
        subtitle: Text(
          '${_date(appointment.scheduledStart)}  ${_time(appointment.scheduledStart)}'
          '${_time(appointment.scheduledEnd).isNotEmpty ? ' – ${_time(appointment.scheduledEnd)}' : ''}\n'
          '${appointment.veterinarianName ?? 'Veterinarian'}'
          '${appointment.ownerName != null ? '  ·  ${appointment.ownerName}' : ''}'
          '${appointment.type == 'FollowUp' ? '  ·  Follow-up' : ''}',
        ),
        isThreeLine: true,
        trailing: StatusBadge(appointment.status),
      ),
    );
  }
}
