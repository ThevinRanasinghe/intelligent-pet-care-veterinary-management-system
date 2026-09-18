import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/network/api_error.dart';
import 'package:petcare_mobile/features/approval/approval_service.dart';
import 'package:petcare_mobile/features/approval/approval_provider.dart';
import 'package:petcare_mobile/features/approval/approvals_page.dart';
import 'package:petcare_mobile/features/approval/approval_detail_page.dart';
import 'package:petcare_mobile/features/approval/approval_history_page.dart';
import 'package:petcare_mobile/core/routing/app_router.dart';
import '../helpers/fake_api_client.dart';

void main() {
  group('ApprovalsPage widget tests', () {
    testWidgets('shows loading then renders approval tiles', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/approvals/pending', [
        {'id': 'apr-1', 'quotationId': 'q1', 'quotationTotal': 4500.0, 'quotationBudget': 10000.0, 'status': 'Pending'},
      ]);
      final provider = ApprovalProvider(ApprovalService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const ApprovalsPage(),
          ),
        ),
      );

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      await tester.pumpAndSettle();

      expect(find.textContaining('Approval'), findsOneWidget);
      expect(find.text('Pending'), findsOneWidget);
      expect(find.byIcon(Icons.check_circle), findsOneWidget);
    });

    testWidgets('shows empty state when no approvals', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/approvals/pending', []);
      final provider = ApprovalProvider(ApprovalService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const ApprovalsPage(),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.text('No pending approvals'), findsOneWidget);
    });

    testWidgets('shows error state on API failure', (tester) async {
      final client = FakeApiClient();
      client.setError('/approvals/pending', ApiError(401, 'Unauthorized', {'detail': 'Please log in'}));
      final provider = ApprovalProvider(ApprovalService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const ApprovalsPage(),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.textContaining('Please log in'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('tapping an approval opens ApprovalDetailPage with the real approval ID', (tester) async {
      final client = FakeApiClient();
      // Set history before detail so path matching resolves correctly
      client.setResponse('/history', []);
      client.setResponse('/approvals/pending', [
        {'id': 'apr-1', 'quotationId': 'q1', 'quotationTotal': 4500.0, 'quotationBudget': 10000.0, 'status': 'Pending'},
      ]);
      client.setResponse('/approvals/apr-1', {
        'id': 'apr-1', 'quotationId': 'q1', 'quotationTotal': 4500.0,
        'quotationBudget': 10000.0, 'status': 'Pending',
      });
      final provider = ApprovalProvider(ApprovalService(client));

      await tester.pumpWidget(
        ChangeNotifierProvider.value(
          value: provider,
          child: const MaterialApp(
            onGenerateRoute: AppRouter.onGenerateRoute,
            home: ApprovalsPage(),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.textContaining('Approval'), findsOneWidget);

      await tester.tap(find.textContaining('Approval'));
      await tester.pumpAndSettle();

      expect(find.byType(ApprovalDetailPage), findsOneWidget);
      expect(find.text('Approval Details'), findsOneWidget);
      expect(find.text('apr-1'), findsOneWidget);
      expect(find.text('Pending'), findsOneWidget);
      expect(client.requestedPaths, contains('/approvals/apr-1'));
      expect(client.requestedPaths, isNot(contains('/approvals/apr-1/history')));
    });
  });

  group('ApprovalHistoryPage widget tests', () {
    testWidgets('renders history entries', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/history', [
        {'id': 'h1', 'approvalId': 'apr-1', 'previousStatus': 'Pending', 'newStatus': 'Approved', 'changedBy': '12345678-1234-1234-1234-123456789012', 'reason': 'Looks good', 'changedAt': '2026-01-02T00:00:00'},
      ]);
      final provider = ApprovalProvider(ApprovalService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const ApprovalHistoryPage(approvalId: 'apr-1'),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.text('Pending -> Approved'), findsOneWidget);
      expect(find.textContaining('Looks good'), findsOneWidget);
    });

    testWidgets('shows empty state when no history', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/history', []);
      final provider = ApprovalProvider(ApprovalService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const ApprovalHistoryPage(approvalId: 'apr-1'),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.text('No history entries'), findsOneWidget);
    });
  });
}
