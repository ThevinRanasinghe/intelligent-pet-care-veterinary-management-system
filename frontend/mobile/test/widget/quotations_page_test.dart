import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/network/api_error.dart';
import 'package:petcare_mobile/features/billing/billing_service.dart';
import 'package:petcare_mobile/features/billing/billing_provider.dart';
import 'package:petcare_mobile/features/billing/quotations_page.dart';
import '../helpers/fake_api_client.dart';

void main() {
  group('QuotationsPage widget tests', () {
    testWidgets('shows loading then renders quotation tiles', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/quotations', [
        {
          'id': 'q1', 'appointmentId': 'a1', 'budget': 10000.0, 'subtotal': 4000.0,
          'total': 4500.0, 'isWithinBudget': true, 'status': 'PendingApproval',
          'items': [
            {'id': 'i1', 'category': 'Consultation', 'description': 'Check-up', 'quantity': 1, 'unitPrice': 2500.0, 'totalPrice': 2500.0},
          ],
          'createdAt': '2026-01-01', 'updatedAt': '2026-01-01',
        },
      ]);
      final provider = BillingProvider(BillingService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const QuotationsPage(),
          ),
        ),
      );

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      await tester.pumpAndSettle();

      expect(find.textContaining('Quotation'), findsOneWidget);
      expect(find.textContaining('LKR 4500.00'), findsOneWidget);
      expect(find.text('PendingApproval'), findsOneWidget);
    });

    testWidgets('shows empty state when no quotations', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/quotations', []);
      final provider = BillingProvider(BillingService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const QuotationsPage(),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.text('No quotations found'), findsOneWidget);
    });

    testWidgets('shows error state on API failure', (tester) async {
      final client = FakeApiClient();
      client.setError('/quotations', ApiError(500, 'Server error', {'detail': 'Billing service down'}));
      final provider = BillingProvider(BillingService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const QuotationsPage(),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.textContaining('Billing service down'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });
  });
}
