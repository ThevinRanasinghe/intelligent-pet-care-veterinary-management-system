import 'package:flutter/foundation.dart';
import '../../core/network/api_error.dart';
import 'scheduling_service.dart';
import 'models/appointment_slot.dart';

enum LoadState { idle, loading, success, error }

class SchedulingProvider extends ChangeNotifier {
  final SchedulingService _service;

  SchedulingProvider(this._service);

  LoadState _slotsState = LoadState.idle;
  LoadState _appointmentState = LoadState.idle;
  List<AppointmentSlot> _slots = [];
  Appointment? _appointment;
  String _errorMessage = '';

  LoadState get slotsState => _slotsState;
  LoadState get appointmentState => _appointmentState;
  List<AppointmentSlot> get slots => _slots;
  Appointment? get appointment => _appointment;
  String get errorMessage => _errorMessage;

  Future<void> loadSlots() async {
    _slotsState = LoadState.loading;
    _errorMessage = '';
    notifyListeners();
    try {
      _slots = await _service.getAvailableSlots();
      _slotsState = LoadState.success;
    } on ApiError catch (e) {
      _errorMessage = _extractMessage(e);
      _slotsState = LoadState.error;
    } catch (e) {
      _errorMessage = e.toString();
      _slotsState = LoadState.error;
    }
    notifyListeners();
  }

  Future<void> loadAppointment(String id, {bool forSlot = false}) async {
    _appointmentState = LoadState.loading;
    _errorMessage = '';
    notifyListeners();
    try {
      _appointment = forSlot
          ? await _service.getAppointmentForSlot(id)
          : await _service.getAppointmentById(id);
      _appointmentState = LoadState.success;
    } on ApiError catch (e) {
      _errorMessage = _extractMessage(e);
      _appointmentState = LoadState.error;
    } catch (e) {
      _errorMessage = e.toString();
      _appointmentState = LoadState.error;
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
