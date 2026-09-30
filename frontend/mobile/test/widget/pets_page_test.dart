import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/auth/auth_service.dart';
import 'package:petcare_mobile/core/auth/token_storage.dart';
import 'package:petcare_mobile/core/network/api_client.dart';
import 'package:petcare_mobile/features/auth/auth_provider.dart';
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
    };

Widget appFor(FakeApiClient client, AuthProvider auth) {
  return MultiProvider(
    providers: [
      Provider<ApiClient>.value(value: client),
      ChangeNotifierProvider.value(value: auth),
      ChangeNotifierProvider.value(
          value: PetProvider(PetService(client))),
    ],
    // Providers sit above MaterialApp (as in main.dart) so pushed routes
    // like PetFormPage/PetDetailPage can read them.
    child: const MaterialApp(home: PetsPage()),
  );
}

Future<AuthProvider> ownerAuth(FakeApiClient client) async {
  final auth =
      AuthProvider(AuthService(client, TokenStorage()));
  await auth.init();
  return auth;
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() => FlutterSecureStorage.setMockInitialValues(ownerSession()));

  group('PetsPage (owner) widget tests', () {
    testWidgets('lists the owner\'s pets with species/breed, age and View Profile',
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
      expect(client.requestedPaths, contains('/pets'));
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
        {'id': 'own-1', 'fullName': 'Amal Perera', 'email': 'amal@example.test'},
      ]);
      client.setResponse('POST /pets',
          petJson(id: 'p2', name: 'Rex'));
      final auth = await ownerAuth(client);

      await tester.pumpWidget(appFor(client, auth));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(FloatingActionButton, 'Add Pet'));
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

      await tester.enterText(
          find.byType(TextFormField).first, 'Rex');
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
}
