import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/auth/auth_service.dart';
import 'package:petcare_mobile/core/auth/token_storage.dart';
import 'package:petcare_mobile/core/network/api_error.dart';
import 'package:petcare_mobile/features/auth/auth_provider.dart';
import 'package:petcare_mobile/features/billing/billing_service.dart';
import 'package:petcare_mobile/features/billing/billing_provider.dart';
import 'package:petcare_mobile/features/billing/quotations_page.dart';
import '../helpers/fake_api_client.dart';

Map<String, String> sessionValues(String role) => {
      'petcare.token': 'test-token',
      'petcare.expiresAt':
          DateTime.now().add(const Duration(hours: 1)).toIso8601String(),
      'petcare.userId': 'user-1',
      'petcare.email': 'test@example.test',
      'petcare.name': 'Test User',
      'petcare.role': role,
    };

Widget appFor(FakeApiClient client, AuthProvider auth) {
  return MaterialApp(
    home: MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: auth),
        ChangeNotifierProvider.value(
            value: BillingProvider(BillingService(client))),
      ],
      child: const QuotationsPage(),
    ),
  );
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  AuthProvider staffAuth(FakeApiClient client) =>
      AuthProvider(AuthService(client, TokenStorage()));

  group('QuotationsPage widget tests', () {
    testWidgets('shows loading then renders quotation tiles', (tester) async {
      FlutterSecureStorage.setMockInitialValues(sessionValues('ClinicManager'));
      final client = FakeApiClient();
      client.setResponse('/quotations', [
        {
          'id': 'q1',
          'appointmentId': 'a1',
          'budget': 10000.0,
          'subtotal': 4000.0,
          'total': 4500.0,
          'isWithinBudget': true,
          'status': 'PendingApproval',
          'items': [
            {
              'id': 'i1',
              'category': 'Consultation',
              'description': 'Check-up',
              'quantity': 1,
              'unitPrice': 2500.0,
              'totalPrice': 2500.0
            },
          ],
          'createdAt': '2026-01-01',
          'updatedAt': '2026-01-01',
        },
      ]);
      final auth = staffAuth(client);
      await auth.init();

      await tester.pumpWidget(appFor(client, auth));

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      await tester.pumpAndSettle();

      expect(find.textContaining('Quotation'), findsOneWidget);
      expect(find.textContaining('LKR 4500.00'), findsOneWidget);
      expect(find.text('PendingApproval'), findsOneWidget);
      // Staff role → org-scoped list, not the owner endpoint.
      expect(client.requestedPaths, contains('/quotations'));
      expect(client.requestedPaths, isNot(contains('/quotations/mine')));
    });

    testWidgets('shows empty state when no quotations', (tester) async {
      FlutterSecureStorage.setMockInitialValues(sessionValues('ClinicManager'));
      final client = FakeApiClient();
      client.setResponse('/quotations', []);

      await tester.pumpWidget(appFor(client, staffAuth(client)));

      await tester.pumpAndSettle();
      expect(find.text('No quotations found'), findsOneWidget);
    });

    testWidgets('shows error state on API failure', (tester) async {
      FlutterSecureStorage.setMockInitialValues(sessionValues('ClinicManager'));
      final client = FakeApiClient();
      client.setError('/quotations',
          ApiError(500, 'Server error', {'detail': 'Billing service down'}));

      await tester.pumpWidget(appFor(client, staffAuth(client)));

      await tester.pumpAndSettle();
      expect(find.textContaining('Billing service down'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('PetOwner sees My Bills loaded from /quotations/mine',
        (tester) async {
      FlutterSecureStorage.setMockInitialValues(sessionValues('PetOwner'));
      final client = FakeApiClient();
      client.setResponse('/quotations/mine', [
        {
          'id': 'q1',
          'invoiceNumber': 'INV-AAAA0001',
          'appointmentId': 'a1',
          'budget': 2800.0,
          'subtotal': 2800.0,
          'total': 2800.0,
          'isWithinBudget': true,
          'status': 'Finalised',
          'paymentStatus': 'Paid',
          'petName': 'Shadow',
          'ownerName': 'Amal',
          'veterinarianName': 'Dr. Silva',
          'veterinarianChargeTotal': 2500.0,
          'medicineTotal': 300.0,
          'items': [
            {
              'id': 'i1',
              'category': 'Examination',
              'description': 'Vet charge',
              'quantity': 1,
              'unitPrice': 2500.0,
              'totalPrice': 2500.0
            },
          ],
          'createdAt': '2026-01-01',
          'updatedAt': '2026-01-01',
        },
      ]);
      final auth = AuthProvider(AuthService(client, TokenStorage()));
      await auth.init();

      await tester.pumpWidget(appFor(client, auth));
      await tester.pumpAndSettle();

      expect(find.text('My Bills'), findsOneWidget);
      expect(find.text('INV-AAAA0001'), findsOneWidget);
      expect(find.text('Paid'), findsOneWidget);
      expect(client.requestedPaths, contains('/quotations/mine'));
      expect(client.requestedPaths, isNot(contains('/quotations')));
    });
  });
}
