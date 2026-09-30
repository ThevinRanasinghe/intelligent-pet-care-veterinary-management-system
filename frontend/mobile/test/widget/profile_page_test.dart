import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/auth/auth_service.dart';
import 'package:petcare_mobile/core/auth/token_storage.dart';
import 'package:petcare_mobile/core/network/api_client.dart';
import 'package:petcare_mobile/features/auth/auth_provider.dart';
import 'package:petcare_mobile/features/profile/profile_page.dart';
import '../helpers/fake_api_client.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('ProfilePage (owner)', () {
    Future<AuthProvider> ownerAuth(FakeApiClient client) async {
      FlutterSecureStorage.setMockInitialValues({
        'petcare.token': 'test-token',
        'petcare.expiresAt':
            DateTime.now().add(const Duration(hours: 1)).toIso8601String(),
        'petcare.userId': 'user-1',
        'petcare.email': 'amal@example.test',
        'petcare.name': 'Amal Perera',
        'petcare.role': 'PetOwner',
      });
      final auth = AuthProvider(AuthService(client, TokenStorage()));
      await auth.init();
      return auth;
    }

    Widget appFor(FakeApiClient client, AuthProvider auth) {
      return MultiProvider(
        providers: [
          Provider<ApiClient>.value(value: client),
          ChangeNotifierProvider.value(value: auth),
        ],
        child: const MaterialApp(home: ProfilePage()),
      );
    }

    testWidgets('shows name/email/phone and owner actions', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/petowners', [
        {
          'id': 'own-1',
          'fullName': 'Amal Perera',
          'email': 'amal@example.test',
          'phoneNumber': '0771234567',
          'address': '12 Palm Grove',
        },
      ]);
      final auth = await ownerAuth(client);

      await tester.pumpWidget(appFor(client, auth));
      await tester.pumpAndSettle();

      expect(find.text('Amal Perera'), findsOneWidget);
      expect(find.text('amal@example.test'), findsOneWidget);
      expect(find.text('0771234567'), findsOneWidget);
      expect(find.text('12 Palm Grove'), findsOneWidget);
      expect(find.text('Edit Profile'), findsOneWidget);
      expect(find.text('Medical History'), findsOneWidget);
      expect(find.text('Change Password'), findsOneWidget);
      await tester.scrollUntilVisible(find.text('Logout'), 200,
          scrollable: find.byType(Scrollable).last);
      expect(find.text('Logout'), findsOneWidget);
      expect(client.requestedPaths, contains('/petowners'));
    });

    testWidgets('logout clears the session (returns to login at app level)',
        (tester) async {
      final client = FakeApiClient();
      client.setResponse('/petowners', <Map<String, dynamic>>[]);
      final auth = await ownerAuth(client);

      await tester.pumpWidget(appFor(client, auth));
      await tester.pumpAndSettle();
      expect(auth.isAuthenticated, isTrue);

      await tester.scrollUntilVisible(find.text('Logout'), 200,
          scrollable: find.byType(Scrollable).last);
      await tester.tap(find.text('Logout'));
      await tester.pumpAndSettle();
      expect(auth.isAuthenticated, isFalse);
      expect(auth.session, isNull);
    });
  });
}
