// SE3110 TC-FL-006: Login form validation — tapping Sign In with empty
// fields must show validator text and make ZERO API calls.
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/auth/auth_service.dart';
import 'package:petcare_mobile/core/auth/token_storage.dart';
import 'package:petcare_mobile/core/network/api_client.dart';
import 'package:petcare_mobile/features/auth/auth_provider.dart';
import 'package:petcare_mobile/features/auth/login_page.dart';
import '../helpers/fake_api_client.dart';

Widget appFor(FakeApiClient client, AuthProvider auth) {
  return MultiProvider(
    providers: [
      Provider<ApiClient>.value(value: client),
      ChangeNotifierProvider.value(value: auth),
    ],
    child: const MaterialApp(home: LoginPage()),
  );
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('empty submit shows required messages and calls no endpoint',
      (tester) async {
    final client = FakeApiClient();
    final auth = AuthProvider(AuthService(client, TokenStorage()));
    addTearDown(auth.dispose);

    await tester.pumpWidget(appFor(client, auth));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Sign In'));
    await tester.pumpAndSettle();

    expect(find.text('Email is required'), findsOneWidget);
    expect(find.text('Password is required'), findsOneWidget);
    expect(client.requestedPaths, isEmpty);
    expect(auth.isAuthenticated, isFalse);
  });

  testWidgets('missing password still blocks the API call', (tester) async {
    final client = FakeApiClient();
    final auth = AuthProvider(AuthService(client, TokenStorage()));
    addTearDown(auth.dispose);

    await tester.pumpWidget(appFor(client, auth));
    await tester.pumpAndSettle();

    // AppTextField renders its label outside the field, so locate the
    // email TextFormField positionally (email is first in the column).
    await tester.enterText(
        find.byType(TextFormField).first, 'amal@example.test');
    await tester.tap(find.text('Sign In'));
    await tester.pumpAndSettle();

    expect(find.text('Email is required'), findsNothing);
    expect(find.text('Password is required'), findsOneWidget);
    expect(client.requestedPaths, isEmpty);
    expect(auth.isAuthenticated, isFalse);
  });
}
