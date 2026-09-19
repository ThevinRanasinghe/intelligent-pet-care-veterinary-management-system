import 'package:flutter/foundation.dart';
import '../../core/network/api_error.dart';
import 'approval_service.dart';
import 'models/approval.dart';

enum LoadState { idle, loading, success, error }

class ApprovalProvider extends ChangeNotifier {
  final ApprovalService _service;

  ApprovalProvider(this._service);

  LoadState _listState = LoadState.idle;
  LoadState _detailState = LoadState.idle;
  LoadState _historyState = LoadState.idle;
  List<Approval> _approvals = [];
  Approval? _approval;
  List<ApprovalHistoryEntry> _history = [];
  String _errorMessage = '';

  LoadState get listState => _listState;
  LoadState get detailState => _detailState;
  LoadState get historyState => _historyState;
  List<Approval> get approvals => _approvals;
  Approval? get approval => _approval;
  List<ApprovalHistoryEntry> get history => _history;
  String get errorMessage => _errorMessage;

  Future<void> loadPendingApprovals() async {
    _listState = LoadState.loading;
    _errorMessage = '';
    notifyListeners();
    try {
      _approvals = await _service.getPendingApprovals();
      _listState = LoadState.success;
    } on ApiError catch (e) {
      _errorMessage = _extractMessage(e);
      _listState = LoadState.error;
    } catch (e) {
      _errorMessage = e.toString();
      _listState = LoadState.error;
    }
    notifyListeners();
  }

  Future<void> loadApproval(String id) async {
    _detailState = LoadState.loading;
    _errorMessage = '';
    notifyListeners();
    try {
      _approval = await _service.getApprovalById(id);
      _detailState = LoadState.success;
    } on ApiError catch (e) {
      _errorMessage = _extractMessage(e);
      _detailState = LoadState.error;
    } catch (e) {
      _errorMessage = e.toString();
      _detailState = LoadState.error;
    }
    notifyListeners();
  }

  Future<void> loadHistory(String approvalId) async {
    _historyState = LoadState.loading;
    _errorMessage = '';
    notifyListeners();
    try {
      _history = await _service.getApprovalHistory(approvalId);
      _historyState = LoadState.success;
    } on ApiError catch (e) {
      _errorMessage = _extractMessage(e);
      _historyState = LoadState.error;
    } catch (e) {
      _errorMessage = e.toString();
      _historyState = LoadState.error;
    }
    notifyListeners();
  }

  String _extractMessage(ApiError e) {
    final body = e.body;
    if (body is Map) {
      return body['detail'] ?? body['title'] ?? e.message;
    }
    if (body is String) return body;
    return e.message;
  }
}
