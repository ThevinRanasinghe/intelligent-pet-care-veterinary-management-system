import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/status_badge.dart';
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
        return const AppLoading();
      case LoadState.error:
        return AppErrorState(
          message: provider.errorMessage,
          onRetry: provider.loadSlots,
        );
      case LoadState.success:
        if (provider.slots.isEmpty) {
          return ListView(
            children: const [
              AppEmptyState(
                message: 'No available appointment slots',
                icon: Icons.event_busy,
                padding: EdgeInsets.only(top: 200),
              ),
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
    return AppCard(
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      padding: EdgeInsets.zero,
      onTap: () {
        Navigator.of(context).pushNamed('/slot-appointment', arguments: slot.id);
      },
      child: ListTile(
        leading: Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color: AppColors.primarySoft,
            borderRadius: BorderRadius.circular(11),
          ),
          child: const Icon(Icons.calendar_today,
              color: AppColors.black, size: 20),
        ),
        title: Text('Vet: ${slot.veterinarianId}'),
        subtitle: Text('${slot.date}  ${slot.startTime}–${slot.endTime}\nBranch: ${slot.branch}'),
        isThreeLine: true,
        trailing: StatusBadge(slot.status),
      ),
    );
  }
}
