import { useEffect, useMemo, useState } from 'react';
import { Loader2, RefreshCw, Stethoscope } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import {
  getVeterinarianHistory,
  getVeterinarians,
  type ManagerVeterinarian,
  type VeterinarianHistory,
} from '../../services/managerService';
import { messageFrom } from '../../utils/errors';
import { formatDate, formatLkr } from '../../utils/format';

const requestTone: Record<string, 'warning' | 'success' | 'danger' | 'neutral'> = {
  Pending: 'warning',
  Issued: 'success',
  Unavailable: 'danger',
};

const paymentTone = (status?: string) => (status === 'Paid' ? 'success' : 'warning');

const formatDateTime = (value?: string | null) =>
  value ? formatDate(value.slice(0, 10)) : '—';

const formatTime = (value?: string | null) => {
  if (!value) return '—';
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return '—';
  return parsed.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
};

const sectionCard = {
  backgroundColor: '#ffffff',
  padding: '1.5rem',
  borderRadius: '0.75rem',
  border: '1px solid #e5e7eb',
  marginBottom: '1.25rem',
} as const;

/**
 * ClinicManager veterinarian work history — appointments, examinations,
 * prescriptions/medicine requests and bills for one veterinarian.
 */
export function VeterinarianHistoryPage() {
  const [veterinarians, setVeterinarians] = useState<ManagerVeterinarian[]>([]);
  const [veterinarianId, setVeterinarianId] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [history, setHistory] = useState<VeterinarianHistory | null>(null);
  const [loadingVets, setLoadingVets] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    getVeterinarians()
      .then((vets) => {
        if (!active) return;
        setVeterinarians(vets);
      })
      .catch((err) => {
        if (!active) return;
        setError(messageFrom(err));
      })
      .finally(() => {
        if (active) setLoadingVets(false);
      });
    return () => {
      active = false;
    };
  }, []);

  const loadHistory = async (id: string) => {
    if (!id) {
      setHistory(null);
      return;
    }
    setLoading(true);
    setError('');
    try {
      setHistory(await getVeterinarianHistory(id, {
        from: from || undefined,
        to: to || undefined,
      }));
    } catch (err) {
      setHistory(null);
      setError(messageFrom(err));
    } finally {
      setLoading(false);
    }
  };

  const vet = useMemo(
    () => veterinarians.find((v) => v.id === veterinarianId),
    [veterinarians, veterinarianId],
  );

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">CLINIC MANAGEMENT</span>
          <h2>Veterinarian History</h2>
          <p>Appointments, examinations, medicine requests and billing for one veterinarian.</p>
        </div>
        <div className="heading-actions" style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <select
            aria-label="Veterinarian"
            value={veterinarianId}
            onChange={(event) => setVeterinarianId(event.target.value)}
            disabled={loadingVets}
          >
            <option value="">Select a veterinarian…</option>
            {veterinarians.map((v) => (
              <option key={v.id} value={v.id}>{v.name} — {v.specialisation}</option>
            ))}
          </select>
          <input aria-label="From date" type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
          <input aria-label="To date" type="date" value={to} onChange={(event) => setTo(event.target.value)} />
          <Button
            variant="primary"
            disabled={!veterinarianId || loading}
            onClick={() => void loadHistory(veterinarianId)}
            icon={<RefreshCw size={14} />}
          >
            Load history
          </Button>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: '16px' }}><strong>Error:</strong> {error}</div>}

      {loading ? (
        <div className="empty-state"><Loader2 className="spinner" size={30} /><strong>Loading history…</strong></div>
      ) : !history ? (
        <div className="empty-state"><Stethoscope size={28} /><strong>Select a veterinarian to view their work history.</strong></div>
      ) : (
        <>
          {/* Appointments */}
          <div style={sectionCard}>
            <h3 style={{ marginTop: 0 }}>Appointments {vet ? `— ${vet.name}` : ''}</h3>
            <p className="muted" style={{ fontSize: '0.85rem' }}>
              {history.appointments.completedCount} completed · {history.appointments.upcomingCount} upcoming · {history.appointments.cancelledCount} cancelled
            </p>
            {history.appointments.items.length === 0 ? (
              <p className="muted">No appointments in this range.</p>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr><th>Date</th><th>Time</th><th>Pet</th><th>Owner</th><th>Type</th><th>Status</th></tr>
                  </thead>
                  <tbody>
                    {history.appointments.items.map((a) => (
                      <tr key={a.id}>
                        <td>{formatDateTime(a.scheduledStart)}</td>
                        <td>{formatTime(a.scheduledStart)} – {formatTime(a.scheduledEnd)}</td>
                        <td>{a.petName ?? a.petId}</td>
                        <td>{a.ownerName ?? '—'}</td>
                        <td>{a.type === 'FollowUp' ? <Badge tone="info">Follow-up</Badge> : 'Initial'}</td>
                        <td>{a.status}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Examinations */}
          <div style={sectionCard}>
            <h3 style={{ marginTop: 0 }}>Examinations</h3>
            <p className="muted" style={{ fontSize: '0.85rem' }}>
              {history.examinations.total} total · {history.examinations.initialCount} initial · {history.examinations.followUpCount} follow-up
            </p>
            {history.examinations.items.length === 0 ? (
              <p className="muted">No examinations in this range.</p>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr><th>Date</th><th>Pet</th><th>Symptoms</th><th>Fee</th></tr>
                  </thead>
                  <tbody>
                    {history.examinations.items.map((e) => (
                      <tr key={e.id}>
                        <td>{formatDateTime(e.examinationDate)}</td>
                        <td>{e.petId}</td>
                        <td>{e.symptoms.length > 60 ? `${e.symptoms.slice(0, 60)}…` : e.symptoms}</td>
                        <td>{formatLkr(e.veterinarianCharge ?? 0)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Prescriptions / medicine requests */}
          <div style={sectionCard}>
            <h3 style={{ marginTop: 0 }}>Prescriptions &amp; medicine requests</h3>
            <p className="muted" style={{ fontSize: '0.85rem' }}>
              {history.prescriptions.total} prescriptions · {history.medicineRequests.pending} pending · {history.medicineRequests.issued} issued · {history.medicineRequests.unavailable} unavailable
            </p>
            {history.prescriptions.items.length === 0 ? (
              <p className="muted">No prescriptions in this range.</p>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr><th>Date</th><th>Pet</th><th>Medicine</th><th>Qty</th><th>Request status</th></tr>
                  </thead>
                  <tbody>
                    {history.prescriptions.items.map((p) => (
                      <tr key={p.id}>
                        <td>{formatDateTime(p.createdAt)}</td>
                        <td>{p.petName ?? p.petId ?? '—'}</td>
                        <td>{p.medicineName ?? '—'}</td>
                        <td>{p.quantity ?? 1}</td>
                        <td><Badge tone={requestTone[p.requestStatus ?? ''] ?? 'neutral'}>{p.requestStatus ?? 'Pending'}</Badge></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Bills */}
          <div style={sectionCard}>
            <h3 style={{ marginTop: 0 }}>Bills</h3>
            <p className="muted" style={{ fontSize: '0.85rem' }}>
              {history.bills.total} bills · {history.bills.paidCount} paid · {history.bills.pendingCount} pending
              {' · '}vet charges {formatLkr(history.bills.vetChargeTotal)} · medicine {formatLkr(history.bills.medicineTotal)} · grand total {formatLkr(history.bills.grandTotal)}
            </p>
            {history.bills.items.length === 0 ? (
              <p className="muted">No bills in this range.</p>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr><th>Invoice</th><th>Pet</th><th>Owner</th><th>Vet charge</th><th>Medicine</th><th>Total</th><th>Payment</th></tr>
                  </thead>
                  <tbody>
                    {history.bills.items.map((b) => (
                      <tr key={b.id}>
                        <td><strong>{b.invoiceNumber}</strong></td>
                        <td>{b.petName ?? '—'}</td>
                        <td>{b.ownerName ?? '—'}</td>
                        <td>{formatLkr(b.veterinarianChargeTotal)}</td>
                        <td>{formatLkr(b.medicineTotal)}</td>
                        <td>{formatLkr(b.total)}</td>
                        <td><Badge tone={paymentTone(b.paymentStatus)}>{b.paymentStatus}</Badge></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}

export default VeterinarianHistoryPage;
