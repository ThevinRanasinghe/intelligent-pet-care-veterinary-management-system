class Approval {
  final String id;
  final String quotationId;
  final double quotationTotal;
  final double quotationBudget;
  final String status;
  final String? reviewedBy;
  final String? reviewedAt;
  final String? comment;

  Approval({
    required this.id,
    required this.quotationId,
    required this.quotationTotal,
    required this.quotationBudget,
    required this.status,
    this.reviewedBy,
    this.reviewedAt,
    this.comment,
  });

  factory Approval.fromJson(Map<String, dynamic> json) {
    return Approval(
      id: json['id'] as String,
      quotationId: json['quotationId'] as String,
      quotationTotal: (json['quotationTotal'] as num).toDouble(),
      quotationBudget: (json['quotationBudget'] as num).toDouble(),
      status: json['status'] as String,
      reviewedBy: json['reviewedBy'] as String?,
      reviewedAt: json['reviewedAt'] as String?,
      comment: json['comment'] as String?,
    );
  }
}

class ApprovalHistoryEntry {
  final String id;
  final String approvalId;
  final String previousStatus;
  final String newStatus;
  final String changedBy;
  final String? reason;
  final String changedAt;

  ApprovalHistoryEntry({
    required this.id,
    required this.approvalId,
    required this.previousStatus,
    required this.newStatus,
    required this.changedBy,
    this.reason,
    required this.changedAt,
  });

  factory ApprovalHistoryEntry.fromJson(Map<String, dynamic> json) {
    return ApprovalHistoryEntry(
      id: json['id'] as String,
      approvalId: json['approvalId'] as String,
      previousStatus: json['previousStatus'] as String,
      newStatus: json['newStatus'] as String,
      changedBy: json['changedBy'] as String,
      reason: json['reason'] as String?,
      changedAt: json['changedAt'] as String,
    );
  }
}
