import '../../core/network/api_client.dart';
import '../../core/network/api_error.dart';
import 'models/appointment_slot.dart';

class SchedulingService {
  final ApiClient _apiClient;

  SchedulingService(this._apiClient);

  Future<List<AppointmentSlot>> getAvailableSlots() async {
    try {
      return await _apiClient.getList<AppointmentSlot>(
        '/appointments/available-slots',
        fromJson: AppointmentSlot.fromJson,
      );
    } on ApiError {
      rethrow;
    }
  }

  Future<Appointment> getAppointmentById(String id) async {
    try {
      return await _apiClient.get<Appointment>(
        '/appointments/$id',
        fromJson: Appointment.fromJson,
      );
    } on ApiError catch (e) {
      if (e.statusCode == 404) {
        throw ApiError(404, 'Appointment not found');
      }
      rethrow;
    }
  }

  Future<Appointment?> getAppointmentForSlot(String slotId) async {
    final appointments = await getAppointments();
    for (final appointment in appointments) {
      if (appointment.appointmentSlotId == slotId && appointment.status != 'Cancelled') {
        return getAppointmentById(appointment.id);
      }
    }
    return null;
  }

  Future<List<Appointment>> getAppointments() async {
    try {
      return await _apiClient.getList<Appointment>(
        '/appointments',
        fromJson: Appointment.fromJson,
      );
    } on ApiError {
      rethrow;
    }
  }
}
