import { useEffect, useMemo, useState } from 'react';
import { PawPrint, Pill, Search } from 'lucide-react';
import {
  getAllDiagnoses,
  getAllPrescriptions,
  getAllTreatmentRecords,
  getExaminations,
  getMedicinesLookup,
  getPetsLookup,
  type MedicineLookup,
  type PetLookup,
} from '../../services/treatmentService';
import type { Diagnosis, Examination, Prescription, TreatmentRecord } from '../../types/domain';
import { formatDate } from '../../utils/format';

type PrescriptionRow = Prescription & {
  medicineName: string;
  petName: string;
  procedureName: string;
  conditionName: string;
  treatmentStatus: string;
};

/**
 * Staff-facing prescriptions list — every prescription issued across all
 * patients, joined to its treatment, diagnosis, examination, and pet.
 */
export function PrescriptionsPage() {
  const [rows, setRows] = useState<PrescriptionRow[]>([]);
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState('');

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setMessage('');
        const [prescriptions, treatments, diagnoses, examinations, pets, medicines] = await Promise.all([
          getAllPrescriptions(),
          getAllTreatmentRecords(),
          getAllDiagnoses(),
          getExaminations(),
          getPetsLookup().catch(() => [] as PetLookup[]),
          getMedicinesLookup().catch(() => [] as MedicineLookup[]),
        ]);
        if (!active) return;

        const treatmentById = new Map<string, TreatmentRecord>(treatments.map((t) => [t.id, t]));
        const diagnosisById = new Map<string, Diagnosis>(diagnoses.map((d) => [d.id, d]));
        const examinationById = new Map<string, Examination>(examinations.map((e) => [e.id, e]));
        const petById = new Map<string, PetLookup>(pets.map((p) => [p.id, p]));
        const medicineById = new Map<string, MedicineLookup>(medicines.map((m) => [m.id, m]));

        setRows(prescriptions.map((prescription) => {
          const treatment = treatmentById.get(prescription.treatmentRecordId);
          const diagnosis = treatment ? diagnosisById.get(treatment.diagnosisId) : undefined;
          const examination = diagnosis ? examinationById.get(diagnosis.examinationId) : undefined;
          const pet = examination ? petById.get(examination.petId) : undefined;
          const medicine = medicineById.get(prescription.medicineId);

          return {
            ...prescription,
            medicineName: medicine?.name ?? `Medicine #${prescription.medicineId.slice(0, 8)}…`,
            petName: pet ? `${pet.name} (${pet.species})` : '—',
            procedureName: treatment?.procedureName ?? '—',
            conditionName: diagnosis?.conditionName ?? '—',
            treatmentStatus: treatment?.status ?? '—',
          };
        }).sort((a, b) => b.createdAt.localeCompare(a.createdAt)));
      } catch {
        if (active) {
          setRows([]);
          setMessage('Could not load prescriptions. Please try again shortly.');
        }
      } finally {
        if (active) setLoading(false);
      }
    }

    void load();
    return () => { active = false; };
  }, []);

  const visibleRows = useMemo(() => {
    const normalized = query.trim().toLowerCase();
    if (!normalized) return rows;
    return rows.filter((row) =>
      [row.medicineName, row.petName, row.procedureName, row.conditionName, row.dosage, row.treatmentStatus]
        .join(' ')
        .toLowerCase()
        .includes(normalized),
    );
  }, [rows, query]);

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">CLINICAL RECORDS</span>
          <h2>Prescriptions</h2>
          <p>Medications prescribed across all patient treatments.</p>
        </div>
        <div className="heading-actions">
          <div style={{ position: 'relative' }}>
            <Search size={15} style={{ position: 'absolute', left: '0.7rem', top: '50%', transform: 'translateY(-50%)', opacity: 0.5 }} />
            <input
              type="search"
              placeholder="Search prescriptions…"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              style={{ paddingLeft: '2rem', minWidth: 'min(240px, 100%)' }}
            />
          </div>
        </div>
      </div>

      {loading ? (
        <div className="empty-state"><strong>Loading prescriptions…</strong></div>
      ) : message ? (
        <div className="empty-state"><PawPrint size={28} /><strong>{message}</strong></div>
      ) : visibleRows.length === 0 ? (
        <div className="empty-state"><Pill size={28} /><strong>No prescriptions found.</strong></div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Pet</th>
                <th>Medicine</th>
                <th>Dosage</th>
                <th>Duration</th>
                <th>Treatment</th>
                <th>Diagnosis</th>
                <th>Status</th>
                <th>Issued</th>
              </tr>
            </thead>
            <tbody>
              {visibleRows.map((row) => (
                <tr key={row.id}>
                  <td>{row.petName}</td>
                  <td>{row.medicineName}</td>
                  <td>{row.dosage}</td>
                  <td>{row.durationDays} days</td>
                  <td>{row.procedureName}</td>
                  <td>{row.conditionName}</td>
                  <td>{row.treatmentStatus}</td>
                  <td>{formatDate(row.createdAt.slice(0, 10))}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default PrescriptionsPage;
