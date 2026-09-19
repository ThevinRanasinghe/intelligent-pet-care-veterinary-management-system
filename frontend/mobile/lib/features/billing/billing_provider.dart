import 'package:flutter/foundation.dart';
import '../../core/network/api_error.dart';
import 'billing_service.dart';
import 'models/quotation.dart';

enum LoadState { idle, loading, success, error }

class BillingProvider extends ChangeNotifier {
  final BillingService _service;

  BillingProvider(this._service);

  LoadState _listState = LoadState.idle;
  LoadState _detailState = LoadState.idle;
  List<Quotation> _quotations = [];
  Quotation? _quotation;
  String _errorMessage = '';

  LoadState get listState => _listState;
  LoadState get detailState => _detailState;
  List<Quotation> get quotations => _quotations;
  Quotation? get quotation => _quotation;
  String get errorMessage => _errorMessage;

  Future<void> loadQuotations() async {
    _listState = LoadState.loading;
    _errorMessage = '';
    notifyListeners();
    try {
      _quotations = await _service.getQuotations();
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

  Future<void> loadQuotation(String id) async {
    _detailState = LoadState.loading;
    _errorMessage = '';
    notifyListeners();
    try {
      _quotation = await _service.getQuotationById(id);
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

  String _extractMessage(ApiError e) {
    final body = e.body;
    if (body is Map) {
      return body['detail'] ?? body['title'] ?? e.message;
    }
    if (body is String) return body;
    return e.message;
  }
}
