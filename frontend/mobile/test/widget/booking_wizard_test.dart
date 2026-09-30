import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/network/api_client.dart';
import 'package:petcare_mobile/core/network/api_error.dart';
import 'package:petcare_mobile/features/consultations/booking_wizard_page.dart';
import 'package:petcare_mobile/features/consultations/clinic_picker.dart';
import 'package:petcare_mobile/features/consultations/consultation_provider.dart';
import 'package:petcare_mobile/features/consultations/consultation_service.dart';
import 'package:petcare_mobile/features/consultations/models/clinic.dart';
import 'package:petcare_mobile/features/pets/pet_provider.dart';
import 'package:petcare_mobile/features/pets/pet_service.dart';
import '../helpers/fake_api_client.dart';

Map<String, dynamic> petJson() => {
      'id': 'pet-1',
      'ownerId': 'own-1',
      'name': 'Shadow',
      'species': 'Dog',
      'breed': 'Mixed',
      'gender': 'Male',
      'dateOfBirth': '2020-05-10',
      'weight': 4.5,
    };

List<Map<String, dynamic>> orgsJson() => [
      {
        'id': 'org-1',
        'name': 'CityVets Colombo',
        'city': 'Colombo',
        'address': '1 Galle Rd',
        'latitude': 6.9271,
        'longitude': 79.8612,
      },
      // No stored coordinates — skipped on the map, still listed.
      {
        'id': 'org-2',
        'name': 'Rural Clinic',
        'city': 'Kandy',
        'address': null,
        'latitude': null,
        'longitude': null,
      },
    ];

/// Month-availability for the current month: every day available except
/// day 10 (fully booked) and day 1 (past).
List<Map<String, dynamic>> monthJson() {
  final now = DateTime.now();
  final days = DateUtils.getDaysInMonth(now.year, now.month);
  return [
    for (var d = 1; d <= days; d++)
      {
        'date':
            '${now.year}-${now.month.toString().padLeft(2, '0')}-${d.toString().padLeft(2, '0')}',
        'available': d != 10 && d != 1,
        'fullyBooked': d == 10,
        'isPast': d == 1,
      },
  ];
}

Map<String, dynamic> dayJson(String date) => {
      'date': date,
      'isPast': false,
      // The nine fixed one-hour slots 09:00–17:00; 11:00 is booked.
      'slots': [
        for (var h = 9; h <= 17; h++)
          {
            'start': '${h.toString().padLeft(2, '0')}:00',
            'end': '${(h + 1).toString().padLeft(2, '0')}:00',
            'available': h != 11,
            'availableVeterinarianIds': h != 11 ? ['vet-1'] : <String>[],
          },
      ],
    };

Map<String, dynamic> consultationJson(String status) => {
      'id': 'CON-1',
      'petId': 'pet-1',
      'ownerId': 'own-1',
      'petName': 'Shadow',
      'symptoms': 'Limping',
      'urgency': 'Medium',
      'preferredDate': '2026-01-02',
      'preferredTime': '10:00:00',
      'organizationId': 'org-1',
      'organizationName': 'CityVets Colombo',
      'status': status,
      'requestType': 'Initial',
      'createdAt': '2026-01-01T00:00:00',
      'updatedAt': '2026-01-01T00:00:00',
    };

Widget wizardApp(FakeApiClient client, {bool useMap = false}) {
  return MultiProvider(
    providers: [
      Provider<ApiClient>.value(value: client),
      ChangeNotifierProvider.value(value: PetProvider(PetService(client))),
      ChangeNotifierProvider.value(
          value: ConsultationProvider(ConsultationService(client))),
    ],
    child: MaterialApp(home: BookingWizardPage(useMap: useMap)),
  );
}

void main() {
  group('BookingWizardPage', () {
    testWidgets(
        'full flow: pet → clinic → date → slot → submit creates+submits the request',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/pets', [petJson()]);
      client.setResponse('/lookups/organizations', orgsJson());
      client.setResponse('/consultations/availability/month', monthJson());
      client.setResponse(
          '/consultations/availability?', dayJson('ignored'));
      client.setResponse('POST /consultations', consultationJson('Draft'));
      client.setResponse(
          '/consultations/CON-1/submit', consultationJson('Submitted'));

      await tester.pumpWidget(wizardApp(client));
      await tester.pumpAndSettle();

      // Step 1 — pet guard: Next disabled until a pet is picked.
      expect(find.textContaining('Step 1 of 5'), findsOneWidget);
      var next = tester.widget<FilledButton>(
          find.widgetWithText(FilledButton, 'Next'));
      expect(next.onPressed, isNull);
      await tester.tap(find.text('Shadow'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();

      // Step 2 — clinic list (map is disabled in tests; the list is the
      // always-working path). Both clinics render; the null-coords one is
      // still selectable from the list.
      expect(find.textContaining('Step 2 of 5'), findsOneWidget);
      expect(find.text('CityVets Colombo'), findsOneWidget);
      expect(find.text('Rural Clinic'), findsOneWidget);
      await tester.tap(find.text('CityVets Colombo'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();

      // Step 3 — month grid: fully-booked day shows the label and is
      // disabled; a free day selects + loads day availability.
      expect(find.textContaining('Step 3 of 5'), findsOneWidget);
      expect(find.text('Fully booked'), findsOneWidget);
      final now = DateTime.now();
      final bookedKey = Key(
          'day-${now.year}-${now.month.toString().padLeft(2, '0')}-10');
      await tester.tap(find.byKey(bookedKey));
      await tester.pumpAndSettle();
      next = tester.widget<FilledButton>(
          find.widgetWithText(FilledButton, 'Next'));
      expect(next.onPressed, isNull); // fully-booked day not selectable

      final freeKey = Key(
          'day-${now.year}-${now.month.toString().padLeft(2, '0')}-02');
      await tester.tap(find.byKey(freeKey));
      await tester.pumpAndSettle();
      final pickedDate =
          '${now.year}-${now.month.toString().padLeft(2, '0')}-02';
      expect(
          client.requestedPaths,
          contains(
              '/consultations/availability?organizationId=org-1&date=$pickedDate'));

      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();

      // Step 4 — nine slot chips; the booked 11:00 slot is disabled.
      expect(find.textContaining('Step 4 of 5'), findsOneWidget);
      expect(find.byType(ChoiceChip), findsNWidgets(9));
      final bookedChip = tester.widget<ChoiceChip>(
          find.widgetWithText(ChoiceChip, '11:00 – 12:00'));
      expect(bookedChip.onSelected, isNull);
      await tester.tap(find.text('10:00 – 11:00'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();

      // Step 5 — summary + required symptoms gate the submit.
      expect(find.textContaining('Step 5 of 5'), findsOneWidget);
      expect(find.text('Booking Summary'), findsOneWidget);
      var submit = tester.widget<FilledButton>(
          find.widgetWithText(FilledButton, 'Submit Request'));
      expect(submit.onPressed, isNull);
      await tester.enterText(find.byType(TextField).first, 'Limping');
      await tester.pumpAndSettle();
      await tester.tap(find.text('Submit Request'));
      await tester.pumpAndSettle();

      // POST body matches the API contract, then /submit is called.
      final body = client.bodyFor('POST', '/consultations');
      expect(body, isNotNull);
      expect(body!['petId'], 'pet-1');
      expect(body['organizationId'], 'org-1');
      expect(body['preferredDate'], pickedDate);
      expect(body['preferredTime'], '10:00:00');
      expect(body['symptoms'], 'Limping');
      expect(client.requestedPaths, contains('/consultations/CON-1/submit'));

      // Success screen.
      expect(find.text('Consultation Requested'), findsOneWidget);
      expect(find.text('Submitted'), findsOneWidget);
    });

    testWidgets('409 on submit shows the friendly slot-unavailable message',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/pets', [petJson()]);
      client.setResponse('/lookups/organizations', orgsJson());
      client.setResponse('/consultations/availability/month', monthJson());
      client.setResponse('/consultations/availability?', dayJson('x'));
      client.setError('POST /consultations', ApiError(409, 'Conflict'));

      await tester.pumpWidget(wizardApp(client));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Shadow'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('CityVets Colombo'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      final now = DateTime.now();
      await tester.tap(find.byKey(Key(
          'day-${now.year}-${now.month.toString().padLeft(2, '0')}-02')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('10:00 – 11:00'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField).first, 'Limping');
      await tester.pumpAndSettle();
      await tester.tap(find.text('Submit Request'));
      await tester.pumpAndSettle();

      expect(
        find.text(
            'This appointment slot is no longer available. Please select another time.'),
        findsOneWidget,
      );
    });

    testWidgets('clinic load failure shows friendly message + retry',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/pets', [petJson()]);
      client.setError('/lookups/organizations', ApiError(500, 'Server error'));

      await tester.pumpWidget(wizardApp(client));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Shadow'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();

      expect(find.text('Unable to load clinics. Please try again.'),
          findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('empty clinic list shows the no-clinics message',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/pets', [petJson()]);
      client.setResponse('/lookups/organizations', <Map<String, dynamic>>[]);

      await tester.pumpWidget(wizardApp(client));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Shadow'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();

      expect(
          find.text(
              'No PetCare clinics are currently available in this area.'),
          findsOneWidget);
    });

    testWidgets('a day with no free slots shows the no-slots message',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/pets', [petJson()]);
      client.setResponse('/lookups/organizations', orgsJson());
      client.setResponse('/consultations/availability/month', monthJson());
      client.setResponse(
          '/consultations/availability?', {'date': 'x', 'isPast': false, 'slots': <dynamic>[]});

      await tester.pumpWidget(wizardApp(client));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Shadow'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('CityVets Colombo'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      final now = DateTime.now();
      await tester.tap(find.byKey(Key(
          'day-${now.year}-${now.month.toString().padLeft(2, '0')}-02')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();

      expect(
          find.text('No appointment slots are available for this date.'),
          findsOneWidget);
    });
  });

  group('ClinicPicker helpers', () {
    test('directions URL uses the Google Maps dir scheme with lat,lng', () {
      final uri = clinicDirectionsUri(Clinic(
        id: 'org-1',
        name: 'CityVets',
        latitude: 6.9271,
        longitude: 79.8612,
      ));
      expect(
        uri.toString(),
        'https://www.google.com/maps/dir/?api=1&destination=6.9271,79.8612',
      );
    });
  });
}
