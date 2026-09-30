/**
 * Pet Owner landing/dashboard — pet owner's view of their pets, consultations,
 * upcoming appointments and bills.
 * Renders inside the shared DashboardLayout shell.
 */
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Badge } from '../../components/ui/Badge';
import { Modal } from '../../components/ui/Modal';
import { getMyAppointments, type AppointmentResponse } from '../../services/schedulingService';
import { getMyBills } from '../../services/billingService';
import { OwnerBillDetail } from '../billing/OwnerBillDetail';
import type { Quotation } from '../../types/domain';
import { formatDate, formatLkr } from '../../utils/format';

const cardStyle = {
  backgroundColor: '#ffffff',
  padding: '1.5rem',
  borderRadius: '0.75rem',
  border: '1px solid #e5e7eb',
} as const;

const paymentTone = (status?: string): 'success' | 'warning' => (status === 'Paid' ? 'success' : 'warning');

const formatTimeOfDay = (iso: string) => {
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) return '—';
  return parsed.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
};

export function PetOwnerDashboard() {
  const [appointments, setAppointments] = useState<AppointmentResponse[]>([]);
  const [bills, setBills] = useState<Quotation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selectedBill, setSelectedBill] = useState<Quotation | null>(null);

  useEffect(() => {
    let active = true;
    async function load() {
      try {
        const [appts, myBills] = await Promise.all([
          getMyAppointments(),
          getMyBills(),
        ]);
        if (!active) return;
        setAppointments(appts);
        setBills(myBills);
      } catch {
        if (active) setError('Could not load your appointments and bills. Please try again shortly.');
      } finally {
        if (active) setLoading(false);
      }
    }
    void load();
    return () => { active = false; };
  }, []);

  const upcoming = appointments
    .filter((a) => a.status !== 'Completed' && a.status !== 'Cancelled')
    .sort((a, b) => a.scheduledStart.localeCompare(b.scheduledStart))
    .slice(0, 5);

  return (
    <>
      {error && <div className="notice error" role="alert">{error}</div>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>My Pets</div>
          <Link to="/pets" style={{ fontSize: '0.8rem', color: '#0f6b5f', fontWeight: 700 }}>Manage my pets</Link>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Active Consultations</div>
          <Link to="/consultations" style={{ fontSize: '0.8rem', color: '#0f6b5f', fontWeight: 700 }}>View my consultations</Link>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Treatment History</div>
          <Link to="/care-history" style={{ fontSize: '0.8rem', color: '#0f6b5f', fontWeight: 700 }}>View care history</Link>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        {/* My Appointments */}
        <div style={cardStyle}>
          <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginTop: 0, marginBottom: '0.75rem' }}>My Appointments</h3>
          {loading ? (
            <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>Loading…</p>
          ) : upcoming.length === 0 ? (
            <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>No upcoming appointments.</p>
          ) : (
            <div style={{ display: 'grid', gap: '0.6rem' }}>
              {upcoming.map((a) => (
                <div key={a.id} className="info-strip" style={{ justifyContent: 'space-between', gap: '0.75rem', flexWrap: 'wrap' }}>
                  <div>
                    <strong>{a.petName ?? a.petId}</strong>
                    <span className="muted"> · {a.veterinarianName ?? 'Veterinarian'}{a.type === 'FollowUp' ? ' · Follow-up' : ''}</span>
                  </div>
                  <span className="muted" style={{ fontSize: '0.8rem' }}>
                    {formatDate(a.scheduledStart.slice(0, 10))} · {formatTimeOfDay(a.scheduledStart)} · {a.status}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* My Bills */}
        <div style={cardStyle}>
          <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginTop: 0, marginBottom: '0.75rem' }}>My Bills</h3>
          {loading ? (
            <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>Loading…</p>
          ) : bills.length === 0 ? (
            <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>No bills yet.</p>
          ) : (
            <div style={{ display: 'grid', gap: '0.6rem' }}>
              {bills.map((bill) => (
                <button
                  key={bill.id}
                  type="button"
                  className="info-strip"
                  onClick={() => setSelectedBill(bill)}
                  style={{ justifyContent: 'space-between', gap: '0.75rem', flexWrap: 'wrap', cursor: 'pointer', textAlign: 'left' }}
                >
                  <div>
                    <strong>{bill.invoiceNumber ?? bill.id}</strong>
                    <span className="muted"> · {bill.petName}{bill.appointmentDate ? ` · ${formatDate(bill.appointmentDate)}` : ''}</span>
                  </div>
                  <span style={{ fontSize: '0.8rem', display: 'inline-flex', gap: '0.5rem', alignItems: 'center' }}>
                    <strong>{formatLkr(bill.total ?? 0)}</strong>
                    <Badge tone={paymentTone(bill.paymentStatus)}>{bill.paymentStatus ?? 'Pending'}</Badge>
                  </span>
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Welcome to Beacon Pet Health</h3>
        <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
          Register and manage your pets, submit consultation requests, track their progress, and review care history. Use your profile card to update your account or change your password.
        </p>
      </div>

      {selectedBill && (
        <Modal title={`Bill ${selectedBill.invoiceNumber ?? ''}`} onClose={() => setSelectedBill(null)}>
          <div style={{ padding: '0 4px 8px' }}>
            <OwnerBillDetail bill={selectedBill} onClose={() => setSelectedBill(null)} />
          </div>
        </Modal>
      )}
    </>
  );
}

export default PetOwnerDashboard;
