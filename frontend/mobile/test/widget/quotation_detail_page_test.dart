import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/features/billing/billing_service.dart';
import 'package:petcare_mobile/features/billing/billing_provider.dart';
import 'package:petcare_mobile/features/billing/quotation_detail_page.dart';
import '../helpers/fake_api_client.dart';

void main() {
  group('QuotationDetailPage widget tests', () {
    testWidgets('renders invoice number, parties, totals and Paid chip', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/quotations/q-1', {
        'id': 'q-1',
        'invoiceNumber': 'INV-AAAA0001',
        'appointmentId': 'appt-1',
        'budget': 3000.0,
        'subtotal': 2800.0,
        'total': 2800.0,
        'isWithinBudget': true,
        'status': 'Finalised',
        'paymentStatus': 'Paid',
        'paidAt': '2026-09-27T00:00:00Z',
        'petName': 'Shadow',
        'ownerName': 'Amal',
        'veterinarianName': 'Dr. Silva',
        'examinationDate': '2026-09-26T10:05:00',
        'veterinarianChargeTotal': 2500.0,
        'medicineTotal': 300.0,
        'items': [
          {'id': 'i1', 'category': 'Examination', 'description': 'Veterinarian charge — Dr. Silva', 'quantity': 1, 'unitPrice': 2500.0, 'totalPrice': 2500.0},
          {'id': 'i2', 'category': 'Medicine', 'description': 'Amoxicillin (1 pill)', 'quantity': 3, 'unitPrice': 100.0, 'totalPrice': 300.0},
        ],
        'createdAt': '2026-09-26T10:00:00Z',
        'updatedAt': '2026-09-27T00:00:00Z',
      });
      final provider = BillingProvider(BillingService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const QuotationDetailPage(quotationId: 'q-1'),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('INV-AAAA0001'), findsOneWidget);
      expect(find.text('Paid'), findsOneWidget);
      expect(find.text('Shadow'), findsOneWidget);
      expect(find.text('Amal'), findsOneWidget);
      expect(find.text('Dr. Silva'), findsOneWidget);
      expect(find.text('LKR 2500.00'), findsWidgets);
      expect(find.text('LKR 300.00'), findsWidgets);
      // Appears for both Subtotal and Grand total.
      expect(find.text('LKR 2800.00'), findsWidgets);
      // Read-only: no payment action in the mobile app.
      expect(find.text('Mark as Paid'), findsNothing);
    });
  });
}
