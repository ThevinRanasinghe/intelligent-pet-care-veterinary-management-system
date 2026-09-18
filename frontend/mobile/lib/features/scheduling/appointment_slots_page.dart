import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'scheduling_provider.dart';
import 'models/appointment_slot.dart';

class AppointmentSlotsPage extends StatefulWidget {
  const AppointmentSlotsPage({super.key});

  @override
  State<AppointmentSlotsPage> createState() => _AppointmentSlotsPageState();
}

class _AppointmentSlotsPageState extends State<AppointmentSlotsPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<SchedulingProvider>().loadSlots();
    });
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<SchedulingProvider>();

    return RefreshIndicator(
      onRefresh: provider.loadSlots,
      child: _buildBody(provider),
    );
  }

  Widget _buildBody(SchedulingProvider provider) {
    switch (provider.slotsState) {
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
              FilledButton(onPressed: provider.loadSlots, child: const Text('Retry')),
            ],
          ),
        );
      case LoadState.success:
        if (provider.slots.isEmpty) {
          return ListView(
            children: const [
              SizedBox(height: 200),
              Center(child: Text('No available appointment slots')),
            ],
          );
        }
        return ListView.builder(
          itemCount: provider.slots.length,
          itemBuilder: (context, index) {
            final slot = provider.slots[index];
            return AppointmentSlotTile(slot: slot);
          },
        );
    }
  }
}

class AppointmentSlotTile extends StatelessWidget {
  final AppointmentSlot slot;

  const AppointmentSlotTile({super.key, required this.slot});

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      child: ListTile(
        leading: const CircleAvatar(child: Icon(Icons.calendar_today)),
        title: Text('Vet: ${slot.veterinarianId}'),
        subtitle: Text('${slot.date}  ${slot.startTime}–${slot.endTime}\nBranch: ${slot.branch}'),
        isThreeLine: true,
        trailing: Chip(label: Text(slot.status)),
        onTap: () {
          Navigator.of(context).pushNamed('/slot-appointment', arguments: slot.id);
        },
      ),
    );
  }
}
