import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/detail_row.dart';
import '../../core/widgets/status_badge.dart';
import 'scheduling_provider.dart';

class AppointmentDetailPage extends StatefulWidget {
  final String appointmentId;
  final bool forSlot;

  const AppointmentDetailPage(
      {super.key, required this.appointmentId, this.forSlot = false});

  @override
  State<AppointmentDetailPage> createState() => _AppointmentDetailPageState();
}

class _AppointmentDetailPageState extends State<AppointmentDetailPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context
          .read<SchedulingProvider>()
          .loadAppointment(widget.appointmentId, forSlot: widget.forSlot);
    });
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<SchedulingProvider>();

    return Scaffold(
      appBar: AppBar(title: const Text('Appointment Details')),
      body: _buildBody(provider),
    );
  }

  Widget _buildBody(SchedulingProvider provider) {
    switch (provider.appointmentState) {
      case LoadState.idle:
      case LoadState.loading:
        return const AppLoading();
      case LoadState.error:
        return AppErrorState(
          message: provider.errorMessage,
          onRetry: () => provider.loadAppointment(widget.appointmentId,
              forSlot: widget.forSlot),
        );
      case LoadState.success:
        final appt = provider.appointment;
        if (appt == null) {
          return Center(
              child: Text(widget.forSlot
                  ? 'No appointment has been booked for this slot'
                  : 'Appointment not found'));
        }
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  DetailRow('Appointment ID', value: appt.id),
                  DetailRow('Pet', value: appt.petName ?? appt.petId),
                  if (appt.ownerName != null)
                    DetailRow('Owner', value: appt.ownerName!),
                  DetailRow('Veterinarian',
                      value: appt.veterinarianName ?? appt.veterinarianId),
                  if (appt.type == 'FollowUp')
                    const DetailRow('Type', value: 'Follow-up'),
                  DetailRow('Slot ID', value: appt.appointmentSlotId),
                  DetailRow('Start', value: appt.scheduledStart),
                  DetailRow('End', value: appt.scheduledEnd),
                  DetailRow('Duration', value: appt.durationLabel),
                  DetailRow('Status', child: StatusBadge(appt.status)),
                  if (appt.symptoms != null && appt.symptoms!.isNotEmpty)
                    DetailRow('Symptoms', value: appt.symptoms!),
                  if (appt.notes != null && appt.notes!.isNotEmpty)
                    DetailRow('Notes', value: appt.notes!),
                  DetailRow('Created', value: appt.createdAt),
                  DetailRow('Updated', value: appt.updatedAt),
                ],
              ),
            ),
          ],
        );
    }
  }
}
