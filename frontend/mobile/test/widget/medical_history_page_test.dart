import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/network/api_client.dart';
import 'package:petcare_mobile/core/network/api_error.dart';
import 'package:petcare_mobile/features/history/history_provider.dart';
import 'package:petcare_mobile/features/history/history_service.dart';
import 'package:petcare_mobile/features/history/medical_history_page.dart';
import 'package:petcare_mobile/features/pets/models/pet.dart';
import 'package:petcare_mobile/features/pets/pet_provider.dart';
import 'package:petcare_mobile/features/pets/pet_service.dart';
import '../helpers/fake_api_client.dart';

Pet pet({String id = 'p1', String name = 'Shadow'}) => Pet(
      id: id,
      ownerId: 'own-1',
      name: name,
      species: 'Dog',
      breed: 'Mixed',
      dateOfBirth: '2020-05-10',
    );

Map<String, dynamic> examJson() => {
      'id': 'exam-1',
      'petId': 'p1',
      'veterinarianId': 'vet-1',
      'symptoms': 'Limping after walks',
      'notes': 'Mild swelling',
      'examinationDate': '2026-01-10T09:00:00',
      'createdAt': '2026-01-10T09:00:00',
    };

void stubHistory(FakeApiClient client, {bool withDiagnosis = true}) {
  client.setResponse('/examinations/pet/p1', [examJson()]);
  client.setResponse(
      '/diagnoses/examination/exam-1',
      withDiagnosis
          ? [
              {
                'id': 'diag-1',
                'examinationId': 'exam-1',
                'conditionName': 'Soft tissue strain',
                'description': 'Rest advised',
                'severity': 'Moderate',
                'createdAt': '2026-01-10T09:30:00',
              }
            ]
          : <dynamic>[]);
  client.setResponse('/treatmentrecords/diagnosis/diag-1', [
    {
      'id': 'tr-1',
      'diagnosisId': 'diag-1',
      'procedureName': 'Cold compress',
      'notes': 'Twice daily',
      'status': 'Completed',
      'createdAt': '2026-01-10T09:35:00',
      'updatedAt': '2026-01-10T09:35:00',
    }
  ]);
  client.setResponse('/prescriptions/treatment/tr-1', [
    {
      'id': 'rx-1',
      'treatmentRecordId': 'tr-1',
      'medicineId': 'med-1',
      'medicineName': 'Meloxicam',
      'medicineStrength': '1.5mg/ml',
      'dosage': '0.2mg/kg',
      'durationDays': 5,
      'quantity': 1,
      'requestStatus': 'Pending',
      'createdAt': '2026-01-10T09:40:00',
    }
  ]);
}

Widget historyApp(FakeApiClient client, {Pet? forPet}) {
  return MultiProvider(
    providers: [
      Provider<ApiClient>.value(value: client),
      ChangeNotifierProvider.value(value: PetProvider(PetService(client))),
      ChangeNotifierProvider.value(
          value: HistoryProvider(HistoryService(client))),
    ],
    child: MaterialApp(home: MedicalHistoryPage(pet: forPet)),
  );
}

void main() {
  group('MedicalHistoryPage', () {
    testWidgets('renders the examination timeline with diagnosis, '
        'treatment and prescription', (tester) async {
      final client = FakeApiClient();
      stubHistory(client);

      await tester.pumpWidget(historyApp(client, forPet: pet()));
      await tester.pumpAndSettle();

      expect(find.text('Medical History'), findsOneWidget);
      expect(find.textContaining('Examination'), findsOneWidget);
      expect(find.text('Limping after walks'), findsOneWidget);
      expect(find.text('Diagnosis: Soft tissue strain'), findsOneWidget);
      expect(find.text('Treatment: Cold compress'), findsOneWidget);
      expect(find.textContaining('Meloxicam'), findsOneWidget);
      expect(client.requestedPaths, contains('/examinations/pet/p1'));
      expect(client.requestedPaths,
          contains('/diagnoses/examination/exam-1'));
      expect(client.requestedPaths,
          contains('/treatmentrecords/diagnosis/diag-1'));
      expect(client.requestedPaths,
          contains('/prescriptions/treatment/tr-1'));
    });

    testWidgets('empty history shows the empty state', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/examinations/pet/p1', <dynamic>[]);

      await tester.pumpWidget(historyApp(client, forPet: pet()));
      await tester.pumpAndSettle();

      expect(find.text('No medical records yet'), findsOneWidget);
    });

    testWidgets('API failure shows a friendly error with retry',
        (tester) async {
      final client = FakeApiClient();
      client.setError(
          '/examinations/pet/p1', ApiError(500, 'Server error'));

      await tester.pumpWidget(historyApp(client, forPet: pet()));
      await tester.pumpAndSettle();

      expect(
          find.textContaining('Unable to load medical history'),
          findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('no overflow on a small (320x568) screen', (tester) async {
      tester.view.physicalSize = const Size(320, 568);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);
      final client = FakeApiClient();
      stubHistory(client);

      await tester.pumpWidget(historyApp(client, forPet: pet()));
      await tester.pumpAndSettle();

      // A RenderFlex overflow would have thrown during pumpAndSettle.
      expect(find.text('Medical History'), findsOneWidget);
      expect(find.text('Diagnosis: Soft tissue strain'), findsOneWidget);
    });

    testWidgets('without a pet the selector row lists pets and selection '
        'loads history', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/pets', [
        {
          'id': 'p1',
          'ownerId': 'own-1',
          'name': 'Shadow',
          'species': 'Dog',
          'breed': 'Mixed',
          'dateOfBirth': '2020-05-10',
        }
      ]);
      stubHistory(client);

      await tester.pumpWidget(historyApp(client));
      await tester.pumpAndSettle();

      expect(find.text('Select a pet to see its medical history'),
          findsOneWidget);
      await tester.tap(find.text('Shadow'));
      await tester.pumpAndSettle();

      expect(client.requestedPaths, contains('/examinations/pet/p1'));
      expect(find.text('Diagnosis: Soft tissue strain'), findsOneWidget);
    });
  });
}
