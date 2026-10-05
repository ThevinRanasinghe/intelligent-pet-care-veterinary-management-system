import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/network/api_client.dart';
import 'package:petcare_mobile/features/consultations/consultation_provider.dart';
import 'package:petcare_mobile/features/consultations/consultation_service.dart';
import 'package:petcare_mobile/features/scheduling/owner_appointments_page.dart';
import 'package:petcare_mobile/features/scheduling/scheduling_provider.dart';
import 'package:petcare_mobile/features/scheduling/scheduling_service.dart';
import '../helpers/fake_api_client.dart';

Map<String, dynamic> appointmentJson({
  required String id,
  required String petName,
  String? type,
  String status = 'Confirmed',
}) =>
    {
      'id': id,
      'petId': 'pet-1',
      'veterinarianId': 'vet-1',
      'appointmentSlotId': 'slot-1',
      'scheduledStart': '2026-10-01T10:00:00',
      'scheduledEnd': '2026-10-01T11:00:00',
      'status': status,
      'notes': null,
      'type': type,
      'petName': petName,
      'ownerName': 'Amal',
      'veterinarianName': 'Dr. Silva',
      'symptoms': 'Limping',
      'consultationRequestId': 'CON-1',
      'examinationId': null,
      'createdAt': '2026-09-26T00:00:00',
      'updatedAt': '2026-09-26T00:00:00',
    };

Map<String, dynamic> consultationJson({
  String id = 'CON-1',
  String petName = 'Shadow',
  String status = 'Submitted',
  String requestType = 'Initial',
  String? agentWorkflowStatus,
}) =>
    {
      'id': id,
      'petId': 'pet-1',
      'ownerId': 'own-1',
      'petName': petName,
      'symptoms': 'Limping',
      'urgency': 'Medium',
      'preferredDate': '2026-10-02',
      'preferredTime': '10:00:00',
      'organizationId': 'org-1',
      'organizationName': 'CityVets Colombo',
      'status': status,
      'agentWorkflowStatus': agentWorkflowStatus,
      'requestType': requestType,
      'createdAt': '2026-09-26T00:00:00',
      'updatedAt': '2026-09-26T00:00:00',
    };

Widget appFor(FakeApiClient client) {
  return MultiProvider(
    providers: [
      Provider<ApiClient>.value(value: client),
      ChangeNotifierProvider.value(
          value: SchedulingProvider(SchedulingService(client))),
      ChangeNotifierProvider.value(
          value: ConsultationProvider(ConsultationService(client))),
    ],
    child: const MaterialApp(home: OwnerAppointmentsPage()),
  );
}

void main() {
  group('OwnerAppointmentsPage', () {
    testWidgets(
        'Pending shows consultation requests; Upcoming shows confirmed/follow-up appointments; History shows completed',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/consultations', [
        consultationJson(status: 'Submitted'),
        consultationJson(id: 'CON-2', petName: 'Milo', status: 'Cancelled'),
      ]);
      client.setResponse('/appointments/mine', [
        appointmentJson(id: 'a1', petName: 'Shadow', status: 'Confirmed'),
        appointmentJson(
            id: 'a2', petName: 'Luna', status: 'Confirmed', type: 'FollowUp'),
        appointmentJson(id: 'a3', petName: 'Rex', status: 'Completed'),
      ]);

      await tester.pumpWidget(appFor(client));
      await tester.pumpAndSettle();

      // Default segment: Upcoming.
      expect(find.text('Pending (1)'), findsOneWidget);
      expect(find.text('Upcoming (2)'), findsOneWidget);
      expect(find.text('Shadow'), findsOneWidget);
      expect(find.text('Luna'), findsOneWidget);
      expect(find.textContaining('Follow-up'), findsOneWidget);
      expect(find.text('Confirmed'), findsNWidgets(2));

      // Pending segment: the Submitted request.
      await tester.tap(find.text('Pending (1)'));
      await tester.pumpAndSettle();
      expect(find.text('Submitted'), findsOneWidget);
      expect(find.textContaining('CityVets Colombo'), findsOneWidget);
      expect(find.text('Rex'), findsNothing);

      // History segment: completed appointment + cancelled request.
      await tester.tap(find.text('History'));
      await tester.pumpAndSettle();
      expect(find.text('Rex'), findsOneWidget);
      expect(find.text('Completed'), findsOneWidget);
      expect(find.text('Cancelled'), findsOneWidget);
    });

    testWidgets(
        'shows the AI workflow chip with a friendly label and nothing when null',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/consultations', [
        consultationJson(
            id: 'CON-1',
            petName: 'Shadow',
            agentWorkflowStatus: 'PendingManagerApproval'),
        consultationJson(id: 'CON-2', petName: 'Milo'),
      ]);
      client.setResponse('/appointments/mine', <Map<String, dynamic>>[]);

      await tester.pumpWidget(appFor(client));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Pending (2)'));
      await tester.pumpAndSettle();

      expect(find.text('Clinic reviewing'), findsOneWidget);
      // Milo has no workflow — no chip, only the request status badge.
      expect(find.text('Planning'), findsNothing);
    });

    testWidgets('empty upcoming state is friendly', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/consultations', <Map<String, dynamic>>[]);
      client.setResponse('/appointments/mine', <Map<String, dynamic>>[]);

      await tester.pumpWidget(appFor(client));
      await tester.pumpAndSettle();

      expect(find.text('No upcoming appointments'), findsOneWidget);
    });
  });
}
