import { useEffect, useMemo, useState } from 'react';
import { ClipboardList, PawPrint, RefreshCw } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { useAuth } from '../auth/AuthContext';
import {
  cancelReservation,
  dispenseReservation,
  getReservations,
} from '../../services/inventoryService';
import type { MedicineReservation } from '../../types/domain';
import { messageFrom } from '../../utils/errors';
import { formatDate } from '../../utils/format';

const statusTone: Record<string, 'success' | 'warning' | 'neutral' | 'danger' | 'info'> = {
  Reserved: 'warning',
  Dispensed: 'success',
  Cancelled: 'neutral',
  Expired: 'danger',
};

/** Reservations — every medicine reservation with cancel/dispense actions. */
export function ReservationsPage() {
  const { hasRole } = useAuth();
  // Mirrors backend authorization: cancel = Vet/Officer/Admin, dispense = Officer/Admin.
  const canCancel = hasRole('Veterinarian') || hasRole('InventoryOfficer') || hasRole('Administrator');
  const canDispense = hasRole('InventoryOfficer') || hasRole('Administrator');

  const [reservations, setReservations] = useState<MedicineReservation[]>([]);
  const [statusFilter, setStatusFilter] = useState('All');
  const [loading, setLoading] = useState(true);
  const [actioningId, setActioningId] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      setReservations(await getReservations());
    } catch (err) {
      setError(messageFrom(err));
      setReservations([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void load(); }, []);

  useEffect(() => {
    if (!success) return;
    const timer = setTimeout(() => setSuccess(''), 4000);
    return () => clearTimeout(timer);
  }, [success]);

  const visibleRows = useMemo(
    () => (statusFilter === 'All' ? reservations : reservations.filter((r) => r.status === statusFilter)),
    [reservations, statusFilter],
  );

  const runAction = async (id: string, action: 'cancel' | 'dispense') => {
    setActioningId(id);
    setError('');
    try {
      if (action === 'cancel') {
        await cancelReservation(id);
        setSuccess('Reservation cancelled and stock restored.');
      } else {
        await dispenseReservation(id);
        setSuccess('Reservation dispensed via FEFO batch allocation.');
      }
      await load();
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setActioningId(null);
    }
  };

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">INVENTORY</span>
          <h2>Medicine Reservations</h2>
          <p>Stock reservations created by clinical staff — cancel or dispense them here.</p>
        </div>
        <div className="heading-actions" style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <Button variant="secondary" onClick={() => void load()} icon={<RefreshCw size={14} />}>Refresh</Button>
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)} aria-label="Filter by status">
            {['All', 'Reserved', 'Dispensed', 'Cancelled', 'Expired'].map((s) => (
              <option key={s} value={s}>{s === 'All' ? 'All statuses' : s}</option>
            ))}
          </select>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: '16px' }}><strong>Error:</strong> {error}</div>}
      {success && (
        <div className="info-strip" style={{ marginBottom: '16px', borderColor: 'var(--success)', background: 'var(--success-soft)', color: 'var(--success)' }}>
          <strong>Success:</strong> <span>{success}</span>
        </div>
      )}

      {loading ? (
        <div className="empty-state"><strong>Loading reservations…</strong></div>
      ) : visibleRows.length === 0 ? (
        <div className="empty-state"><ClipboardList size={28} /><strong>No reservations found.</strong></div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Medicine</th>
                <th>Quantity</th>
                <th>Status</th>
                <th>Reference</th>
                <th>Created</th>
                <th style={{ textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {visibleRows.map((reservation) => (
                <tr key={reservation.id}>
                  <td><strong>{reservation.medicineName ?? `Medicine #${reservation.medicineId.slice(0, 8)}…`}</strong></td>
                  <td>{reservation.quantity}</td>
                  <td><Badge tone={statusTone[reservation.status] ?? 'neutral'}>{reservation.status}</Badge></td>
                  <td>{reservation.referenceType ?? '—'}</td>
                  <td>{reservation.createdAt ? formatDate(reservation.createdAt.slice(0, 10)) : '—'}</td>
                  <td style={{ textAlign: 'right' }}>
                    {reservation.status === 'Reserved' && (canCancel || canDispense) ? (
                      <div style={{ display: 'inline-flex', gap: '0.4rem', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                        {canDispense && (
                          <Button
                            variant="primary"
                            disabled={actioningId === reservation.id}
                            onClick={() => void runAction(reservation.id, 'dispense')}
                          >
                            Dispense
                          </Button>
                        )}
                        {canCancel && (
                          <Button
                            variant="secondary"
                            disabled={actioningId === reservation.id}
                            onClick={() => void runAction(reservation.id, 'cancel')}
                          >
                            Cancel
                          </Button>
                        )}
                      </div>
                    ) : (
                      <span className="muted">—</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default ReservationsPage;
