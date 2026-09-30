import { useState } from 'react';
import { ClipboardList, Pill, Receipt, Stethoscope } from 'lucide-react';
import { Card } from '../../components/ui/Card';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Modal } from '../../components/ui/Modal';
import type { Pet } from '../../services/api';
import {
  getDiagnosisByExamination,
  getExaminationsByPet,
  getPrescriptionsByTreatment,
  getTreatmentRecordsByDiagnosis,
} from '../../services/treatmentService';
import type { Diagnosis, Examination, Prescription, Quotation, TreatmentRecord } from '../../types/domain';
import { formatDate, formatLkr } from '../../utils/format';
import { OwnerBillDetail } from '../billing/OwnerBillDetail';

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
          const diagnoses = await getDiagnosisByExamination(examination.id).catch(() => []);
          const diagnosis = diagnoses[0] ?? null;
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

const medicationTone: Record<string, 'warning' | 'success' | 'danger' | 'neutral'> = {
  Pending: 'warning',
  Issued: 'success',
  Unavailable: 'danger',
};

const fieldLabel: React.CSSProperties = { color: '#777777', fontSize: '0.75rem', fontWeight: 600 };

/** One prescribed medicine with the vet's administration instructions. */
function MedicationInstructionCard({ prescription }: { prescription: Prescription }) {
  const status = prescription.requestStatus ?? 'Pending';
  const medicineLabel = [prescription.medicineName, prescription.medicineStrength].filter(Boolean).join(' ')
    + (prescription.medicineDosageForm ? ` (${prescription.medicineDosageForm})` : '');
  return (
    <div className="info-strip" style={{ alignItems: 'flex-start', flexDirection: 'column', gap: '0.35rem' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: '0.5rem', width: '100%', flexWrap: 'wrap', alignItems: 'center' }}>
        <strong>{medicineLabel || `Medicine ${prescription.prescriptionNumber ?? ''}`}</strong>
        <span style={{ display: 'inline-flex', gap: '0.4rem', alignItems: 'center' }}>
          {prescription.prescriptionNumber && <span className="muted" style={{ fontSize: '0.72rem' }}>{prescription.prescriptionNumber}</span>}
          <Badge tone={medicationTone[status] ?? 'warning'}>{status}</Badge>
        </span>
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.35rem 1.4rem', fontSize: '0.85rem' }}>
        <span><span style={fieldLabel}>Quantity:</span> {prescription.quantity ?? 1}</span>
        <span><span style={fieldLabel}>Dosage:</span> {prescription.dosage}</span>
        {prescription.frequency && <span><span style={fieldLabel}>Frequency:</span> {prescription.frequency}</span>}
        {prescription.route && <span><span style={fieldLabel}>Route:</span> {prescription.route}</span>}
        <span><span style={fieldLabel}>Duration:</span> {prescription.durationDays} day{prescription.durationDays === 1 ? '' : 's'}</span>
      </div>
      {prescription.instructions && (
        <div style={{ fontSize: '0.85rem' }}><span style={fieldLabel}>Instructions:</span> {prescription.instructions}</div>
      )}
      {status === 'Unavailable' && prescription.unavailableReason && (
        <div className="muted" style={{ fontSize: '0.78rem' }}>Unavailable: {prescription.unavailableReason}</div>
      )}
    </div>
  );
}

/**
 * Renders examination → diagnosis → treatment → prescription chains as
 * cards. When `billsByExamination` is provided (pet-owner view), each
 * prescription section links to its invoice and opens the full bill.
 */
export function CareHistoryView({
  entries,
  billsByExamination,
}: {
  entries: CareEntry[];
  /** Optional examinationId → bill map (pet-owner view only). */
  billsByExamination?: Map<string, Quotation>;
}) {
  const [openBill, setOpenBill] = useState<Quotation | null>(null);

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
      {entries.map(({ examination, pet, diagnosis, treatments }) => {
        const bill = billsByExamination?.get(examination.id);
        return (
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
                    <div key={treatment.id} className="info-strip" style={{ alignItems: 'flex-start', flexDirection: 'column', gap: '0.5rem' }}>
                      <div>
                        <strong>{treatment.procedureName}</strong> <Badge tone={treatment.status === 'Completed' ? 'success' : 'info'}>{treatment.status}</Badge>
                        {treatment.notes && <span> {treatment.notes}</span>}
                      </div>
                      {treatment.prescriptions.length > 0 && (
                        <div style={{ display: 'grid', gap: '0.5rem', width: '100%' }}>
                          <div className="muted" style={{ fontSize: '0.78rem', fontWeight: 600 }}>
                            <Pill size={13} style={{ verticalAlign: 'text-bottom' }} /> PRESCRIPTION
                            {treatment.prescriptions[0]?.veterinarianName ? ` — Dr. ${treatment.prescriptions[0].veterinarianName}` : ''}
                            {treatment.prescriptions[0]?.createdAt ? ` · ${formatDate(treatment.prescriptions[0].createdAt.slice(0, 10))}` : ''}
                          </div>
                          {treatment.prescriptions.map((prescription) => (
                            <MedicationInstructionCard key={prescription.id} prescription={prescription} />
                          ))}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}
            {bill && (
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', flexWrap: 'wrap' }}>
                <Receipt size={15} />
                <span><strong>{bill.invoiceNumber}</strong> · {formatLkr(bill.total ?? 0)} · {bill.paymentStatus === 'Paid' ? 'Paid' : 'Pending payment'}</span>
                <Button variant="secondary" onClick={() => setOpenBill(bill)}>View bill</Button>
              </div>
            )}
          </div>
        </Card>
        );
      })}

      {openBill && (
        <Modal title={`Bill ${openBill.invoiceNumber ?? ''}`} onClose={() => setOpenBill(null)}>
          <div style={{ padding: '0 4px 8px' }}>
            <OwnerBillDetail bill={openBill} onClose={() => setOpenBill(null)} />
          </div>
        </Modal>
      )}
    </div>
  );
}
