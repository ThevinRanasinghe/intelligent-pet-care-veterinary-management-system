import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/routing/app_router.dart';
import 'package:petcare_mobile/features/scheduling/my_appointments_page.dart';
import 'package:petcare_mobile/features/scheduling/scheduling_service.dart';
import 'package:petcare_mobile/features/scheduling/scheduling_provider.dart';
import '../helpers/fake_api_client.dart';

Map<String, dynamic> appointmentJson({
  required String id,
  required String petName,
  String? type,
  String status = 'Confirmed',
  String? veterinarianName,
}) =>
    {
      'id': id,
      'petId': 'pet-1',
      'veterinarianId': 'vet-1',
      'appointmentSlotId': 'slot-1',
      'scheduledStart': '2026-10-01T10:00:00',
      'scheduledEnd': '2026-10-01T10:30:00',
      'status': status,
      'notes': null,
      'type': type,
      'petName': petName,
      'ownerName': 'Amal',
      'veterinarianName': veterinarianName,
      'symptoms': 'Limping',
      'consultationRequestId': 'CON-0001',
      'examinationId': null,
      'createdAt': '2026-09-26T00:00:00',
      'updatedAt': '2026-09-26T00:00:00',
    };

void main() {
  group('MyAppointmentsPage widget tests', () {
    testWidgets(
        'renders rows from /appointments/mine with Follow-up label and status chips',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/appointments/mine', [
        appointmentJson(
            id: 'a1',
            petName: 'Shadow',
            type: 'FollowUp',
            status: 'Confirmed',
            veterinarianName: 'Dr. Silva'),
        appointmentJson(
            id: 'a2',
            petName: 'Luna',
            type: 'Initial',
            status: 'Completed',
            veterinarianName: 'Dr. Silva'),
      ]);
      final provider = SchedulingProvider(SchedulingService(client));

      await tester.pumpWidget(
        ChangeNotifierProvider.value(
          value: provider,
          child: const MaterialApp(
            onGenerateRoute: AppRouter.onGenerateRoute,
            home: MyAppointmentsPage(),
          ),
        ),
      );

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      await tester.pumpAndSettle();

      expect(find.text('My Appointments'), findsOneWidget);
      expect(find.text('Shadow'), findsOneWidget);
      expect(find.text('Luna'), findsOneWidget);
      expect(find.textContaining('Follow-up'), findsOneWidget);
      expect(find.text('Confirmed'), findsOneWidget);
      expect(find.text('Completed'), findsOneWidget);
      expect(find.textContaining('Dr. Silva'), findsWidgets);
      expect(client.requestedPaths, contains('/appointments/mine'));
    });

    testWidgets('tapping a row opens the existing appointment detail',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/appointments/mine', [
        appointmentJson(
            id: 'a1', petName: 'Shadow', type: 'Initial', status: 'Confirmed'),
      ]);
      client.setResponse(
          '/appointments/a1', appointmentJson(id: 'a1', petName: 'Shadow'));
      final provider = SchedulingProvider(SchedulingService(client));

      await tester.pumpWidget(
        ChangeNotifierProvider.value(
          value: provider,
          child: const MaterialApp(
            onGenerateRoute: AppRouter.onGenerateRoute,
            home: MyAppointmentsPage(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Shadow'));
      await tester.pumpAndSettle();

      expect(find.text('Appointment Details'), findsOneWidget);
      expect(client.requestedPaths, contains('/appointments/a1'));
    });

    testWidgets('shows empty state when there are no appointments',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/appointments/mine', []);
      final provider = SchedulingProvider(SchedulingService(client));

      await tester.pumpWidget(
        ChangeNotifierProvider.value(
          value: provider,
          child: const MaterialApp(home: MyAppointmentsPage()),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('No appointments found'), findsOneWidget);
    });
  });
}
