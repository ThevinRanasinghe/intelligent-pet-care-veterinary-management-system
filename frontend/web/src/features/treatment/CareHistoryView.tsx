import { ClipboardList, Pill, Stethoscope } from 'lucide-react';
import { Card } from '../../components/ui/Card';
import { Badge } from '../../components/ui/Badge';
import type { Pet } from '../../services/api';
import {
  getDiagnosisByExamination,
  getExaminationsByPet,
  getPrescriptionsByTreatment,
  getTreatmentRecordsByDiagnosis,
} from '../../services/treatmentService';
import type { Diagnosis, Examination, Prescription, TreatmentRecord } from '../../types/domain';
import { formatDate } from '../../utils/format';

export type CareEntry = {
  examination: Examination;
  pet: Pet;
  diagnosis: Diagnosis | null;
  treatments: Array<TreatmentRecord & { prescriptions: Prescription[] }>;
};

/** Builds the examination → diagnosis → treatment → prescription chain for a set of pets. */
export async function loadCareEntries(pets: Pet[]): Promise<CareEntry[]> {
  const records = await Promise.all(
    pets.flatMap((pet) =>
      (async () => {
        const examinations = await getExaminationsByPet(pet.id);
        return Promise.all(examinations.map(async (examination) => {
          const diagnosis = await getDiagnosisByExamination(examination.id).catch(() => null);
          const treatments = diagnosis
            ? await getTreatmentRecordsByDiagnosis(diagnosis.id)
            : [];
          const treatmentsWithPrescriptions = await Promise.all(
            treatments.map(async (treatment) => ({
              ...treatment,
              prescriptions: await getPrescriptionsByTreatment(treatment.id).catch(() => []),
            })),
          );
          return { examination, pet, diagnosis, treatments: treatmentsWithPrescriptions };
        }));
      })(),
    ),
  );
  return records.flat();
}

/** Renders examination → diagnosis → treatment → prescription chains as cards. */
export function CareHistoryView({ entries }: { entries: CareEntry[] }) {
  if (entries.length === 0) {
    return (
      <div className="empty-state">
        <ClipboardList size={28} />
        <strong>No care records are available yet.</strong>
      </div>
    );
  }

  return (
    <div style={{ display: 'grid', gap: '1rem' }}>
      {entries.map(({ examination, pet, diagnosis, treatments }) => (
        <Card key={examination.id}>
          <div className="card-header">
            <div>
              <div className="eyebrow">{pet.species} · {pet.breed || 'Pet'}</div>
              <h3>{pet.name}</h3>
              <p className="muted">Examined {formatDate(examination.examinationDate.slice(0, 10))}</p>
            </div>
            {diagnosis && <Badge tone={diagnosis.severity === 'Critical' ? 'danger' : diagnosis.severity === 'High' ? 'warning' : 'info'}>{diagnosis.severity}</Badge>}
          </div>

          <div style={{ display: 'grid', gap: '0.7rem', fontSize: '0.9rem' }}>
            <div><strong>Symptoms:</strong> {examination.symptoms}</div>
            {diagnosis && <div><strong>Diagnosis:</strong> {diagnosis.conditionName}{diagnosis.description ? ` — ${diagnosis.description}` : ''}</div>}
            {treatments.length > 0 && (
              <div>
                <strong><Stethoscope size={15} style={{ verticalAlign: 'text-bottom' }} /> Treatment progress</strong>
                <div style={{ display: 'grid', gap: '0.5rem', marginTop: '0.5rem' }}>
                  {treatments.map((treatment) => (
                    <div key={treatment.id} className="info-strip" style={{ alignItems: 'flex-start' }}>
                      <div>
                        <strong>{treatment.procedureName}</strong> <Badge tone={treatment.status === 'Completed' ? 'success' : 'info'}>{treatment.status}</Badge>
                        {treatment.notes && <span>{treatment.notes}</span>}
                        {treatment.prescriptions.length > 0 && (
                          <span><Pill size={14} style={{ verticalAlign: 'text-bottom' }} /> Prescription: {treatment.prescriptions.map((prescription) => `${prescription.dosage} for ${prescription.durationDays} days`).join(', ')}</span>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </Card>
      ))}
    </div>
  );
}
