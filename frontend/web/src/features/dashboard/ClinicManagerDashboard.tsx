import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { consultationService, type ConsultationRequestApi } from '../../services/api';
import { getAppointments, type AppointmentResponse } from '../../services/schedulingService';
import { getQuotations } from '../../services/billingService';
import { getApprovalProposals } from '../../services/approvalService';
import {
  getExpiring,
  getLowStock,
  getMedicines,
  getReservations,
} from '../../services/inventoryService';
import { getAllTreatmentRecords, getExaminations } from '../../services/treatmentService';
import type { Medicine, MedicineBatch, MedicineReservation, Quotation } from '../../types/domain';
import type { ApprovalProposal } from '../../types/domain';
import { formatDate } from '../../utils/format';

const ATTENTION_STATUSES = new Set(['Submitted', 'Processing', 'PendingApproval']);
const ACTIVE_TREATMENT_STATUSES = new Set(['Planned', 'InProgress']);

const cardStyle = {
  backgroundColor: '#ffffff',
  padding: '1.5rem',
  borderRadius: '0.75rem',
  border: '1px solid #e5e7eb',
} as const;

const statLabel: React.CSSProperties = { fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' };
const statValue: React.CSSProperties = { fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' };
const statHint: React.CSSProperties = { fontSize: '0.8rem', color: '#6b7280' };

/**
 * Clinic Manager dashboard — clinic-wide operational overview across
 * clinical, inventory, and management modules using real backend data.
 */
export function ClinicManagerDashboard() {
  const [consultations, setConsultations] = useState<ConsultationRequestApi[]>([]);
  const [appointments, setAppointments] = useState<AppointmentResponse[]>([]);
  const [quotations, setQuotations] = useState<Quotation[]>([]);
  const [approvals, setApprovals] = useState<ApprovalProposal[]>([]);
  const [medicines, setMedicines] = useState<Medicine[]>([]);
  const [lowStock, setLowStock] = useState<Medicine[]>([]);
  const [expiring, setExpiring] = useState<MedicineBatch[]>([]);
  const [reservations, setReservations] = useState<MedicineReservation[]>([]);
  const [examinationCount, setExaminationCount] = useState(0);
  const [activeTreatments, setActiveTreatments] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;

    async function load() {
      const results = await Promise.allSettled([
        consultationService.getAllConsultations(),
        getAppointments(),
        getQuotations(),
        getApprovalProposals(),
        getMedicines({ page: 1, pageSize: 500 }),
        getLowStock(),
        getExpiring(30),
        getReservations(),
        getExaminations(),
        getAllTreatmentRecords(),
      ]);
      if (!active) return;

      const [cons, appts, quotes, appr, meds, low, exp, res, exams, treatments] = results;
      if (cons.status === 'fulfilled') setConsultations(cons.value);
      if (appts.status === 'fulfilled') setAppointments(appts.value);
      if (quotes.status === 'fulfilled') setQuotations(quotes.value);
      if (appr.status === 'fulfilled') setApprovals(appr.value);
      if (meds.status === 'fulfilled') setMedicines(meds.value.items);
      if (low.status === 'fulfilled') setLowStock(low.value);
      if (exp.status === 'fulfilled') setExpiring(exp.value);
      if (res.status === 'fulfilled') setReservations(res.value);
      if (exams.status === 'fulfilled') setExaminationCount(exams.value.length);
      if (treatments.status === 'fulfilled') {
        setActiveTreatments(treatments.value.filter((t) => ACTIVE_TREATMENT_STATUSES.has(t.status)).length);
      }
      if (results.every((r) => r.status === 'rejected')) {
        setError('Could not load dashboard data. Please try again shortly.');
      }
      setLoading(false);
    }

    void load();
    return () => { active = false; };
  }, []);

  const today = new Date().toISOString().slice(0, 10);
  const todaysAppointments = appointments.filter((a) => a.scheduledStart?.slice(0, 10) === today);
  const awaitingConsultations = consultations.filter((c) => ATTENTION_STATUSES.has(c.status));
  const pendingQuotations = quotations.filter((q) => q.status === 'PendingApproval' || q.status === 'Draft');
  const pendingReservations = reservations.filter((r) => r.status === 'Reserved');
  const reservedUnits = medicines.reduce((sum, m) => sum + m.reservedQuantity, 0);
  const recentAwaiting = [...awaitingConsultations]
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    .slice(0, 5);

  const dash = loading ? '…' : undefined;

  return (
    <>
      {error && <div className="form-error" role="alert" style={{ marginBottom: '1rem' }}><strong>Error:</strong> {error}</div>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '1.25rem' }}>
        {/* Management */}
        <section style={cardStyle}>
          <h3 style={{ fontSize: '1rem', fontWeight: '700', color: '#111827', marginBottom: '1rem' }}>Management</h3>
          <div style={{ display: 'grid', gap: '1rem' }}>
            <div><div style={statLabel}>Today's Appointments</div><div style={statValue}>{dash ?? todaysAppointments.length}</div><div style={statHint}>Scheduled for today</div></div>
            <div><div style={statLabel}>Pending Approvals</div><div style={statValue}>{dash ?? approvals.length}</div><div style={statHint}>Awaiting your review</div></div>
            <div><div style={statLabel}>Open Quotations</div><div style={statValue}>{dash ?? pendingQuotations.length}</div><div style={statHint}>Draft or pending approval</div></div>
          </div>
        </section>

        {/* Clinical */}
        <section style={cardStyle}>
          <h3 style={{ fontSize: '1rem', fontWeight: '700', color: '#111827', marginBottom: '1rem' }}>Clinical Overview</h3>
          <div style={{ display: 'grid', gap: '1rem' }}>
            <div><div style={statLabel}>Consultation Requests</div><div style={statValue}>{dash ?? consultations.length}</div><div style={statHint}>{awaitingConsultations.length} awaiting review</div></div>
            <div><div style={statLabel}>Examinations</div><div style={statValue}>{dash ?? examinationCount}</div><div style={statHint}>Recorded to date</div></div>
            <div><div style={statLabel}>Active Treatments</div><div style={statValue}>{dash ?? activeTreatments}</div><div style={statHint}>Planned or in progress</div></div>
          </div>
        </section>

        {/* Inventory */}
        <section style={cardStyle}>
          <h3 style={{ fontSize: '1rem', fontWeight: '700', color: '#111827', marginBottom: '1rem' }}>Inventory Overview</h3>
          <div style={{ display: 'grid', gap: '1rem' }}>
            <div><div style={statLabel}>Medicines</div><div style={statValue}>{dash ?? medicines.length}</div><div style={statHint}>{reservedUnits} units reserved</div></div>
            <div><div style={statLabel}>Low Stock</div><div style={{ ...statValue, color: lowStock.length > 0 ? '#F59E0B' : '#111827' }}>{dash ?? lowStock.length}</div><div style={statHint}>At or below reorder level</div></div>
            <div><div style={statLabel}>Expiring Batches</div><div style={{ ...statValue, color: expiring.length > 0 ? '#DC2626' : '#111827' }}>{dash ?? expiring.length}</div><div style={statHint}>Within 30 days · {pendingReservations.length} pending reservations</div></div>
          </div>
        </section>
      </div>

      <div style={{ ...cardStyle, padding: '1.75rem', marginTop: '1.5rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Consultations Awaiting Review</h3>
        {loading ? (
          <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>Loading…</p>
        ) : recentAwaiting.length === 0 ? (
          <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>No consultation requests need attention right now.</p>
        ) : (
          <div style={{ display: 'grid', gap: '0.6rem' }}>
            {recentAwaiting.map((request) => (
              <div key={request.id} className="info-strip" style={{ justifyContent: 'space-between', gap: '0.75rem', flexWrap: 'wrap' }}>
                <div>
                  <strong>{request.petName ?? 'Patient'}</strong>
                  <span className="muted"> · {request.symptoms.length > 80 ? `${request.symptoms.slice(0, 80)}…` : request.symptoms}</span>
                </div>
                <span className="muted" style={{ fontSize: '0.8rem' }}>{request.status} · {formatDate(request.createdAt.slice(0, 10))}</span>
              </div>
            ))}
            <Link to="/consultations" style={{ fontSize: '0.85rem' }}>Open Consultation Requests →</Link>
          </div>
        )}
      </div>
    </>
  );
}

export default ClinicManagerDashboard;
