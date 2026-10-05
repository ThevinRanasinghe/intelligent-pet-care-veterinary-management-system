import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/auth/auth_service.dart';
import 'package:petcare_mobile/core/auth/token_storage.dart';
import 'package:petcare_mobile/core/network/api_client.dart';
import 'package:petcare_mobile/features/auth/auth_provider.dart';
import 'package:petcare_mobile/features/pets/models/pet.dart';
import 'package:petcare_mobile/features/pets/pet_detail_page.dart';
import 'package:petcare_mobile/features/pets/pet_provider.dart';
import 'package:petcare_mobile/features/pets/pet_service.dart';
import 'package:petcare_mobile/features/pets/pets_page.dart';
import '../helpers/fake_api_client.dart';

Map<String, String> ownerSession() => {
      'petcare.token': 'test-token',
      'petcare.expiresAt':
          DateTime.now().add(const Duration(hours: 1)).toIso8601String(),
      'petcare.userId': 'user-1',
      'petcare.email': 'amal@example.test',
      'petcare.name': 'Amal Perera',
      'petcare.role': 'PetOwner',
    };

Map<String, dynamic> petJson({
  String id = 'p1',
  String ownerId = 'own-1',
  String name = 'Shadow',
  String species = 'Dog',
  String? breed = 'Mixed',
  String? dateOfBirth = '2020-05-10',
  bool isArchived = false,
}) =>
    {
      'id': id,
      'ownerId': ownerId,
      'name': name,
      'species': species,
      'breed': breed,
      'gender': 'Male',
      'dateOfBirth': dateOfBirth,
      'weight': 4.5,
      'photoUrl': null,
      'notes': null,
      'isArchived': isArchived,
    };

Widget appFor(FakeApiClient client, AuthProvider auth) {
  return MultiProvider(
    providers: [
      Provider<ApiClient>.value(value: client),
      ChangeNotifierProvider.value(value: auth),
      ChangeNotifierProvider.value(value: PetProvider(PetService(client))),
    ],
    // Providers sit above MaterialApp (as in main.dart) so pushed routes
    // and the add-pet bottom sheet can read them.
    child: const MaterialApp(home: PetsPage()),
  );
}

Future<AuthProvider> ownerAuth(FakeApiClient client) async {
  final auth = AuthProvider(AuthService(client, TokenStorage()));
  await auth.init();
  return auth;
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() => FlutterSecureStorage.setMockInitialValues(ownerSession()));

  group('PetsPage (owner) widget tests', () {
    testWidgets(
        'lists the owner\'s pets with species/breed, age and View Profile',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/pets', [
        petJson(name: 'Shadow'),
        petJson(id: 'p2', name: 'Luna', species: 'Cat', breed: 'Siamese'),
      ]);
      final auth = await ownerAuth(client);

      await tester.pumpWidget(appFor(client, auth));
      await tester.pumpAndSettle();

      expect(find.text('My Pets'), findsOneWidget);
      expect(find.text('Shadow'), findsOneWidget);
      expect(find.text('Luna'), findsOneWidget);
      expect(find.textContaining('Dog · Mixed'), findsOneWidget);
      expect(find.textContaining('years old'), findsWidgets);
      expect(find.text('View Profile'), findsNWidgets(2));
      // The list is fetched with archived pets included so the
      // Active/Archived tabs can both be populated.
      expect(
        client.requestedPaths.any((p) => p.startsWith('/pets?')),
        isTrue,
      );
    });

    testWidgets('empty list shows the add-pet hint', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/pets', []);
      await tester.pumpWidget(appFor(client, await ownerAuth(client)));
      await tester.pumpAndSettle();
      expect(find.textContaining('No pets registered yet'), findsOneWidget);
    });

    testWidgets('add-pet form blocks submit without a name, then POSTs /pets',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/pets', [petJson()]);
      client.setResponse('/petowners', [
        {
          'id': 'own-1',
          'fullName': 'Amal Perera',
          'email': 'amal@example.test'
        },
      ]);
      client.setResponse('POST /pets', petJson(id: 'p2', name: 'Rex'));
      final auth = await ownerAuth(client);

      await tester.pumpWidget(appFor(client, auth));
      await tester.pumpAndSettle();

      // Dark circular + FAB opens the add-pet bottom sheet.
      await tester.tap(find.byType(FloatingActionButton));
      await tester.pumpAndSettle();
      expect(find.text('Add Pet'), findsWidgets);

      final scrollable = find.byType(SingleChildScrollView);
      final submitButton = find.widgetWithText(FilledButton, 'Add Pet');

      // Submit with no name → validation blocks the request.
      await tester.dragUntilVisible(
          submitButton, scrollable, const Offset(0, -80));
      await tester.tap(submitButton);
      await tester.pumpAndSettle();
      expect(find.text('Name is required'), findsOneWidget);
      // '/pets' was already GET-ed by the initial list load — the guard
      // is that nothing was POSTed.
      expect(client.bodyFor('POST', '/pets'), isNull);

      await tester.enterText(find.byType(TextFormField).first, 'Rex');
      await tester.dragUntilVisible(
          submitButton, scrollable, const Offset(0, -80));
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      final body = client.bodyFor('POST', '/pets');
      expect(body, isNotNull);
      expect(body!['name'], 'Rex');
      expect(body['species'], 'Dog');
      expect(body['ownerId'], 'own-1');
    });
  });

  group('Pet archive lifecycle', () {
    testWidgets('active tab hides archived pets; Archived tab shows them',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/pets', [
        petJson(name: 'Shadow'),
        petJson(id: 'p2', name: 'Milo', species: 'Cat', isArchived: true),
      ]);
      final auth = await ownerAuth(client);

      await tester.pumpWidget(appFor(client, auth));
      await tester.pumpAndSettle();

      // Active tab: only Shadow; the segmented control offers both views.
      expect(find.text('Shadow'), findsOneWidget);
      expect(find.text('Milo'), findsNothing);
      expect(find.text('Active'), findsOneWidget);
      expect(find.text('Archived'), findsOneWidget);

      await tester.tap(find.text('Archived'));
      await tester.pumpAndSettle();

      expect(find.text('Milo'), findsOneWidget);
      expect(find.text('Shadow'), findsNothing);
    });

    testWidgets(
        'pet detail Remove archives the pet via POST /pets/{id}/archive',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/pets', [petJson()]);
      client.setResponse('POST /pets/p1/archive',
          petJson(isArchived: true));
      final auth = await ownerAuth(client);
      final pet = Pet.fromJson(petJson());

      await tester.pumpWidget(MultiProvider(
        providers: [
          Provider<ApiClient>.value(value: client),
          ChangeNotifierProvider.value(value: auth),
          ChangeNotifierProvider.value(value: PetProvider(PetService(client))),
        ],
        child: MaterialApp(home: PetDetailPage(pet: pet)),
      ));
      await tester.pumpAndSettle();

      // The owner-facing action is Remove — never Delete. The action row
      // sits at the bottom of the scrolling profile, so scroll to it.
      final removeBtn = find.widgetWithText(ElevatedButton, 'Remove');
      await tester.scrollUntilVisible(removeBtn, 200);
      expect(find.widgetWithText(ElevatedButton, 'Delete'), findsNothing);
      await tester.tap(removeBtn);
      await tester.pumpAndSettle();

      // Confirmation explains the history is preserved.
      expect(find.text('Remove Pet?'), findsOneWidget);
      expect(find.textContaining('Medical history will be preserved'),
          findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Remove Pet'));
      await tester.pumpAndSettle();

      expect(client.requestedPaths, contains('/pets/p1/archive'));
      // Nothing was permanently deleted.
      expect(
        client.requestedPaths.where((p) => p == '/pets/p1'),
        isEmpty,
      );
    });

    testWidgets('archived pet detail shows Restore and POSTs /restore',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/pets', [
        petJson(isArchived: true),
      ]);
      client.setResponse('POST /pets/p1/restore', petJson());
      final auth = await ownerAuth(client);
      final pet = Pet.fromJson(petJson(isArchived: true));

      await tester.pumpWidget(MultiProvider(
        providers: [
          Provider<ApiClient>.value(value: client),
          ChangeNotifierProvider.value(value: auth),
          ChangeNotifierProvider.value(value: PetProvider(PetService(client))),
        ],
        child: MaterialApp(home: PetDetailPage(pet: pet)),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Archived'), findsOneWidget);
      final restoreBtn =
          find.widgetWithText(ElevatedButton, 'Restore Pet');
      await tester.scrollUntilVisible(restoreBtn, 200);
      expect(find.widgetWithText(ElevatedButton, 'Remove'), findsNothing);

      await tester.tap(restoreBtn);
      await tester.pumpAndSettle();

      expect(client.requestedPaths, contains('/pets/p1/restore'));
    });
  });
}
