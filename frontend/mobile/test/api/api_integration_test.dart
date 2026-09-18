import 'package:flutter_test/flutter_test.dart';
import 'package:petcare_mobile/core/network/api_error.dart';
import 'package:petcare_mobile/features/scheduling/scheduling_service.dart';
import 'package:petcare_mobile/features/billing/billing_service.dart';
import 'package:petcare_mobile/features/approval/approval_service.dart';
import '../helpers/fake_api_client.dart';

void main() {
  group('SchedulingService API integration', () {
    test('getAvailableSlots returns parsed slots on success', () async {
      final client = FakeApiClient();
      client.setResponse('available-slots', [
        {'id': 's1', 'veterinarianId': 'vet-1', 'date': '2026-01-10', 'startTime': '09:00:00', 'endTime': '09:30:00', 'branch': 'Colombo', 'status': 'Available'},
      ]);
      final service = SchedulingService(client);
      final slots = await service.getAvailableSlots();
      expect(slots.length, 1);
      expect(slots[0].branch, 'Colombo');
    });

    test('getAvailableSlots throws ApiError on 401', () async {
      final client = FakeApiClient();
      client.setError('available-slots', ApiError(401, 'Unauthorized'));
      final service = SchedulingService(client);
      expect(() => service.getAvailableSlots(), throwsA(isA<ApiError>()));
    });

    test('getAvailableSlots throws ApiError on 500', () async {
      final client = FakeApiClient();
      client.setError('available-slots', ApiError(500, 'Server error'));
      final service = SchedulingService(client);
      expect(() => service.getAvailableSlots(), throwsA(isA<ApiError>()));
    });
  });

  group('BillingService API integration', () {
    test('getQuotations returns parsed quotations on success', () async {
      final client = FakeApiClient();
      client.setResponse('/quotations', [
        {
          'id': 'q1', 'appointmentId': 'a1', 'budget': 10000.0, 'subtotal': 4000.0,
          'total': 4500.0, 'isWithinBudget': true, 'status': 'PendingApproval',
          'items': [], 'createdAt': '2026-01-01', 'updatedAt': '2026-01-01',
        },
      ]);
      final service = BillingService(client);
      final quotes = await service.getQuotations();
      expect(quotes.length, 1);
      expect(quotes[0].status, 'PendingApproval');
    });

    test('getQuotations throws ApiError on 401', () async {
      final client = FakeApiClient();
      client.setError('/quotations', ApiError(401, 'Unauthorized'));
      final service = BillingService(client);
      expect(() => service.getQuotations(), throwsA(isA<ApiError>()));
    });

    test('getQuotationById throws ApiError on 403', () async {
      final client = FakeApiClient();
      client.setError('/quotations/', ApiError(403, 'Forbidden'));
      final service = BillingService(client);
      expect(() => service.getQuotationById('q1'), throwsA(isA<ApiError>()));
    });
  });

  group('ApprovalService API integration', () {
    test('getPendingApprovals returns parsed approvals on success', () async {
      final client = FakeApiClient();
      client.setResponse('/approvals/pending', [
        {'id': 'apr-1', 'quotationId': 'q1', 'quotationTotal': 4500.0, 'quotationBudget': 10000.0, 'status': 'Pending'},
      ]);
      final service = ApprovalService(client);
      final approvals = await service.getPendingApprovals();
      expect(approvals.length, 1);
      expect(approvals[0].status, 'Pending');
    });

    test('getApprovalHistory returns parsed history on success', () async {
      final client = FakeApiClient();
      client.setResponse('/history', [
        {'id': 'h1', 'approvalId': 'apr-1', 'previousStatus': 'Pending', 'newStatus': 'Approved', 'changedBy': 'mgr-1', 'reason': 'OK', 'changedAt': '2026-01-02'},
      ]);
      final service = ApprovalService(client);
      final history = await service.getApprovalHistory('apr-1');
      expect(history.length, 1);
      expect(history[0].newStatus, 'Approved');
    });

    test('getPendingApprovals throws ApiError on 401', () async {
      final client = FakeApiClient();
      client.setError('/approvals/pending', ApiError(401, 'Unauthorized'));
      final service = ApprovalService(client);
      expect(() => service.getPendingApprovals(), throwsA(isA<ApiError>()));
    });

    test('getPendingApprovals throws ApiError on 500', () async {
      final client = FakeApiClient();
      client.setError('/approvals/pending', ApiError(500, 'Server error'));
      final service = ApprovalService(client);
      expect(() => service.getPendingApprovals(), throwsA(isA<ApiError>()));
    });
  });
}
