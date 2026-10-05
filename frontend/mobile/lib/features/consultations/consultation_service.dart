import '../../core/network/api_client.dart';
import '../../core/network/api_error.dart';
import '../scheduling/models/appointment_slot.dart';
import 'models/availability.dart';
import 'models/clinic.dart';
import 'models/consultation_request.dart';

/// Owner-facing consultation/booking API.
/// Mirrors the calls the React owner flow makes (lookupsService.ts +
/// consultationService in api.ts) — same endpoints, same payload shape.
class ConsultationService {
  final ApiClient _apiClient;

  ConsultationService(this._apiClient);

  /// Active organizations an owner can book at: GET /lookups/organizations.
  Future<List<Clinic>> getOrganizations() async {
    return _apiClient.getList<Clinic>(
      '/lookups/organizations',
      fromJson: Clinic.fromJson,
    );
  }

  /// GET /consultations/availability/month?organizationId&year&month.
  Future<List<MonthAvailabilityDay>> getMonthAvailability(
    String organizationId,
    int year,
    int month,
  ) async {
    return _apiClient.getList<MonthAvailabilityDay>(
      '/consultations/availability/month?organizationId=$organizationId&year=$year&month=$month',
      fromJson: MonthAvailabilityDay.fromJson,
    );
  }

  /// GET /consultations/availability?organizationId&date ("yyyy-MM-dd").
  Future<DayAvailability> getDayAvailability(
    String organizationId,
    String date,
  ) async {
    return _apiClient.get<DayAvailability>(
      '/consultations/availability?organizationId=$organizationId&date=$date',
      fromJson: DayAvailability.fromJson,
    );
  }

  /// Owner-scoped list: GET /consultations returns only the caller's own
  /// requests for a PetOwner token.
  Future<List<ConsultationRequest>> getMyConsultations() async {
    return _apiClient.getList<ConsultationRequest>(
      '/consultations',
      fromJson: ConsultationRequest.fromJson,
    );
  }

  Future<ConsultationRequest> getConsultationById(String id) async {
    return _apiClient.get<ConsultationRequest>(
      '/consultations/$id',
      fromJson: ConsultationRequest.fromJson,
    );
  }

  /// POST /consultations — creates the request in Draft status.
  /// [preferredDate] is "yyyy-MM-dd"; [preferredTime] the hour-aligned
  /// slot start ("HH:mm"). The server stamps OwnerId for PetOwner callers.
  Future<ConsultationRequest> createConsultation({
    required String petId,
    required String ownerId,
    required String organizationId,
    required String preferredDate,
    required String preferredTime,
    required String symptoms,
    String? additionalNotes,
  }) async {
    return _apiClient.post<ConsultationRequest>(
      '/consultations',
      body: {
        'petId': petId,
        'ownerId': ownerId,
        'organizationId': organizationId,
        'symptoms': symptoms,
        'preferredDate': preferredDate,
        // TimeSpan binds from "HH:mm:ss" — pad "HH:mm" slot starts.
        'preferredTime':
            preferredTime.length == 5 ? '$preferredTime:00' : preferredTime,
        if (additionalNotes != null && additionalNotes.isNotEmpty)
          'additionalNotes': additionalNotes,
      },
      fromJson: ConsultationRequest.fromJson,
    );
  }

  /// POST /consultations/{id}/submit — moves a Draft to Submitted.
  Future<ConsultationRequest> submitConsultation(String id) async {
    return _apiClient.post<ConsultationRequest>(
      '/consultations/$id/submit',
      fromJson: ConsultationRequest.fromJson,
    );
  }

  /// Resolves the clinic an appointment was booked at, for the owner
  /// "Get Directions" action. The appointment payload carries no
  /// organization fields, so the linked consultation's organizationId is
  /// matched against /lookups/organizations. Returns null when the clinic
  /// cannot be resolved or has no stored coordinates.
  Future<Clinic?> getClinicForAppointment(Appointment appointment) async {
    final consultationId = appointment.consultationRequestId;
    if (consultationId == null || consultationId.isEmpty) return null;
    try {
      final consultation = await getConsultationById(consultationId);
      final orgId = consultation.organizationId;
      if (orgId == null) return null;
      final orgs = await getOrganizations();
      for (final org in orgs) {
        if (org.id == orgId && org.hasLocation) return org;
      }
      return null;
    } on ApiError {
      return null;
    }
  }
}
