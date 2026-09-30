import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getMyAppointments } from '../../services/schedulingService';
import {
  getAllPrescriptions,
  getAllTreatmentRecords,
  getExaminations,
} from '../../services/treatmentService';

const ACTIVE_TREATMENT_STATUSES = new Set(['Planned', 'InProgress']);

const cardStyle = {
  backgroundColor: '#ffffff',
  padding: '1.5rem',
  borderRadius: '0.75rem',
  border: '1px solid #e5e7eb',
} as const;

/**
 * Veterinarian dashboard — clinical workload overview built from real
 * appointment, examination, treatment, and prescription data.
 * Renders inside the shared DashboardLayout shell.
 */
export function VeterinarianDashboard() {
  const [upcomingAppointments, setUpcomingAppointments] = useState(0);
  const [examinationCount, setExaminationCount] = useState(0);
  const [activeTreatments, setActiveTreatments] = useState(0);
  const [prescriptionCount, setPrescriptionCount] = useState(0);
  const [todayAppointments, setTodayAppointments] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        const today = new Date().toISOString().slice(0, 10);
        const [examinations, treatments, prescriptions, myAppointments] = await Promise.all([
          getExaminations(),
          getAllTreatmentRecords(),
          getAllPrescriptions(),
          getMyAppointments().catch(() => []),
        ]);
        if (!active) return;
        setUpcomingAppointments(
          myAppointments.filter(
            (a) => a.scheduledStart.slice(0, 10) >= today && a.status === 'Confirmed',
          ).length,
        );
        setExaminationCount(examinations.length);
        setActiveTreatments(treatments.filter((t) => ACTIVE_TREATMENT_STATUSES.has(t.status)).length);
        setPrescriptionCount(prescriptions.length);
        setTodayAppointments(
          myAppointments.filter(
            (a) => a.scheduledStart.slice(0, 10) === today && a.status !== 'Cancelled',
          ).length,
        );
      } catch {
        if (active) setError('Could not load dashboard data. Please try again shortly.');
      } finally {
        if (active) setLoading(false);
      }
    }

    void load();
    return () => { active = false; };
  }, []);

  return (
    <>
      {error && <div className="notice error" role="alert">{error}</div>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Today&apos;s Appointments</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>{loading ? '…' : todayAppointments}</div>
          <Link to="/vet/appointments" style={{ fontSize: '0.8rem', color: '#0f6b5f', fontWeight: 700 }}>View my appointments</Link>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Upcoming Appointments</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>{loading ? '…' : upcomingAppointments}</div>
          <Link to="/vet/appointments" style={{ fontSize: '0.8rem', color: '#0f6b5f', fontWeight: 700 }}>View my appointments</Link>
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
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>My Work Queue</h3>
        <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
          Your assigned appointments are the vet&apos;s work queue — open an
          appointment to record the examination, diagnosis, treatment,
          prescriptions, and follow-up requests.
        </p>
        <Link to="/vet/appointments" style={{ fontSize: '0.85rem' }}>Go to My Appointments →</Link>
      </div>
    </>
  );
}

export default VeterinarianDashboard;
