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
import 'package:petcare_mobile/core/widgets/app_bottom_nav.dart';
import 'package:petcare_mobile/features/home/main_shell.dart';
import 'package:petcare_mobile/features/home/owner_home_tab.dart';
import 'package:petcare_mobile/features/pets/pets_page.dart';
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
    testWidgets(
        'unbooked slot shows an empty state without requesting a slot as an appointment',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('available-slots', [
        {
          'id': 's1',
          'veterinarianId': 'vet-1',
          'date': '2026-01-10',
          'startTime': '09:00:00',
          'endTime': '09:30:00',
          'branch': 'Colombo',
          'status': 'Available'
        },
      ]);
      client.setResponse('/appointments', []);
      final provider = SchedulingProvider(SchedulingService(client));
      await tester.pumpWidget(ChangeNotifierProvider.value(
        value: provider,
        child: const MaterialApp(
            onGenerateRoute: AppRouter.onGenerateRoute,
            home: AppointmentSlotsPage()),
      ));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Vet: vet-1'));
      await tester.pumpAndSettle();
      expect(find.text('No appointment has been booked for this slot'),
          findsOneWidget);
      expect(client.requestedPaths, contains('/appointments'));
      expect(client.requestedPaths, isNot(contains('/appointments/s1')));
    });
    testWidgets('tapping a slot navigates to AppointmentDetailPage',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('available-slots', [
        {
          'id': 's1',
          'veterinarianId': 'vet-1',
          'date': '2026-01-10',
          'startTime': '09:00:00',
          'endTime': '09:30:00',
          'branch': 'Colombo',
          'status': 'Available'
        },
      ]);
      // Also provide appointment detail response for the detail page
      client.setResponse('/appointments/', {
        'id': 'appt-1',
        'petId': 'pet-1',
        'veterinarianId': 'vet-1',
        'appointmentSlotId': 's1',
        'scheduledStart': '2026-01-10T09:00:00',
        'scheduledEnd': '2026-01-10T09:30:00',
        'status': 'Reserved',
        'notes': 'Check-up',
        'createdAt': '2026-01-01',
        'updatedAt': '2026-01-01',
      });

      client.setResponse('/appointments', [
        {
          'id': 'appt-1',
          'petId': 'pet-1',
          'veterinarianId': 'vet-1',
          'appointmentSlotId': 's1',
          'scheduledStart': '2026-01-10T09:00:00',
          'scheduledEnd': '2026-01-10T09:30:00',
          'status': 'Reserved',
          'notes': 'Check-up',
          'createdAt': '2026-01-01',
          'updatedAt': '2026-01-01',
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
          ChangeNotifierProvider.value(value: PetProvider(PetService(client))),
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

      final nav = tester.widget<AppBottomNav>(find.byType(AppBottomNav));
      expect(nav.items.map((e) => e.label).toList(),
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
      expect(
        client.requestedPaths.any((p) => p.startsWith('/pets')),
        isTrue,
      );
    });

    group('tab slide transitions', () {
      FakeApiClient seededClient() {
        final client = FakeApiClient();
        client.setResponse('/appointments/mine', <dynamic>[]);
        client.setResponse('/pets', <dynamic>[]);
        client.setResponse('/consultations', <dynamic>[]);
        client.setResponse('/quotations/mine', <dynamic>[]);
        client.setResponse('/petowners', <dynamic>[]);
        return client;
      }

      testWidgets('tabs are driven by a PageView with swipe disabled',
          (tester) async {
        final auth = await ownerAuth(seededClient());
        await tester.pumpWidget(shellApp(seededClient(), auth));
        await tester.pumpAndSettle();

        final pageView = tester.widget<PageView>(find.byType(PageView));
        expect(pageView.physics, isA<NeverScrollableScrollPhysics>());
      });

      testWidgets('Home → Pets: home slides left, pets enters from right',
          (tester) async {
        final client = seededClient();
        final auth = await ownerAuth(client);
        await tester.pumpWidget(shellApp(client, auth));
        await tester.pumpAndSettle();

        final width = tester.getSize(find.byType(MainShell)).width;
        final controller =
            tester.widget<PageView>(find.byType(PageView)).controller!;
        await tester.tap(find.text('My Pets').last);
        // First frame starts the ticker; second pump lands mid-slide.
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 140));

        // Page position between 0 and 1 → sliding toward the higher-index
        // tab: Home exits left while Pets enters from the right.
        expect(controller.page, greaterThan(0.0));
        expect(controller.page, lessThan(1.0));
        final homeCenter =
            tester.getCenter(find.byType(OwnerHomeTab, skipOffstage: false));
        final petsCenter =
            tester.getCenter(find.byType(PetsPage, skipOffstage: false));
        expect(homeCenter.dx, lessThan(width / 2)); // exiting left
        expect(petsCenter.dx, greaterThan(width / 2)); // entering from right

        await tester.pumpAndSettle();
        expect(controller.page, 1.0);
        expect(find.byType(PetsPage), findsOneWidget);
      });

      testWidgets('Pets → Home: pets slides right, home enters from left',
          (tester) async {
        final client = seededClient();
        final auth = await ownerAuth(client);
        await tester.pumpWidget(shellApp(client, auth));
        await tester.pumpAndSettle();

        await tester.tap(find.text('My Pets').last);
        await tester.pumpAndSettle();

        final width = tester.getSize(find.byType(MainShell)).width;
        final controller =
            tester.widget<PageView>(find.byType(PageView)).controller!;
        await tester.tap(find.text('Home').last);
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 140));

        // Page position between 1 and 0 → sliding back toward the
        // lower-index tab: Pets exits right, Home enters from the left.
        expect(controller.page, greaterThan(0.0));
        expect(controller.page, lessThan(1.0));
        final petsCenter =
            tester.getCenter(find.byType(PetsPage, skipOffstage: false));
        final homeCenter =
            tester.getCenter(find.byType(OwnerHomeTab, skipOffstage: false));
        expect(petsCenter.dx, greaterThan(width / 2)); // exiting right
        expect(homeCenter.dx, lessThan(width / 2)); // entering from left

        await tester.pumpAndSettle();
        expect(controller.page, 0.0);
        expect(find.byType(OwnerHomeTab), findsOneWidget);
      });

      testWidgets('bottom nav stays fixed during the slide', (tester) async {
        final client = seededClient();
        final auth = await ownerAuth(client);
        await tester.pumpWidget(shellApp(client, auth));
        await tester.pumpAndSettle();

        final navPosBefore = tester.getTopLeft(find.byType(AppBottomNav));
        await tester.tap(find.text('My Pets').last);
        await tester.pump(const Duration(milliseconds: 100));
        final navPosDuring = tester.getTopLeft(find.byType(AppBottomNav));

        expect(navPosDuring, navPosBefore);
        await tester.pumpAndSettle();
        expect(find.byType(AppBottomNav), findsOneWidget);
      });

      testWidgets('visited tabs keep their state when switching away',
          (tester) async {
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

        await tester.tap(find.text('Home').last);
        await tester.pumpAndSettle();

        // Pets tab stays mounted (keep-alive) even though off-screen.
        expect(find.byType(PetsPage, skipOffstage: false), findsOneWidget);
        expect(find.text('Shadow', skipOffstage: false), findsOneWidget);
      });
    });
  });
}
