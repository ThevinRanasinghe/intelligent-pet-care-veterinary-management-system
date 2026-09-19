import '../../core/network/api_client.dart';
import '../../core/network/api_error.dart';
import 'models/quotation.dart';

class BillingService {
  final ApiClient _apiClient;

  BillingService(this._apiClient);

  Future<List<Quotation>> getQuotations() async {
    try {
      return await _apiClient.getList<Quotation>(
        '/quotations',
        fromJson: Quotation.fromJson,
      );
    } on ApiError {
      rethrow;
    }
  }

  Future<Quotation> getQuotationById(String id) async {
    try {
      return await _apiClient.get<Quotation>(
        '/quotations/$id',
        fromJson: Quotation.fromJson,
      );
    } on ApiError catch (e) {
      if (e.statusCode == 404) {
        throw ApiError(404, 'Quotation not found');
      }
      rethrow;
    }
  }
}
