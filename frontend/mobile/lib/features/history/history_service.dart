import '../../core/network/api_client.dart';
import 'models/medical_history_entry.dart';

/// Aggregates the owner-readable clinical endpoints into a per-pet
/// timeline. No new backend endpoints: every read is already
/// owner-scoped server-side (OwnsPet / OwnsExamination checks).
class HistoryService {
  final ApiClient _api;

  HistoryService(this._api);

  Future<List<MedicalHistoryEntry>> getPetHistory(String petId) async {
    final exams = await _api.getList<Map<String, dynamic>>(
      '/examinations/pet/$petId',
      fromJson: (json) => json,
    );
    final entries =
        await Future.wait(exams.map((exam) => _buildEntry(exam)));
    entries.sort((a, b) => b.examinationDate.compareTo(a.examinationDate));
    return entries;
  }

  Future<MedicalHistoryEntry> _buildEntry(Map<String, dynamic> exam) async {
    final examId = exam['id'] as String;
    final diagnoses = await _api.getList<HistoryDiagnosis>(
      '/diagnoses/examination/$examId',
      fromJson: HistoryDiagnosis.fromJson,
    );
    final withTreatments = await Future.wait(
      diagnoses.map((d) async {
        final treatments = await _api.getList<HistoryTreatment>(
          '/treatmentrecords/diagnosis/${d.id}',
          fromJson: HistoryTreatment.fromJson,
        );
        final withRx = await Future.wait(
          treatments.map((t) async {
            final rx = await _api.getList<HistoryPrescription>(
              '/prescriptions/treatment/${t.id}',
              fromJson: HistoryPrescription.fromJson,
            );
            return t.withPrescriptions(rx);
          }),
        );
        return d.withTreatments(withRx);
      }),
    );
    return MedicalHistoryEntry(
      examinationId: examId,
      petId: exam['petId'] as String? ?? '',
      examinationDate: DateTime.tryParse(
              exam['examinationDate'] as String? ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0),
      symptoms: exam['symptoms'] as String? ?? '',
      notes: exam['notes'] as String? ?? '',
      diagnoses: withTreatments,
    );
  }
}
