import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/network/api_client.dart';
import 'package:petcare_mobile/core/auth/auth_service.dart';
import 'package:petcare_mobile/core/auth/token_storage.dart';
import 'package:petcare_mobile/features/auth/auth_provider.dart';
import 'package:petcare_mobile/features/approval/approval_provider.dart';
import 'package:petcare_mobile/features/approval/approval_service.dart';
import 'package:petcare_mobile/features/billing/billing_provider.dart';
import 'package:petcare_mobile/features/billing/billing_service.dart';
import 'package:petcare_mobile/features/consultations/consultation_provider.dart';
import 'package:petcare_mobile/features/consultations/consultation_service.dart';
import 'package:petcare_mobile/features/history/history_provider.dart';
import 'package:petcare_mobile/features/history/history_service.dart';
import 'package:petcare_mobile/features/home/main_shell.dart';
import 'package:petcare_mobile/features/pets/pet_provider.dart';
import 'package:petcare_mobile/features/pets/pet_service.dart';
import 'package:petcare_mobile/features/scheduling/scheduling_service.dart';
import 'package:petcare_mobile/features/scheduling/scheduling_provider.dart';
import 'package:petcare_mobile/features/scheduling/appointment_slots_page.dart';
import 'package:petcare_mobile/core/routing/app_router.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
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

  group('MainShell bottom navigation', () {
    Future<AuthProvider> ownerAuth(FakeApiClient client) async {
      FlutterSecureStorage.setMockInitialValues({
        'petcare.token': 'test-token',
        'petcare.expiresAt':
            DateTime.now().add(const Duration(hours: 1)).toIso8601String(),
        'petcare.userId': 'u1',
        'petcare.email': 'amal@example.test',
        'petcare.name': 'Amal Perera',
        'petcare.role': 'PetOwner',
      });
      final auth = AuthProvider(AuthService(client, TokenStorage()));
      await auth.init();
      return auth;
    }

    Widget shellApp(FakeApiClient client, AuthProvider auth) {
      return MultiProvider(
        providers: [
          Provider<ApiClient>.value(value: client),
          ChangeNotifierProvider.value(value: auth),
          ChangeNotifierProvider.value(
              value: SchedulingProvider(SchedulingService(client))),
          ChangeNotifierProvider.value(
              value: BillingProvider(BillingService(client))),
          ChangeNotifierProvider.value(
              value: ApprovalProvider(ApprovalService(client))),
          ChangeNotifierProvider.value(
              value: PetProvider(PetService(client))),
          ChangeNotifierProvider.value(
              value: ConsultationProvider(ConsultationService(client))),
          ChangeNotifierProvider.value(
              value: HistoryProvider(HistoryService(client))),
        ],
        child: const MaterialApp(home: MainShell()),
      );
    }

    testWidgets(
        'tabs are Home, My Pets, Appointments, Bills, Profile — in order',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/appointments/mine', <dynamic>[]);
      client.setResponse('/pets', <dynamic>[]);
      client.setResponse('/consultations', <dynamic>[]);
      client.setResponse('/quotations/mine', <dynamic>[]);
      client.setResponse('/petowners', <dynamic>[]);
      final auth = await ownerAuth(client);

      await tester.pumpWidget(shellApp(client, auth));
      await tester.pumpAndSettle();

      final destinations = tester
          .widget<NavigationBar>(find.byType(NavigationBar))
          .destinations
          .map((d) => (d as NavigationDestination).label)
          .toList();
      expect(destinations,
          ['Home', 'My Pets', 'Appointments', 'Bills', 'Profile']);
    });

    testWidgets('tapping My Pets switches to the pets tab', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/appointments/mine', <dynamic>[]);
      client.setResponse('/pets', [
        {
          'id': 'p1',
          'ownerId': 'own-1',
          'name': 'Shadow',
          'species': 'Dog',
          'dateOfBirth': '2020-05-10',
        }
      ]);
      client.setResponse('/consultations', <dynamic>[]);
      client.setResponse('/quotations/mine', <dynamic>[]);
      client.setResponse('/petowners', <dynamic>[]);
      final auth = await ownerAuth(client);

      await tester.pumpWidget(shellApp(client, auth));
      await tester.pumpAndSettle();

      await tester.tap(find.text('My Pets').last);
      await tester.pumpAndSettle();

      expect(find.text('Shadow'), findsOneWidget);
      expect(client.requestedPaths, contains('/pets'));
    });
  });
}
