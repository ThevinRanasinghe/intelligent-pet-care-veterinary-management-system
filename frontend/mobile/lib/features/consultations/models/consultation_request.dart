/// Mirrors backend ConsultationRequestDto
/// (PetCare.Application/DTOs/Consultations/ConsultationRequestDto.cs).
class ConsultationRequest {
  final String id;
  final String petId;
  final String ownerId;
  final String? petName;
  final String symptoms;
  final String? symptomPhotoUrl;
  final String urgency;
  final String? preferredDate;

  /// One-hour slot start, serialized as a TimeSpan ("HH:mm:ss").
  final String? preferredTime;
  final String? organizationId;
  final String? organizationName;
  final String? additionalNotes;
  final String status;

  /// "Initial" (owner-filed) | "FollowUp" (veterinarian-requested).
  final String requestType;
  final String? requestedByVeterinarianName;
  final String? createdAt;

  ConsultationRequest({
    required this.id,
    required this.petId,
    this.ownerId = '',
    this.petName,
    this.symptoms = '',
    this.symptomPhotoUrl,
    this.urgency = 'Medium',
    this.preferredDate,
    this.preferredTime,
    this.organizationId,
    this.organizationName,
    this.additionalNotes,
    required this.status,
    this.requestType = 'Initial',
    this.requestedByVeterinarianName,
    this.createdAt,
  });

  /// True while the request is still moving through the clinic workflow.
  bool get isPending => !const {
        'AppointmentConfirmed',
        'Cancelled',
        'Rejected',
      }.contains(status);

  /// "HH:mm" for display, tolerating "HH:mm:ss" TimeSpan payloads.
  String? get preferredTimeShort {
    final t = preferredTime;
    if (t == null) return null;
    return t.length >= 5 ? t.substring(0, 5) : t;
  }

  /// "yyyy-MM-dd" for display, tolerating full ISO timestamps.
  String? get preferredDateShort {
    final d = preferredDate;
    if (d == null) return null;
    return d.length >= 10 ? d.substring(0, 10) : d;
  }

  factory ConsultationRequest.fromJson(Map<String, dynamic> json) {
    return ConsultationRequest(
      id: json['id'] as String,
      petId: json['petId'] as String? ?? '',
      ownerId: json['ownerId'] as String? ?? '',
      petName: json['petName'] as String?,
      symptoms: json['symptoms'] as String? ?? '',
      symptomPhotoUrl: json['symptomPhotoUrl'] as String?,
      urgency: json['urgency'] as String? ?? 'Medium',
      preferredDate: json['preferredDate'] as String?,
      preferredTime: json['preferredTime']?.toString(),
      organizationId: json['organizationId']?.toString(),
      organizationName: json['organizationName'] as String?,
      additionalNotes: json['additionalNotes'] as String?,
      status: json['status'] as String? ?? 'Draft',
      requestType: json['requestType'] as String? ?? 'Initial',
      requestedByVeterinarianName:
          json['requestedByVeterinarianName'] as String?,
      createdAt: json['createdAt']?.toString(),
    );
  }
}
