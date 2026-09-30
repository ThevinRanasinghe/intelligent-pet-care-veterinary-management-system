import 'package:flutter/foundation.dart';
import '../../core/state/load_state.dart';

export '../../core/state/load_state.dart' show LoadState;
import '../../core/network/api_error.dart';
import 'consultation_service.dart';
import 'models/availability.dart';
import 'models/clinic.dart';
import 'models/consultation_request.dart';


class ConsultationProvider extends ChangeNotifier {
  final ConsultationService _service;

  ConsultationProvider(this._service);

  LoadState _clinicsState = LoadState.idle;
  LoadState _monthState = LoadState.idle;
  LoadState _dayState = LoadState.idle;
  LoadState _listState = LoadState.idle;
  List<Clinic> _clinics = [];
  List<MonthAvailabilityDay> _monthDays = [];
  DayAvailability? _dayAvailability;
  List<ConsultationRequest> _myConsultations = [];
  String _errorMessage = '';
  bool _submitting = false;
  String? _submitError;

  LoadState get clinicsState => _clinicsState;
  LoadState get monthState => _monthState;
  LoadState get dayState => _dayState;
  LoadState get listState => _listState;
  List<Clinic> get clinics => _clinics;
  List<MonthAvailabilityDay> get monthDays => _monthDays;
  DayAvailability? get dayAvailability => _dayAvailability;
  List<ConsultationRequest> get myConsultations => _myConsultations;
  String get errorMessage => _errorMessage;
  bool get submitting => _submitting;
  String? get submitError => _submitError;

  Future<void> loadClinics() async {
    _clinicsState = LoadState.loading;
    _errorMessage = '';
    notifyListeners();
    try {
      _clinics = await _service.getOrganizations();
      _clinicsState = LoadState.success;
    } on ApiError catch (e) {
      _errorMessage = _extractMessage(e);
      _clinicsState = LoadState.error;
    } catch (e) {
      _errorMessage = e.toString();
      _clinicsState = LoadState.error;
    }
    notifyListeners();
  }

  Future<void> loadMonthAvailability(
      String organizationId, int year, int month) async {
    _monthState = LoadState.loading;
    _errorMessage = '';
    notifyListeners();
    try {
      _monthDays =
          await _service.getMonthAvailability(organizationId, year, month);
      _monthState = LoadState.success;
    } on ApiError catch (e) {
      _errorMessage = _extractMessage(e);
      _monthState = LoadState.error;
    } catch (e) {
      _errorMessage = e.toString();
      _monthState = LoadState.error;
    }
    notifyListeners();
  }

  Future<void> loadDayAvailability(String organizationId, String date) async {
    _dayState = LoadState.loading;
    _errorMessage = '';
    _dayAvailability = null;
    notifyListeners();
    try {
      _dayAvailability = await _service.getDayAvailability(organizationId, date);
      _dayState = LoadState.success;
    } on ApiError catch (e) {
      _errorMessage = _extractMessage(e);
      _dayState = LoadState.error;
    } catch (e) {
      _errorMessage = e.toString();
      _dayState = LoadState.error;
    }
    notifyListeners();
  }

  Future<void> loadMyConsultations() async {
    _listState = LoadState.loading;
    _errorMessage = '';
    notifyListeners();
    try {
      _myConsultations = await _service.getMyConsultations();
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

  /// Create + submit the consultation request. Returns the submitted
  /// request on success; on failure returns null and exposes a friendly
  /// message via [submitError] (409 → slot conflict copy).
  Future<ConsultationRequest?> bookConsultation({
    required String petId,
    required String ownerId,
    required String organizationId,
    required String preferredDate,
    required String preferredTime,
    required String symptoms,
    String? additionalNotes,
  }) async {
    _submitting = true;
    _submitError = null;
    notifyListeners();
    try {
      final created = await _service.createConsultation(
        petId: petId,
        ownerId: ownerId,
        organizationId: organizationId,
        preferredDate: preferredDate,
        preferredTime: preferredTime,
        symptoms: symptoms,
        additionalNotes: additionalNotes,
      );
      final submitted = await _service.submitConsultation(created.id);
      return submitted;
    } on ApiError catch (e) {
      _submitError = e.statusCode == 409
          ? 'This appointment slot is no longer available. Please select another time.'
          : _extractMessage(e);
      return null;
    } catch (e) {
      _submitError = e.toString();
      return null;
    } finally {
      _submitting = false;
      notifyListeners();
    }
  }

  Future<Clinic?> clinicForAppointment(String consultationRequestId) async {
    try {
      final consultation = await _service.getConsultationById(consultationRequestId);
      final orgId = consultation.organizationId;
      if (orgId == null) return null;
      final orgs = _clinics.isNotEmpty ? _clinics : await _service.getOrganizations();
      for (final org in orgs) {
        if (org.id == orgId && org.hasLocation) return org;
      }
      return null;
    } on ApiError {
      return null;
    }
  }

  String _extractMessage(ApiError e) {
    final body = e.body;
    if (body is Map) {
      final errors = body['errors'];
      if (errors is Map && errors.isNotEmpty) {
        final first = errors.values.first;
        if (first is List && first.isNotEmpty) return first.first.toString();
      }
      return body['detail'] ?? body['message'] ?? body['title'] ?? e.message;
    }
    if (body is String) return body;
    return e.message;
  }
}
