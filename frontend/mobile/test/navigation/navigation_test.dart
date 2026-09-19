import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/features/scheduling/scheduling_service.dart';
import 'package:petcare_mobile/features/scheduling/scheduling_provider.dart';
import 'package:petcare_mobile/features/scheduling/appointment_slots_page.dart';
import 'package:petcare_mobile/core/routing/app_router.dart';
import '../helpers/fake_api_client.dart';

void main() {
  group('Navigation: appointment slots list -> detail', () {
    testWidgets('unbooked slot shows an empty state without requesting a slot as an appointment', (tester) async {
      final client = FakeApiClient();
      client.setResponse('available-slots', [
        {'id': 's1', 'veterinarianId': 'vet-1', 'date': '2026-01-10', 'startTime': '09:00:00', 'endTime': '09:30:00', 'branch': 'Colombo', 'status': 'Available'},
      ]);
      client.setResponse('/appointments', []);
      final provider = SchedulingProvider(SchedulingService(client));
      await tester.pumpWidget(ChangeNotifierProvider.value(
        value: provider,
        child: const MaterialApp(onGenerateRoute: AppRouter.onGenerateRoute, home: AppointmentSlotsPage()),
      ));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Vet: vet-1'));
      await tester.pumpAndSettle();
      expect(find.text('No appointment has been booked for this slot'), findsOneWidget);
      expect(client.requestedPaths, contains('/appointments'));
      expect(client.requestedPaths, isNot(contains('/appointments/s1')));
    });
    testWidgets('tapping a slot navigates to AppointmentDetailPage', (tester) async {
      final client = FakeApiClient();
      client.setResponse('available-slots', [
        {'id': 's1', 'veterinarianId': 'vet-1', 'date': '2026-01-10', 'startTime': '09:00:00', 'endTime': '09:30:00', 'branch': 'Colombo', 'status': 'Available'},
      ]);
      // Also provide appointment detail response for the detail page
      client.setResponse('/appointments/', {
        'id': 'appt-1', 'petId': 'pet-1', 'veterinarianId': 'vet-1', 'appointmentSlotId': 's1',
        'scheduledStart': '2026-01-10T09:00:00', 'scheduledEnd': '2026-01-10T09:30:00',
        'status': 'Reserved', 'notes': 'Check-up', 'createdAt': '2026-01-01', 'updatedAt': '2026-01-01',
      });

      client.setResponse('/appointments', [
        {
          'id': 'appt-1', 'petId': 'pet-1', 'veterinarianId': 'vet-1', 'appointmentSlotId': 's1',
          'scheduledStart': '2026-01-10T09:00:00', 'scheduledEnd': '2026-01-10T09:30:00',
          'status': 'Reserved', 'notes': 'Check-up', 'createdAt': '2026-01-01', 'updatedAt': '2026-01-01',
        },
      ]);
      final schedulingProvider = SchedulingProvider(SchedulingService(client));

      await tester.pumpWidget(
        ChangeNotifierProvider.value(
          value: schedulingProvider,
          child: const MaterialApp(
            onGenerateRoute: AppRouter.onGenerateRoute,
            home: AppointmentSlotsPage(),
          ),
        ),
      );

      await tester.pumpAndSettle();

      // Tap the slot tile
      await tester.tap(find.text('Vet: vet-1'));
      await tester.pumpAndSettle();

      // Should be on the detail page
      expect(find.text('Appointment Details'), findsOneWidget);
      expect(find.text('Reserved'), findsOneWidget);
      expect(find.text('Check-up'), findsOneWidget);
      expect(client.requestedPaths, contains('/appointments/appt-1'));
      expect(client.requestedPaths, isNot(contains('/appointments/s1')));
    });
  });
}
