import { useEffect, useMemo, useState } from 'react';
import { ArrowDownUp, CalendarDays, Loader2, PawPrint, RefreshCw } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Modal } from '../../components/ui/Modal';
import { getMyAppointments, type AppointmentResponse } from '../../services/schedulingService';
import {
  createDiagnosis,
  createExamination,
  createPrescription,
  createTreatmentRecord,
  getDiagnosisByExamination,
  getMedicinesLookup,
  getTreatmentRecordsByDiagnosis,
  type MedicineLookup,
} from '../../services/treatmentService';
import { consultationService } from '../../services/api';
import {
  lookupsService,
  type DayAvailability,
  type MonthAvailabilityDay,
} from '../../services/lookupsService';
import type { DiagnosisSeverity, MedicineRequestStatus, Prescription } from '../../types/domain';
import { messageFrom } from '../../utils/errors';
import { formatDate } from '../../utils/format';
import { useAuth } from '../auth/AuthContext';
import { BookingCalendar, currentMonth, type VisibleMonth } from '../shared/booking/BookingCalendar';
import { SlotPicker } from '../shared/booking/SlotPicker';

type Chip = 'Today' | 'Upcoming' | 'Completed' | 'All';

const statusTone: Record<string, 'success' | 'warning' | 'neutral' | 'danger' | 'info'> = {
  Confirmed: 'success',
  Reserved: 'info',
  Completed: 'neutral',
  Cancelled: 'danger',
  Available: 'warning',
};

const requestTone: Record<string, 'warning' | 'success' | 'danger' | 'neutral'> = {
  Pending: 'warning',
  Issued: 'success',
  Unavailable: 'danger',
};

const dayKey = (iso: string) => iso.slice(0, 10);

/** One medicine line in the vet's medicine request (max 10 per request). */
type RxItem = {
  medicineId: string;
  quantity: string;
  dosage: string;
  frequency: string;
  route: string;
  duration: string;
};

const MAX_RX_ITEMS = 10;
const ROUTE_OPTIONS = ['Oral', 'Topical', 'Injectable', 'Otic', 'Ophthalmic', 'Other'];
const emptyRxItem = (): RxItem => ({ medicineId: '', quantity: '1', dosage: '', frequency: '', route: '', duration: '7' });

const formatTimeOfDay = (iso: string) => {
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) return '—';
  return parsed.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
};

const inputStyle = {
  width: '100%',
  padding: '9px 10px',
  border: '1px solid #d8d8d2',
  borderRadius: '8px',
  fontSize: '12px',
  boxSizing: 'border-box',
} as const;

const labelStyle = {
  display: 'block',
  marginBottom: '5px',
  fontSize: '10px',
  fontWeight: 700,
  color: '#777777',
} as const;

/** Veterinarian's own appointment list with clinical workflow actions. */
export function MyAppointmentsPage() {
  const [appointments, setAppointments] = useState<AppointmentResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const [chip, setChip] = useState<Chip>('All');
  const [statusFilter, setStatusFilter] = useState('All');
  const [petFilter, setPetFilter] = useState('');
  const [descending, setDescending] = useState(true);

  const [selected, setSelected] = useState<AppointmentResponse | null>(null);

  // Record-examination form
  const [examOpen, setExamOpen] = useState(false);
  const [examSymptoms, setExamSymptoms] = useState('');
  const [examNotes, setExamNotes] = useState('');
  const [examDate, setExamDate] = useState('');
  const [examCharge, setExamCharge] = useState('0');
  const [diagnosisName, setDiagnosisName] = useState('');
  const [diagnosisDescription, setDiagnosisDescription] = useState('');
  const [diagnosisSeverity, setDiagnosisSeverity] = useState<DiagnosisSeverity>('Moderate');
  const [procedureName, setProcedureName] = useState('');
  const [procedureNotes, setProcedureNotes] = useState('');
  const [examBusy, setExamBusy] = useState(false);
  const [examError, setExamError] = useState('');

  // Prescription / medicine request form — one request can hold up to
  // 10 different medicines, each row is one medicine kind.
  const [rxOpen, setRxOpen] = useState(false);
  const [medicines, setMedicines] = useState<MedicineLookup[]>([]);
  const [rxItems, setRxItems] = useState<RxItem[]>([emptyRxItem()]);
  const [rxInstructions, setRxInstructions] = useState('');
  const [rxBusy, setRxBusy] = useState(false);
  const [rxError, setRxError] = useState('');
  const [createdRequests, setCreatedRequests] = useState<Record<string, Prescription[]>>({});

  // Follow-up form — fixed-slot booking in the veterinarian's own clinic
  const { user } = useAuth();
  const vetOrganizationId = user?.organizationId ?? null;
  const [followUpOpen, setFollowUpOpen] = useState(false);
  const [followUpDate, setFollowUpDate] = useState<string | null>(null);
  const [followUpSlot, setFollowUpSlot] = useState<string | null>(null);
  const [followUpReason, setFollowUpReason] = useState('');
  const [followUpNotes, setFollowUpNotes] = useState('');
  const [followUpBusy, setFollowUpBusy] = useState(false);
  const [followUpError, setFollowUpError] = useState('');
  const [followUpMonth, setFollowUpMonth] = useState<VisibleMonth>(currentMonth());
  const [followUpMonthDays, setFollowUpMonthDays] = useState<MonthAvailabilityDay[] | null>(null);
  const [followUpMonthLoading, setFollowUpMonthLoading] = useState(false);
  const [followUpSlots, setFollowUpSlots] = useState<DayAvailability | null>(null);
  const [followUpSlotsLoading, setFollowUpSlotsLoading] = useState(false);

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      setAppointments(await getMyAppointments());
    } catch (err) {
      setAppointments([]);
      setError(messageFrom(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void load(); }, []);

  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => setNotice(''), 5000);
    return () => clearTimeout(t);
  }, [notice]);

  const today = dayKey(new Date().toISOString());

  const visible = useMemo(() => {
    const petNeedle = petFilter.trim().toLowerCase();
    return appointments
      .filter((a) => {
        if (chip === 'Today') return dayKey(a.scheduledStart) === today;
        if (chip === 'Upcoming')
          return dayKey(a.scheduledStart) >= today && a.status !== 'Completed' && a.status !== 'Cancelled';
        if (chip === 'Completed') return a.status === 'Completed';
        return true;
      })
      .filter((a) => (statusFilter === 'All' ? true : a.status === statusFilter))
      .filter((a) =>
        !petNeedle ||
        (a.petName ?? a.petId).toLowerCase().includes(petNeedle) ||
        (a.ownerName ?? '').toLowerCase().includes(petNeedle),
      )
      .sort((a, b) => {
        const diff = a.scheduledStart.localeCompare(b.scheduledStart);
        return descending ? -diff : diff;
      });
  }, [appointments, chip, statusFilter, petFilter, descending, today]);

  const openDetail = (appointment: AppointmentResponse) => {
    setSelected(appointment);
    setExamOpen(false);
    setRxOpen(false);
    setFollowUpOpen(false);
    setExamError('');
    setRxError('');
    setFollowUpError('');
    // Prefill examination fields from the consultation symptoms.
    setExamSymptoms(appointment.symptoms ?? '');
    setExamNotes('');
    setExamDate(new Date().toISOString().slice(0, 16));
    setExamCharge('0');
    setDiagnosisName('');
    setDiagnosisDescription('');
    setDiagnosisSeverity('Moderate');
    setProcedureName('');
    setProcedureNotes('');
  };

  const canRecordExam =
    !!selected && (selected.status === 'Confirmed' || selected.status === 'Reserved') && !selected.examinationId;
  const hasExam = !!selected?.examinationId;

  const handleRecordExamination = async () => {
    if (!selected) return;
    if (!examSymptoms.trim()) {
      setExamError('Symptoms / findings are required.');
      return;
    }
    const charge = Number(examCharge);
    if (!Number.isFinite(charge) || charge < 0) {
      setExamError('Veterinarian charge must be zero or more.');
      return;
    }
    setExamBusy(true);
    setExamError('');
    try {
      const examination = await createExamination({
        petId: selected.petId,
        veterinarianId: selected.veterinarianId,
        consultationRequestId: selected.consultationRequestId ?? undefined,
        appointmentId: selected.id,
        veterinarianCharge: charge,
        symptoms: examSymptoms.trim(),
        notes: examNotes.trim(),
        examinationDate: new Date(examDate).toISOString(),
      });
      if (diagnosisName.trim()) {
        const diagnosis = await createDiagnosis({
          examinationId: examination.id,
          conditionName: diagnosisName.trim(),
          description: diagnosisDescription.trim(),
          severity: diagnosisSeverity,
        });
        if (procedureName.trim()) {
          await createTreatmentRecord({
            diagnosisId: diagnosis.id,
            procedureName: procedureName.trim(),
            notes: procedureNotes.trim(),
          });
        }
      }
      setSelected({ ...selected, examinationId: examination.id, status: 'Completed' });
      setExamOpen(false);
      setNotice('Examination recorded — the appointment is now completed.');
      await load();
    } catch (err) {
      setExamError(messageFrom(err));
    } finally {
      setExamBusy(false);
    }
  };

  const openPrescriptionForm = async () => {
    setRxOpen(true);
    setRxError('');
    setRxItems([emptyRxItem()]);
    setRxInstructions('');
    if (medicines.length === 0) {
      try {
        setMedicines(await getMedicinesLookup());
      } catch (err) {
        setRxError(messageFrom(err));
      }
    }
  };

  const updateRxItem = (index: number, patch: Partial<RxItem>) => {
    setRxItems((current) => current.map((item, i) => (i === index ? { ...item, ...patch } : item)));
  };

  const handleCreatePrescription = async () => {
    if (!selected?.examinationId) return;
    if (rxItems.some((item) => !item.medicineId || !item.dosage.trim())) {
      setRxError('Select a medicine and enter the dosage for every row.');
      return;
    }
    const quantities = rxItems.map((item) => Number(item.quantity));
    const durations = rxItems.map((item) => Number(item.duration));
    if (quantities.some((q) => !Number.isInteger(q) || q < 1)) {
      setRxError('Quantity must be at least 1.');
      return;
    }
    if (durations.some((d) => !Number.isInteger(d) || d < 1)) {
      setRxError('Duration must be at least 1 day.');
      return;
    }
    setRxBusy(true);
    setRxError('');
    try {
      // The prescriptions hang off a treatment record under the exam's
      // diagnosis — fetch the existing chain (first record) or none.
      const diagnoses = await getDiagnosisByExamination(selected.examinationId);
      const diagnosis = diagnoses[0];
      if (!diagnosis) {
        setRxError('No diagnosis recorded for this examination — record one via Diagnosis & Treatment first.');
        return;
      }
      const records = await getTreatmentRecordsByDiagnosis(diagnosis.id);
      if (records.length === 0) {
        setRxError('No treatment record exists for this examination — record one via Diagnosis & Treatment first.');
        return;
      }
      const instructions = rxInstructions.trim() || undefined;
      const created = await createPrescription({
        treatmentRecordId: records[0].id,
        items: rxItems.map((item, index) => ({
          medicineId: item.medicineId,
          dosage: item.dosage.trim(),
          durationDays: durations[index],
          quantity: quantities[index],
          frequency: item.frequency.trim() || undefined,
          route: item.route || undefined,
          instructions,
        })),
      });
      setCreatedRequests((current) => ({
        ...current,
        [selected.id]: [...(current[selected.id] ?? []), ...created],
      }));
      setRxOpen(false);
      setRxItems([emptyRxItem()]);
      setRxInstructions('');
      setNotice(`Medicine request with ${created.length} medicine${created.length === 1 ? '' : 's'} sent to the inventory officer.`);
    } catch (err) {
      setRxError(messageFrom(err));
    } finally {
      setRxBusy(false);
    }
  };

  /* Month availability for the vet's own clinic (org-level capacity). */
  useEffect(() => {
    if (!followUpOpen || !vetOrganizationId) {
      setFollowUpMonthDays(null);
      return;
    }

    let mounted = true;
    setFollowUpMonthLoading(true);

    lookupsService
      .getMonthAvailability(vetOrganizationId, followUpMonth.year, followUpMonth.month)
      .then((days) => { if (mounted) setFollowUpMonthDays(days); })
      .catch((err) => {
        console.error('Failed to load follow-up availability:', err);
        if (mounted) setFollowUpMonthDays(null);
      })
      .finally(() => { if (mounted) setFollowUpMonthLoading(false); });

    return () => { mounted = false; };
  }, [followUpOpen, vetOrganizationId, followUpMonth]);

  /* Slot availability for the chosen follow-up date. */
  useEffect(() => {
    if (!followUpOpen || !vetOrganizationId || !followUpDate) {
      setFollowUpSlots(null);
      return;
    }

    let mounted = true;
    setFollowUpSlotsLoading(true);

    lookupsService
      .getAvailability(vetOrganizationId, followUpDate)
      .then((day) => { if (mounted) setFollowUpSlots(day); })
      .catch((err) => {
        console.error('Failed to load follow-up slots:', err);
        if (mounted) setFollowUpSlots(null);
      })
      .finally(() => { if (mounted) setFollowUpSlotsLoading(false); });

    return () => { mounted = false; };
  }, [followUpOpen, vetOrganizationId, followUpDate]);

  const openFollowUp = () => {
    setFollowUpOpen(true);
    setFollowUpError('');
    setFollowUpDate(null);
    setFollowUpSlot(null);
    setFollowUpSlots(null);
    setFollowUpMonth(currentMonth());
    if (!vetOrganizationId) {
      setFollowUpError('Your account is not linked to a clinic, so availability cannot be loaded.');
    }
  };

  const handleFollowUp = async () => {
    if (!selected) return;
    if (!followUpDate || !followUpSlot || !followUpReason.trim()) {
      setFollowUpError('Pick a date, a one-hour slot, and give a reason.');
      return;
    }
    setFollowUpBusy(true);
    setFollowUpError('');
    try {
      await consultationService.createFollowUp({
        petId: selected.petId,
        examinationId: selected.examinationId!,
        preferredDate: followUpDate,
        preferredTime: `${followUpSlot}:00`,
        reason: followUpReason.trim(),
        notes: followUpNotes.trim() || undefined,
      });
      setFollowUpOpen(false);
      setFollowUpReason('');
      setFollowUpNotes('');
      setFollowUpDate(null);
      setFollowUpSlot(null);
      setNotice('Follow-up request sent to the clinic manager.');
    } catch (err) {
      setFollowUpError(messageFrom(err));
    } finally {
      setFollowUpBusy(false);
    }
  };

  const selectedRequests = selected ? (createdRequests[selected.id] ?? []) : [];

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">VETERINARIAN</span>
          <h2>My Appointments</h2>
          <p>Your confirmed consultations — record examinations, request medicines and follow-ups.</p>
        </div>
        <div className="heading-actions">
          <Button variant="secondary" onClick={() => void load()} icon={<RefreshCw size={14} />}>Refresh</Button>
        </div>
      </div>

      {error && <div className="form-error" role="alert" style={{ marginBottom: '16px' }}><strong>Error:</strong> {error}</div>}
      {notice && <div className="notice" style={{ marginBottom: '16px' }}>{notice}</div>}

      <div className="filter-bar" style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '1rem', alignItems: 'center' }}>
        {(['Today', 'Upcoming', 'Completed', 'All'] as Chip[]).map((c) => (
          <button
            key={c}
            type="button"
            className={`btn ${chip === c ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setChip(c)}
          >
            {c}
          </button>
        ))}
        <select aria-label="Status filter" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
          {['All', 'Reserved', 'Confirmed', 'Completed', 'Cancelled'].map((s) => (
            <option key={s} value={s}>{s === 'All' ? 'All statuses' : s}</option>
          ))}
        </select>
        <input
          aria-label="Filter by pet"
          placeholder="Filter by pet or owner…"
          value={petFilter}
          onChange={(e) => setPetFilter(e.target.value)}
          style={{ ...inputStyle, width: '200px' }}
        />
        <Button
          variant="secondary"
          icon={<ArrowDownUp size={14} />}
          onClick={() => setDescending((v) => !v)}
          aria-label="Toggle date sort"
        >
          {descending ? 'Newest first' : 'Oldest first'}
        </Button>
      </div>

      {loading ? (
        <div className="empty-state"><Loader2 className="spinner" size={30} /><strong>Loading appointments…</strong></div>
      ) : visible.length === 0 ? (
        <div className="empty-state"><CalendarDays size={28} /><strong>No appointments match the current filters.</strong></div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr><th>Date</th><th>Time</th><th>Pet</th><th>Owner</th><th>Type</th><th>Symptoms</th><th>Status</th><th /></tr>
            </thead>
            <tbody>
              {visible.map((a) => (
                <tr key={a.id}>
                  <td>{formatDate(dayKey(a.scheduledStart))}</td>
                  <td>{formatTimeOfDay(a.scheduledStart)} – {formatTimeOfDay(a.scheduledEnd)}</td>
                  <td><strong>{a.petName ?? a.petId}</strong></td>
                  <td>{a.ownerName ?? '—'}</td>
                  <td>{a.type === 'FollowUp' ? <Badge tone="info">Follow-up</Badge> : <Badge tone="neutral">Initial</Badge>}</td>
                  <td>{a.symptoms ? (a.symptoms.length > 60 ? `${a.symptoms.slice(0, 60)}…` : a.symptoms) : '—'}</td>
                  <td><Badge tone={statusTone[a.status] ?? 'neutral'}>{a.status}</Badge></td>
                  <td style={{ textAlign: 'right' }}>
                    <Button variant="secondary" onClick={() => openDetail(a)}>Open</Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {selected && (
        <Modal title={`Appointment — ${selected.petName ?? selected.petId}`} onClose={() => setSelected(null)}>
          <div style={{ padding: '0 4px 8px' }}>
            <p className="muted" style={{ fontSize: '0.85rem' }}>
              {formatDate(dayKey(selected.scheduledStart))} · {formatTimeOfDay(selected.scheduledStart)} – {formatTimeOfDay(selected.scheduledEnd)} ·{' '}
              {selected.ownerName ?? 'Owner'} · <Badge tone={statusTone[selected.status] ?? 'neutral'}>{selected.status}</Badge>
              {selected.type === 'FollowUp' && <> <Badge tone="info">Follow-up</Badge></>}
            </p>
            {selected.symptoms && (
              <p style={{ fontSize: '0.85rem' }}><strong>Reported symptoms:</strong> {selected.symptoms}</p>
            )}
            {selected.notes && (
              <p className="muted" style={{ fontSize: '0.85rem' }}><strong>Notes:</strong> {selected.notes}</p>
            )}
            {hasExam && (
              <p className="muted" style={{ fontSize: '0.8rem' }}>Examination recorded: {selected.examinationId}</p>
            )}

            <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginTop: '0.75rem' }}>
              {canRecordExam && (
                <Button variant="primary" onClick={() => setExamOpen((v) => !v)}>Record Examination</Button>
              )}
              {hasExam && (
                <>
                  <Button variant="secondary" onClick={() => void openPrescriptionForm()}>Add Prescription / Medicine Request</Button>
                  <Button variant="secondary" onClick={openFollowUp}>Request Follow-up</Button>
                </>
              )}
            </div>

            {/* Record examination form */}
            {examOpen && canRecordExam && (
              <div style={{ marginTop: '1rem', padding: '14px', border: '1px solid #e8e8e2', borderRadius: '10px', background: '#fafaf7' }}>
                <h4 style={{ marginTop: 0 }}>Record Examination</h4>
                <label htmlFor="exam-symptoms" style={labelStyle}>SYMPTOMS / FINDINGS</label>
                <textarea id="exam-symptoms" aria-label="Symptoms" rows={3} value={examSymptoms} onChange={(e) => setExamSymptoms(e.target.value)} style={inputStyle} />
                <label htmlFor="exam-notes" style={{ ...labelStyle, marginTop: '8px' }}>NOTES</label>
                <textarea id="exam-notes" aria-label="Examination notes" rows={2} value={examNotes} onChange={(e) => setExamNotes(e.target.value)} style={inputStyle} />
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', marginTop: '8px' }}>
                  <div>
                    <label htmlFor="exam-date" style={labelStyle}>EXAMINATION DATE</label>
                    <input id="exam-date" aria-label="Examination date" type="datetime-local" value={examDate} onChange={(e) => setExamDate(e.target.value)} style={inputStyle} />
                  </div>
                  <div>
                    <label htmlFor="exam-charge" style={labelStyle}>VETERINARIAN CHARGE (LKR)</label>
                    <input id="exam-charge" aria-label="Veterinarian charge" type="number" min="0" value={examCharge} onChange={(e) => setExamCharge(e.target.value)} style={inputStyle} />
                  </div>
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', marginTop: '8px' }}>
                  <div>
                    <label htmlFor="diag-name" style={labelStyle}>DIAGNOSIS (OPTIONAL)</label>
                    <input id="diag-name" aria-label="Diagnosis condition" value={diagnosisName} onChange={(e) => setDiagnosisName(e.target.value)} placeholder="Condition name" style={inputStyle} />
                    <textarea aria-label="Diagnosis description" rows={2} value={diagnosisDescription} onChange={(e) => setDiagnosisDescription(e.target.value)} placeholder="Description" style={{ ...inputStyle, marginTop: '8px' }} />
                    <select aria-label="Diagnosis severity" value={diagnosisSeverity} onChange={(e) => setDiagnosisSeverity(e.target.value as DiagnosisSeverity)} style={{ ...inputStyle, marginTop: '8px' }}>
                      {(['Low', 'Moderate', 'High', 'Critical'] as DiagnosisSeverity[]).map((s) => <option key={s} value={s}>{s}</option>)}
                    </select>
                  </div>
                  <div>
                    <label htmlFor="proc-name" style={labelStyle}>TREATMENT (OPTIONAL)</label>
                    <input id="proc-name" aria-label="Treatment procedure" value={procedureName} onChange={(e) => setProcedureName(e.target.value)} placeholder="Procedure name" style={inputStyle} />
                    <textarea aria-label="Treatment notes" rows={2} value={procedureNotes} onChange={(e) => setProcedureNotes(e.target.value)} placeholder="Procedure notes" style={{ ...inputStyle, marginTop: '8px' }} />
                  </div>
                </div>
                {examError && <div className="form-error" role="alert" style={{ marginTop: '8px' }}>{examError}</div>}
                <Button style={{ marginTop: '10px' }} onClick={() => void handleRecordExamination()} disabled={examBusy}>
                  {examBusy ? 'Saving…' : 'Save examination'}
                </Button>
              </div>
            )}

            {/* Prescription / medicine request form — up to 10 medicine kinds per request */}
            {rxOpen && hasExam && (
              <div style={{ marginTop: '1rem', padding: '14px', border: '1px solid #e8e8e2', borderRadius: '10px', background: '#fafaf7' }}>
                <h4 style={{ marginTop: 0 }}>Add Prescription / Medicine Request</h4>
                {rxItems.map((item, index) => {
                  const taken = new Set(rxItems.filter((_, i) => i !== index).map((i) => i.medicineId));
                  return (
                    <div key={index} style={{ padding: '10px', border: '1px solid #e8e8e2', borderRadius: '8px', background: '#fff', marginBottom: '8px' }}>
                      <div style={{ display: 'flex', gap: '8px', alignItems: 'flex-end' }}>
                        <div style={{ flex: 1 }}>
                          <label htmlFor={`rx-medicine-${index}`} style={labelStyle}>MEDICINE {index + 1}</label>
                          <select
                            id={`rx-medicine-${index}`}
                            aria-label={`Medicine ${index + 1}`}
                            value={item.medicineId}
                            onChange={(e) => updateRxItem(index, { medicineId: e.target.value })}
                            style={inputStyle}
                          >
                            <option value="">Select a medicine…</option>
                            {medicines.filter((m) => !taken.has(m.id)).map((m) => (
                              <option key={m.id} value={m.id}>{m.name}</option>
                            ))}
                          </select>
                        </div>
                        {rxItems.length > 1 && (
                          <Button variant="secondary" aria-label={`Remove medicine ${index + 1}`} onClick={() => setRxItems((current) => current.filter((_, i) => i !== index))}>
                            Remove
                          </Button>
                        )}
                      </div>
                      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: '10px', marginTop: '8px' }}>
                        <div>
                          <label htmlFor={`rx-qty-${index}`} style={labelStyle}>QUANTITY</label>
                          <input id={`rx-qty-${index}`} aria-label={`Quantity ${index + 1}`} type="number" min="1" value={item.quantity} onChange={(e) => updateRxItem(index, { quantity: e.target.value })} style={inputStyle} />
                        </div>
                        <div>
                          <label htmlFor={`rx-dosage-${index}`} style={labelStyle}>DOSAGE</label>
                          <input id={`rx-dosage-${index}`} aria-label={`Dosage ${index + 1}`} value={item.dosage} onChange={(e) => updateRxItem(index, { dosage: e.target.value })} placeholder="1 pill" style={inputStyle} />
                        </div>
                        <div>
                          <label htmlFor={`rx-frequency-${index}`} style={labelStyle}>FREQUENCY</label>
                          <input id={`rx-frequency-${index}`} aria-label={`Frequency ${index + 1}`} value={item.frequency} onChange={(e) => updateRxItem(index, { frequency: e.target.value })} placeholder="twice daily" style={inputStyle} />
                        </div>
                        <div>
                          <label htmlFor={`rx-route-${index}`} style={labelStyle}>ROUTE</label>
                          <select id={`rx-route-${index}`} aria-label={`Route ${index + 1}`} value={item.route} onChange={(e) => updateRxItem(index, { route: e.target.value })} style={inputStyle}>
                            <option value="">—</option>
                            {ROUTE_OPTIONS.map((r) => <option key={r} value={r}>{r}</option>)}
                          </select>
                        </div>
                        <div>
                          <label htmlFor={`rx-duration-${index}`} style={labelStyle}>DURATION (DAYS)</label>
                          <input id={`rx-duration-${index}`} aria-label={`Duration days ${index + 1}`} type="number" min="1" value={item.duration} onChange={(e) => updateRxItem(index, { duration: e.target.value })} style={inputStyle} />
                        </div>
                      </div>
                    </div>
                  );
                })}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '8px' }}>
                  <span className="muted" style={{ fontSize: '0.8rem' }}>{rxItems.length} / {MAX_RX_ITEMS} medicines</span>
                  <Button
                    variant="secondary"
                    disabled={rxItems.length >= MAX_RX_ITEMS}
                    onClick={() => setRxItems((current) => [...current, emptyRxItem()])}
                  >
                    + Add medicine
                  </Button>
                </div>
                <label htmlFor="rx-instructions" style={{ ...labelStyle, marginTop: '8px' }}>INSTRUCTIONS</label>
                <textarea id="rx-instructions" aria-label="Instructions" rows={2} value={rxInstructions} onChange={(e) => setRxInstructions(e.target.value)} style={inputStyle} />
                {rxError && <div className="form-error" role="alert" style={{ marginTop: '8px' }}>{rxError}</div>}
                <Button style={{ marginTop: '10px' }} onClick={() => void handleCreatePrescription()} disabled={rxBusy}>
                  {rxBusy ? 'Sending…' : 'Send medicine request'}
                </Button>
              </div>
            )}

            {/* Created medicine requests for this appointment */}
            {selectedRequests.length > 0 && (
              <div style={{ marginTop: '1rem' }}>
                <h4 style={{ margin: '0 0 6px' }}>Medicine requests</h4>
                <ul style={{ margin: 0, paddingLeft: '1.1rem', fontSize: '0.85rem' }}>
                  {selectedRequests.map((p) => (
                    <li key={p.id} style={{ marginBottom: '4px' }}>
                      {p.medicineName ?? 'Medicine'} × {p.quantity ?? 1}{' '}
                      <Badge tone={requestTone[(p.requestStatus ?? 'Pending') as MedicineRequestStatus] ?? 'warning'}>
                        {p.requestStatus ?? 'Pending'}
                      </Badge>
                      {p.unavailableReason ? ` — ${p.unavailableReason}` : ''}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {/* Follow-up form — clinic availability + fixed one-hour slots */}
            {followUpOpen && hasExam && (
              <div style={{ marginTop: '1rem', padding: '14px', border: '1px solid #e8e8e2', borderRadius: '10px', background: '#fafaf7' }}>
                <h4 style={{ marginTop: 0 }}>Request Follow-up</h4>

                <div style={{ marginTop: '8px' }}>
                  <div style={labelStyle}>DATE</div>
                  {vetOrganizationId ? (
                    <BookingCalendar
                      visibleMonth={followUpMonth}
                      days={followUpMonthDays}
                      loading={followUpMonthLoading}
                      selectedDate={followUpDate}
                      onSelectDate={(iso) => { setFollowUpDate(iso); setFollowUpSlot(null); }}
                      onMonthChange={setFollowUpMonth}
                    />
                  ) : (
                    <div className="booking-slots-hint">
                      Your account is not linked to a clinic — availability cannot be loaded.
                    </div>
                  )}
                </div>

                <div style={{ marginTop: '10px' }}>
                  <div style={labelStyle}>TIME SLOT (ONE HOUR)</div>
                  <SlotPicker
                    slots={followUpSlots?.slots ?? null}
                    selectedStart={followUpSlot}
                    onSelect={setFollowUpSlot}
                    loading={followUpSlotsLoading}
                  />
                </div>

                <label htmlFor="fu-reason" style={{ ...labelStyle, marginTop: '8px' }}>REASON</label>
                <textarea id="fu-reason" aria-label="Reason" rows={2} value={followUpReason} onChange={(e) => setFollowUpReason(e.target.value)} style={inputStyle} />
                <label htmlFor="fu-notes" style={{ ...labelStyle, marginTop: '8px' }}>NOTES (OPTIONAL)</label>
                <textarea id="fu-notes" aria-label="Follow-up notes" rows={2} value={followUpNotes} onChange={(e) => setFollowUpNotes(e.target.value)} style={inputStyle} />
                {followUpError && <div className="form-error" role="alert" style={{ marginTop: '8px' }}>{followUpError}</div>}
                <Button style={{ marginTop: '10px' }} onClick={() => void handleFollowUp()} disabled={followUpBusy || !followUpDate || !followUpSlot}>
                  {followUpBusy ? 'Sending…' : 'Send follow-up request'}
                </Button>
              </div>
            )}
          </div>
        </Modal>
      )}
    </div>
  );
}

export default MyAppointmentsPage;
