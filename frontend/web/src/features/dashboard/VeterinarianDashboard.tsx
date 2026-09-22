import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { consultationService, type ConsultationRequestApi } from '../../services/api';
import {
  getAllPrescriptions,
  getAllTreatmentRecords,
  getExaminations,
} from '../../services/treatmentService';

const ATTENTION_STATUSES = new Set(['Submitted', 'Processing', 'PendingApproval']);
const ACTIVE_TREATMENT_STATUSES = new Set(['Planned', 'InProgress']);

const cardStyle = {
  backgroundColor: '#ffffff',
  padding: '1.5rem',
  borderRadius: '0.75rem',
  border: '1px solid #e5e7eb',
} as const;

/**
 * Veterinarian dashboard — clinical workload overview built from real
 * consultation, examination, treatment, and prescription data.
 * Renders inside the shared DashboardLayout shell.
 */
export function VeterinarianDashboard() {
  const [consultations, setConsultations] = useState<ConsultationRequestApi[]>([]);
  const [examinationCount, setExaminationCount] = useState(0);
  const [activeTreatments, setActiveTreatments] = useState(0);
  const [prescriptionCount, setPrescriptionCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        const [consultationData, examinations, treatments, prescriptions] = await Promise.all([
          consultationService.getAllConsultations(),
          getExaminations(),
          getAllTreatmentRecords(),
          getAllPrescriptions(),
        ]);
        if (!active) return;
        setConsultations(consultationData);
        setExaminationCount(examinations.length);
        setActiveTreatments(treatments.filter((t) => ACTIVE_TREATMENT_STATUSES.has(t.status)).length);
        setPrescriptionCount(prescriptions.length);
      } catch {
        if (active) setError('Could not load dashboard data. Please try again shortly.');
      } finally {
        if (active) setLoading(false);
      }
    }

    void load();
    return () => { active = false; };
  }, []);

  const awaitingReview = consultations.filter((c) => ATTENTION_STATUSES.has(c.status));
  const recentAwaiting = [...awaitingReview]
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    .slice(0, 5);

  return (
    <>
      {error && <div className="notice error" role="alert">{error}</div>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Requests Awaiting Review</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>{loading ? '…' : awaitingReview.length}</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Submitted or in-progress consultations</div>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Examinations</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>{loading ? '…' : examinationCount}</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Clinical examinations on record</div>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Active Treatments</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>{loading ? '…' : activeTreatments}</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Planned or in-progress treatment records</div>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Prescriptions</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>{loading ? '…' : prescriptionCount}</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Prescriptions issued</div>
        </div>
      </div>

      <div style={{ ...cardStyle, padding: '1.75rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Consultations Needing Attention</h3>
        {loading ? (
          <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>Loading…</p>
        ) : recentAwaiting.length === 0 ? (
          <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
            No consultation requests are waiting for review right now.
          </p>
        ) : (
          <div style={{ display: 'grid', gap: '0.6rem' }}>
            {recentAwaiting.map((request) => (
              <div key={request.id} className="info-strip" style={{ justifyContent: 'space-between', gap: '0.75rem', flexWrap: 'wrap' }}>
                <div>
                  <strong>{request.petName ?? 'Patient'}</strong>
                  <span className="muted"> · {request.symptoms.length > 80 ? `${request.symptoms.slice(0, 80)}…` : request.symptoms}</span>
                </div>
                <span className="muted" style={{ fontSize: '0.8rem' }}>{request.status}</span>
              </div>
            ))}
            <Link to="/consultations" style={{ fontSize: '0.85rem' }}>View all consultation requests →</Link>
          </div>
        )}
      </div>
    </>
  );
}

export default VeterinarianDashboard;
