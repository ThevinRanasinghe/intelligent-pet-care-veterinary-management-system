import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:petcare_mobile/core/network/api_client.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/network/api_error.dart';
import 'package:petcare_mobile/features/scheduling/scheduling_service.dart';
import 'package:petcare_mobile/features/scheduling/scheduling_provider.dart';
import 'package:petcare_mobile/features/scheduling/appointment_slots_page.dart';
import '../helpers/fake_api_client.dart';

void main() {
  group('AppointmentSlotsPage widget tests', () {
    testWidgets('retry recovers from API error and empty list supports pull refresh', (tester) async {
      var calls = 0;
      final transport = MockClient((_) async {
        calls++;
        return calls == 1 ? http.Response('', 500) : http.Response('[]', 200);
      });
      addTearDown(transport.close);
      final provider = SchedulingProvider(SchedulingService(ApiClient(client: transport)));
      await tester.pumpWidget(MaterialApp(home: Scaffold(body: ChangeNotifierProvider.value(
        value: provider, child: const AppointmentSlotsPage(),
      ))));
      await tester.pumpAndSettle();
      expect(find.text('Retry'), findsOneWidget);
      await tester.tap(find.text('Retry'));
      await tester.pumpAndSettle();
      expect(calls, 2);
      expect(find.text('No available appointment slots'), findsOneWidget);
      await tester.drag(find.byType(ListView), const Offset(0, 400));
      await tester.pumpAndSettle();
      expect(calls, 3);
    });
    testWidgets('shows loading indicator initially, then slot data', (tester) async {
      final client = FakeApiClient();
      client.setResponse('available-slots', [
        {'id': 's1', 'veterinarianId': 'vet-1', 'date': '2026-01-10', 'startTime': '09:00:00', 'endTime': '09:30:00', 'branch': 'Colombo', 'status': 'Available'},
        {'id': 's2', 'veterinarianId': 'vet-2', 'date': '2026-01-11', 'startTime': '10:00:00', 'endTime': '10:30:00', 'branch': 'Galle', 'status': 'Available'},
      ]);
      final provider = SchedulingProvider(SchedulingService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const AppointmentSlotsPage(),
          ),
        ),
      );

      // Loading state
      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      // Allow async to complete
      await tester.pumpAndSettle();

      // Data rendered
      expect(find.text('Vet: vet-1'), findsOneWidget);
      expect(find.text('Vet: vet-2'), findsOneWidget);
      expect(find.byType(Card), findsNWidgets(2));
    });

    testWidgets('shows empty state when no slots returned', (tester) async {
      final client = FakeApiClient();
      client.setResponse('available-slots', []);
      final provider = SchedulingProvider(SchedulingService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const AppointmentSlotsPage(),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.text('No available appointment slots'), findsOneWidget);
    });

    testWidgets('shows error state on API failure', (tester) async {
      final client = FakeApiClient();
      client.setError('available-slots', ApiError(500, 'Server error', {'detail': 'Service unavailable'}));
      final provider = SchedulingProvider(SchedulingService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const AppointmentSlotsPage(),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.textContaining('Service unavailable'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });
  });
}
