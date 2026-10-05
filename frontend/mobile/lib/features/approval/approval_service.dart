import '../../core/network/api_client.dart';
import '../../core/network/api_error.dart';
import 'models/approval.dart';

class ApprovalService {
  final ApiClient _apiClient;

  ApprovalService(this._apiClient);

  Future<List<Approval>> getPendingApprovals() async {
    try {
      return await _apiClient.getList<Approval>(
        '/approvals/pending',
        fromJson: Approval.fromJson,
      );
    } on ApiError {
      rethrow;
    }
  }

  Future<Approval> getApprovalById(String id) async {
    try {
      return await _apiClient.get<Approval>(
        '/approvals/$id',
        fromJson: Approval.fromJson,
      );
    } on ApiError catch (e) {
      if (e.statusCode == 404) {
        throw ApiError(404, 'Approval not found');
      }
      rethrow;
    }
  }

  Future<List<ApprovalHistoryEntry>> getApprovalHistory(String id) async {
    try {
      return await _apiClient.getList<ApprovalHistoryEntry>(
        '/approvals/$id/history',
        fromJson: ApprovalHistoryEntry.fromJson,
      );
    } on ApiError {
      rethrow;
    }
  }
}
