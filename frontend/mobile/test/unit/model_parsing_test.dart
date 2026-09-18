import 'package:flutter_test/flutter_test.dart';
import 'package:petcare_mobile/features/scheduling/models/appointment_slot.dart';
import 'package:petcare_mobile/features/billing/models/quotation.dart';
import 'package:petcare_mobile/features/approval/models/approval.dart';

void main() {
  group('AppointmentSlot.fromJson', () {
    test('parses all fields correctly', () {
      final json = {
        'id': 'slot-1',
        'veterinarianId': 'vet-1',
        'date': '2026-01-10',
        'startTime': '09:00:00',
        'endTime': '09:30:00',
        'branch': 'Colombo',
        'status': 'Available',
      };
      final slot = AppointmentSlot.fromJson(json);
      expect(slot.id, 'slot-1');
      expect(slot.veterinarianId, 'vet-1');
      expect(slot.date, '2026-01-10');
      expect(slot.startTime, '09:00:00');
      expect(slot.endTime, '09:30:00');
      expect(slot.branch, 'Colombo');
      expect(slot.status, 'Available');
    });
  });

  group('Appointment.fromJson', () {
    test('parses all fields including nullable notes', () {
      final json = {
        'id': 'appt-1',
        'petId': 'pet-1',
        'veterinarianId': 'vet-1',
        'appointmentSlotId': 'slot-1',
        'scheduledStart': '2026-01-10T09:00:00',
        'scheduledEnd': '2026-01-10T09:30:00',
        'status': 'Reserved',
        'notes': 'Follow-up visit',
        'createdAt': '2026-01-01T00:00:00',
        'updatedAt': '2026-01-01T00:00:00',
      };
      final appt = Appointment.fromJson(json);
      expect(appt.id, 'appt-1');
      expect(appt.petId, 'pet-1');
      expect(appt.status, 'Reserved');
      expect(appt.notes, 'Follow-up visit');
    });

    test('handles null notes', () {
      final json = {
        'id': 'appt-2',
        'petId': 'pet-2',
        'veterinarianId': 'vet-2',
        'appointmentSlotId': 'slot-2',
        'scheduledStart': '2026-01-10T10:00:00',
        'scheduledEnd': '2026-01-10T10:30:00',
        'status': 'Confirmed',
        'notes': null,
        'createdAt': '2026-01-01T00:00:00',
        'updatedAt': '2026-01-01T00:00:00',
      };
      final appt = Appointment.fromJson(json);
      expect(appt.notes, isNull);
    });
  });

  group('Quotation.fromJson', () {
    test('parses quotation with items', () {
      final json = {
        'id': 'quo-1',
        'appointmentId': 'appt-1',
        'budget': 10000.0,
        'subtotal': 4000.0,
        'total': 4500.0,
        'isWithinBudget': true,
        'status': 'PendingApproval',
        'items': [
          {
            'id': 'item-1',
            'category': 'Consultation',
            'description': 'General check-up',
            'quantity': 1,
            'unitPrice': 2500.0,
            'totalPrice': 2500.0,
          },
          {
            'id': 'item-2',
            'category': 'Medicine',
            'description': 'Antibiotics',
            'quantity': 2,
            'unitPrice': 1000.0,
            'totalPrice': 2000.0,
          },
        ],
        'createdAt': '2026-01-01T00:00:00',
        'updatedAt': '2026-01-01T00:00:00',
      };
      final q = Quotation.fromJson(json);
      expect(q.id, 'quo-1');
      expect(q.budget, 10000.0);
      expect(q.total, 4500.0);
      expect(q.isWithinBudget, true);
      expect(q.items.length, 2);
      expect(q.items[0].description, 'General check-up');
      expect(q.items[1].quantity, 2);
    });
  });

  group('QuotationItem.fromJson', () {
    test('parses numeric fields from int or double', () {
      final json = {
        'id': 'item-1',
        'category': 'Treatment',
        'description': 'Surgery',
        'quantity': 3,
        'unitPrice': 500.5,
        'totalPrice': 1501.5,
      };
      final item = QuotationItem.fromJson(json);
      expect(item.quantity, 3);
      expect(item.unitPrice, 500.5);
      expect(item.totalPrice, 1501.5);
    });
  });

  group('Approval.fromJson', () {
    test('parses all fields', () {
      final json = {
        'id': 'apr-1',
        'quotationId': 'quo-1',
        'quotationTotal': 4500.0,
        'quotationBudget': 10000.0,
        'status': 'Pending',
        'reviewedBy': null,
        'reviewedAt': null,
        'comment': null,
      };
      final a = Approval.fromJson(json);
      expect(a.id, 'apr-1');
      expect(a.quotationTotal, 4500.0);
      expect(a.quotationBudget, 10000.0);
      expect(a.status, 'Pending');
      expect(a.reviewedBy, isNull);
    });

    test('parses with reviewedBy and comment', () {
      final json = {
        'id': 'apr-2',
        'quotationId': 'quo-2',
        'quotationTotal': 3000.0,
        'quotationBudget': 5000.0,
        'status': 'Approved',
        'reviewedBy': 'manager-1',
        'reviewedAt': '2026-01-02T00:00:00',
        'comment': 'Looks good',
      };
      final a = Approval.fromJson(json);
      expect(a.status, 'Approved');
      expect(a.reviewedBy, 'manager-1');
      expect(a.comment, 'Looks good');
    });
  });

  group('ApprovalHistoryEntry.fromJson', () {
    test('parses all fields', () {
      final json = {
        'id': 'hist-1',
        'approvalId': 'apr-1',
        'previousStatus': 'Pending',
        'newStatus': 'Approved',
        'changedBy': 'manager-1',
        'reason': 'Approved',
        'changedAt': '2026-01-02T00:00:00',
      };
      final h = ApprovalHistoryEntry.fromJson(json);
      expect(h.id, 'hist-1');
      expect(h.previousStatus, 'Pending');
      expect(h.newStatus, 'Approved');
      expect(h.changedBy, 'manager-1');
      expect(h.reason, 'Approved');
    });

    test('handles null reason', () {
      final json = {
        'id': 'hist-2',
        'approvalId': 'apr-1',
        'previousStatus': 'Pending',
        'newStatus': 'Approved',
        'changedBy': 'manager-1',
        'reason': null,
        'changedAt': '2026-01-02T00:00:00',
      };
      final h = ApprovalHistoryEntry.fromJson(json);
      expect(h.reason, isNull);
    });
  });
}
