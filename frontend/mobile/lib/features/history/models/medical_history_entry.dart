// Owner-facing medical history aggregates the existing clinical read
// endpoints (all owner-gated server-side):
//   GET /examinations/pet/{petId}
//   GET /diagnoses/examination/{examinationId}
//   GET /treatmentrecords/diagnosis/{diagnosisId}
//   GET /prescriptions/treatment/{treatmentRecordId}

class HistoryPrescription {
  final String? medicineName;
  final String? medicineStrength;
  final String dosage;
  final int durationDays;
  final int quantity;
  final String requestStatus;
  final String? veterinarianName;

  const HistoryPrescription({
    this.medicineName,
    this.medicineStrength,
    required this.dosage,
    required this.durationDays,
    required this.quantity,
    required this.requestStatus,
    this.veterinarianName,
  });

  factory HistoryPrescription.fromJson(Map<String, dynamic> json) {
    return HistoryPrescription(
      medicineName: json['medicineName'] as String?,
      medicineStrength: json['medicineStrength'] as String?,
      dosage: json['dosage'] as String? ?? '',
      durationDays: (json['durationDays'] as num?)?.toInt() ?? 0,
      quantity: (json['quantity'] as num?)?.toInt() ?? 1,
      requestStatus: json['requestStatus'] as String? ?? '',
      veterinarianName: json['veterinarianName'] as String?,
    );
  }
}

class HistoryTreatment {
  final String id;
  final String procedureName;
  final String notes;
  final String status;
  final List<HistoryPrescription> prescriptions;

  const HistoryTreatment({
    required this.id,
    required this.procedureName,
    required this.notes,
    required this.status,
    this.prescriptions = const [],
  });

  factory HistoryTreatment.fromJson(Map<String, dynamic> json) {
    return HistoryTreatment(
      id: json['id'] as String,
      procedureName: json['procedureName'] as String? ?? '',
      notes: json['notes'] as String? ?? '',
      status: json['status'] as String? ?? '',
    );
  }

  HistoryTreatment withPrescriptions(List<HistoryPrescription> rx) {
    return HistoryTreatment(
      id: id,
      procedureName: procedureName,
      notes: notes,
      status: status,
      prescriptions: rx,
    );
  }
}

class HistoryDiagnosis {
  final String id;
  final String conditionName;
  final String description;
  final String severity;
  final List<HistoryTreatment> treatments;

  const HistoryDiagnosis({
    required this.id,
    required this.conditionName,
    required this.description,
    required this.severity,
    this.treatments = const [],
  });

  factory HistoryDiagnosis.fromJson(Map<String, dynamic> json) {
    return HistoryDiagnosis(
      id: json['id'] as String,
      conditionName: json['conditionName'] as String? ?? '',
      description: json['description'] as String? ?? '',
      severity: json['severity'] as String? ?? '',
    );
  }

  HistoryDiagnosis withTreatments(List<HistoryTreatment> treatments) {
    return HistoryDiagnosis(
      id: id,
      conditionName: conditionName,
      description: description,
      severity: severity,
      treatments: treatments,
    );
  }
}

/// One examination = one timeline entry in the medical history view.
class MedicalHistoryEntry {
  final String examinationId;
  final String petId;
  final DateTime examinationDate;
  final String symptoms;
  final String notes;
  final List<HistoryDiagnosis> diagnoses;

  const MedicalHistoryEntry({
    required this.examinationId,
    required this.petId,
    required this.examinationDate,
    required this.symptoms,
    required this.notes,
    this.diagnoses = const [],
  });
}
