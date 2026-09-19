import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:petcare_mobile/core/network/api_client.dart';
import 'package:petcare_mobile/core/network/api_error.dart';
import 'package:petcare_mobile/features/approval/approval_service.dart';
import 'package:petcare_mobile/features/approval/approval_provider.dart';
import 'package:petcare_mobile/features/approval/approval_detail_page.dart';
import 'package:petcare_mobile/features/approval/approval_history_page.dart';
import 'package:petcare_mobile/core/routing/app_router.dart';
import '../helpers/fake_api_client.dart';

void main() {
  group('ApprovalDetailPage widget tests', () {
    testWidgets('shows loading indicator initially', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/approvals/apr-1', {
        'id': 'apr-1', 'quotationId': 'q1', 'quotationTotal': 4500.0,
        'quotationBudget': 10000.0, 'status': 'Pending',
      });
      final provider = ApprovalProvider(ApprovalService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const ApprovalDetailPage(approvalId: 'apr-1'),
          ),
        ),
      );

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
    });

    testWidgets('renders approval detail fields on success', (tester) async {
      final client = FakeApiClient();
      client.setResponse('/approvals/apr-1', {
        'id': 'apr-1', 'quotationId': 'q1', 'quotationTotal': 4500.0,
        'quotationBudget': 10000.0, 'status': 'Pending',
        'reviewedBy': 'manager-1', 'reviewedAt': '2026-01-02T12:00:00Z',
        'comment': 'Looks reasonable',
      });
      final provider = ApprovalProvider(ApprovalService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const ApprovalDetailPage(approvalId: 'apr-1'),
          ),
        ),
      );

      await tester.pumpAndSettle();

      expect(find.text('Approval Details'), findsOneWidget);
      expect(find.text('apr-1'), findsOneWidget);
      expect(find.text('q1'), findsOneWidget);
      expect(find.text('Pending'), findsOneWidget);
      expect(find.text('LKR 4500.00'), findsOneWidget);
      expect(find.text('LKR 10000.00'), findsOneWidget);
      expect(find.text('Yes'), findsOneWidget);
      expect(find.text('manager-1'), findsOneWidget);
      expect(find.textContaining('Looks reasonable'), findsOneWidget);
      expect(find.text('View History'), findsOneWidget);
      expect(client.requestedPaths, contains('/approvals/apr-1'));
    });

    testWidgets('shows error state on 404 not-found', (tester) async {
      final client = FakeApiClient();
      client.setError('/approvals/apr-missing', ApiError(404, 'Approval not found'));
      final provider = ApprovalProvider(ApprovalService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const ApprovalDetailPage(approvalId: 'apr-missing'),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.textContaining('Approval not found'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('shows error state on API failure', (tester) async {
      final client = FakeApiClient();
      client.setError('/approvals/apr-1', ApiError(500, 'Server error', {'detail': 'Approval service down'}));
      final provider = ApprovalProvider(ApprovalService(client));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const ApprovalDetailPage(approvalId: 'apr-1'),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.textContaining('Approval service down'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('retry recovers from API error', (tester) async {
      var calls = 0;
      final transport = MockClient((request) async {
        calls++;
        if (calls == 1) {
          return http.Response('', 500);
        }
        return http.Response(
          '{"id":"apr-1","quotationId":"q1","quotationTotal":4500.0,"quotationBudget":10000.0,"status":"Pending"}',
          200,
          headers: {'content-type': 'application/json'},
        );
      });
      addTearDown(transport.close);
      final provider = ApprovalProvider(ApprovalService(ApiClient(client: transport)));

      await tester.pumpWidget(
        MaterialApp(
          home: ChangeNotifierProvider.value(
            value: provider,
            child: const ApprovalDetailPage(approvalId: 'apr-1'),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.text('Retry'), findsOneWidget);

      await tester.tap(find.text('Retry'));
      await tester.pumpAndSettle();

      expect(calls, 2);
      expect(find.text('apr-1'), findsOneWidget);
      expect(find.text('Pending'), findsOneWidget);
    });

    testWidgets('navigates to ApprovalHistoryPage via View History button', (tester) async {
      final client = FakeApiClient();
      // Set history before detail so path matching resolves correctly
      client.setResponse('/history', [
        {'id': 'h1', 'approvalId': 'apr-1', 'previousStatus': 'Pending', 'newStatus': 'Approved', 'changedBy': '12345678-1234-1234-1234-123456789012', 'reason': 'Looks good', 'changedAt': '2026-01-02T00:00:00'},
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
            home: ApprovalDetailPage(approvalId: 'apr-1'),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.text('View History'), findsOneWidget);

      await tester.tap(find.text('View History'));
      await tester.pumpAndSettle();

      expect(find.byType(ApprovalHistoryPage), findsOneWidget);
      expect(find.text('Approval History'), findsOneWidget);
      expect(find.text('Pending -> Approved'), findsOneWidget);
      expect(client.requestedPaths, contains('/approvals/apr-1/history'));
    });
  });
}
