import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'scheduling_provider.dart';

class AppointmentDetailPage extends StatefulWidget {
  final String appointmentId;
  final bool forSlot;

  const AppointmentDetailPage({super.key, required this.appointmentId, this.forSlot = false});

  @override
  State<AppointmentDetailPage> createState() => _AppointmentDetailPageState();
}

class _AppointmentDetailPageState extends State<AppointmentDetailPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<SchedulingProvider>().loadAppointment(widget.appointmentId, forSlot: widget.forSlot);
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
        return const Center(child: CircularProgressIndicator());
      case LoadState.error:
        return Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.error_outline, size: 48, color: Colors.red),
              const SizedBox(height: 8),
              Text(provider.errorMessage, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () => provider.loadAppointment(widget.appointmentId, forSlot: widget.forSlot),
                child: const Text('Retry'),
              ),
            ],
          ),
        );
      case LoadState.success:
        final appt = provider.appointment;
        if (appt == null) {
          return Center(child: Text(widget.forSlot
              ? 'No appointment has been booked for this slot'
              : 'Appointment not found'));
        }
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            _detailRow('Appointment ID', appt.id),
            _detailRow('Pet ID', appt.petId),
            _detailRow('Veterinarian ID', appt.veterinarianId),
            _detailRow('Slot ID', appt.appointmentSlotId),
            _detailRow('Start', appt.scheduledStart),
            _detailRow('End', appt.scheduledEnd),
            _detailRow('Status', appt.status),
            if (appt.notes != null && appt.notes!.isNotEmpty)
              _detailRow('Notes', appt.notes!),
            _detailRow('Created', appt.createdAt),
            _detailRow('Updated', appt.updatedAt),
          ],
        );
    }
  }

  Widget _detailRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 130,
            child: Text(label, style: const TextStyle(fontWeight: FontWeight.bold)),
          ),
          Expanded(child: Text(value)),
        ],
      ),
    );
  }
}
