import { useEffect, useMemo, useState } from 'react';
import { Loader2, Receipt, RefreshCw, X } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { useAuth } from '../auth/AuthContext';
import { getQuotations, markBillPaid } from '../../services/billingService';
import type { Quotation } from '../../types/domain';
import { messageFrom } from '../../utils/errors';
import { formatDate, formatLkr } from '../../utils/format';

const paymentTone = (status?: string) => (status === 'Paid' ? 'success' : 'warning');

/**
 * Inventory Officer bills & payments — finalised quotations awaiting
 * collection plus already-paid bills.
 */
export function BillsPage() {
  const { hasRole } = useAuth();
  // Mirrors POST /quotations/{id}/mark-paid — InventoryOfficer + Admin only.
  const canMarkPaid = hasRole('InventoryOfficer') || hasRole('Administrator');

  const [bills, setBills] = useState<Quotation[]>([]);
  const [filter, setFilter] = useState('All');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [selected, setSelected] = useState<Quotation | null>(null);
  const [marking, setMarking] = useState(false);

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      setBills(await getQuotations());
    } catch (err) {
      setBills([]);
      setError(messageFrom(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void load(); }, []);

  useEffect(() => {
    if (!success) return;
    const t = setTimeout(() => setSuccess(''), 4000);
    return () => clearTimeout(t);
  }, [success]);

  const visible = useMemo(
    () => bills.filter((b) => {
      if (filter === 'Pending') return (b.paymentStatus ?? 'Pending') === 'Pending';
      if (filter === 'Paid') return b.paymentStatus === 'Paid';
      return true;
    }),
    [bills, filter],
  );

  const handleMarkPaid = async () => {
    if (!selected) return;
    setMarking(true);
    setError('');
    try {
      const updated = await markBillPaid(selected.id);
      setBills((current) => current.map((b) => (b.id === updated.id ? updated : b)));
      setSelected(updated);
      setSuccess(`${updated.invoiceNumber ?? 'Bill'} marked as paid.`);
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setMarking(false);
    }
  };

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">INVENTORY</span>
          <h2>Bills &amp; Payments</h2>
          <p>Finalised bills generated from examinations and issued medicines.</p>
        </div>
        <div className="heading-actions" style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <Button variant="secondary" onClick={() => void load()} icon={<RefreshCw size={14} />}>Refresh</Button>
          <select value={filter} onChange={(e) => setFilter(e.target.value)} aria-label="Filter by payment status">
            {['All', 'Pending', 'Paid'].map((s) => (
              <option key={s} value={s}>{s === 'All' ? 'All bills' : s}</option>
            ))}
          </select>
        </div>
      </div>

      {error && <div className="form-error" role="alert" style={{ marginBottom: '16px' }}><strong>Error:</strong> {error}</div>}
      {success && (
        <div className="info-strip" style={{ marginBottom: '16px', borderColor: 'var(--success)', background: 'var(--success-soft)', color: 'var(--success)' }}>
          <strong>Success:</strong> <span>{success}</span>
        </div>
      )}

      {loading ? (
        <div className="empty-state"><Loader2 className="spinner" size={30} /><strong>Loading bills…</strong></div>
      ) : visible.length === 0 ? (
        <div className="empty-state"><Receipt size={28} /><strong>No bills found.</strong></div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr><th>Invoice</th><th>Date</th><th>Pet</th><th>Owner</th><th>Veterinarian</th><th>Vet charge</th><th>Medicine</th><th>Total</th><th>Payment</th><th /></tr>
            </thead>
            <tbody>
              {visible.map((bill) => (
                <tr key={bill.id}>
                  <td><strong>{bill.invoiceNumber ?? bill.id}</strong></td>
                  <td>{bill.appointmentDate ? formatDate(bill.appointmentDate) : '—'}</td>
                  <td>{bill.petName}</td>
                  <td>{bill.ownerName}</td>
                  <td>{bill.veterinarianName}</td>
                  <td>{formatLkr(bill.veterinarianChargeTotal ?? 0)}</td>
                  <td>{formatLkr(bill.medicineTotal ?? 0)}</td>
                  <td><strong>{formatLkr(bill.total ?? 0)}</strong></td>
                  <td><Badge tone={paymentTone(bill.paymentStatus)}>{bill.paymentStatus ?? 'Pending'}</Badge></td>
                  <td style={{ textAlign: 'right' }}>
                    <Button variant="secondary" onClick={() => setSelected(bill)}>Details</Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {selected && (
        <div className="modal-backdrop" role="presentation">
          <div className="modal" role="dialog" aria-modal="true" aria-label={`Bill ${selected.invoiceNumber ?? selected.id}`}>
            <div className="modal-header">
              <div><div className="eyebrow">PetCare AI</div><h3>Bill {selected.invoiceNumber ?? selected.id}</h3></div>
              <Button variant="ghost" aria-label="Close" onClick={() => setSelected(null)} icon={<X size={18} />} />
            </div>
            <div style={{ padding: '0 4px 8px' }}>
              <p className="muted" style={{ fontSize: '0.85rem' }}>
                {selected.petName} · {selected.ownerName} · {selected.veterinarianName} ·{' '}
                {selected.appointmentDate ? formatDate(selected.appointmentDate) : '—'}{' '}
                <Badge tone={paymentTone(selected.paymentStatus)}>
                  {selected.paymentStatus === 'Paid'
                    ? `Paid${selected.paidAt ? ` · ${formatDate(selected.paidAt.slice(0, 10))}` : ''}`
                    : 'Pending'}
                </Badge>
              </p>

              <div className="table-wrap" style={{ marginTop: '8px' }}>
                <table>
                  <thead><tr><th>Item</th><th>Category</th><th>Qty</th><th>Unit price</th><th>Total</th></tr></thead>
                  <tbody>
                    {selected.items.map((item) => (
                      <tr key={item.id}>
                        <td>{item.description}</td>
                        <td>{item.category}</td>
                        <td>{item.quantity}</td>
                        <td>{formatLkr(item.unitPrice)}</td>
                        <td>{formatLkr(item.quantity * item.unitPrice)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div style={{ display: 'flex', gap: '1.5rem', flexWrap: 'wrap', marginTop: '12px', fontSize: '0.85rem' }}>
                <span><strong>Vet charge:</strong> {formatLkr(selected.veterinarianChargeTotal ?? 0)}</span>
                <span><strong>Medicines:</strong> {formatLkr(selected.medicineTotal ?? 0)}</span>
                <span><strong>Total:</strong> {formatLkr(selected.total ?? 0)}</span>
              </div>

              {canMarkPaid && selected.status === 'Finalised' && (selected.paymentStatus ?? 'Pending') === 'Pending' && (
                <Button style={{ marginTop: '14px' }} onClick={() => void handleMarkPaid()} disabled={marking}>
                  {marking ? 'Marking…' : 'Mark as Paid'}
                </Button>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default BillsPage;
