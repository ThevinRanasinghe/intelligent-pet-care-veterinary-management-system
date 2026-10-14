import { useEffect, useMemo, useState } from 'react';
import { ClipboardList, Loader2, RefreshCw } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Modal } from '../../components/ui/Modal';
import { useAuth } from '../auth/AuthContext';
import {
  getInventoryPlan,
  getMedicineRequests,
  issueMedicineRequest,
  markMedicineRequestUnavailable,
  type InventoryPlanApi,
  type MedicineRequest,
} from '../../services/treatmentService';
import { messageFrom } from '../../utils/errors';
import { formatDate, formatLkr } from '../../utils/format';

const requestTone: Record<string, 'warning' | 'success' | 'danger' | 'neutral'> = {
  Pending: 'warning',
  Issued: 'success',
  Unavailable: 'danger',
};

/**
 * Inventory Officer medicine-request queue — prescriptions that need stock
 * issued (Reserve + Dispense) or marked unavailable with a reason.
 */
export function MedicineRequestsPage() {
  const { hasRole } = useAuth();
  // Mirrors POST /prescriptions/{id}/issue and /unavailable — IO + Admin only.
  const canProcess = hasRole('InventoryOfficer') || hasRole('Administrator');

  const [requests, setRequests] = useState<MedicineRequest[]>([]);
  const [statusFilter, setStatusFilter] = useState('Pending');
  const [loading, setLoading] = useState(true);
  const [actioningId, setActioningId] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const [unavailableTarget, setUnavailableTarget] = useState<MedicineRequest | null>(null);
  const [unavailableReason, setUnavailableReason] = useState('');
  const [unavailableError, setUnavailableError] = useState('');

  // Advisory AI plans keyed by treatmentRecordId (the request group key) —
  // keyed state guarantees one request's analysis never shows on another's.
  const [plans, setPlans] = useState<Record<string, 'loading' | InventoryPlanApi>>({});

  const load = async (status = statusFilter) => {
    setLoading(true);
    setError('');
    try {
      setRequests(await getMedicineRequests(status === 'All' ? undefined : (status as 'Pending' | 'Issued' | 'Unavailable')));
    } catch (err) {
      setRequests([]);
      setError(messageFrom(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void load('Pending'); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!success) return;
    const t = setTimeout(() => setSuccess(''), 4000);
    return () => clearTimeout(t);
  }, [success]);

  const changeStatus = (status: string) => {
    setStatusFilter(status);
    void load(status);
  };

  const visibleRows = useMemo(() => requests, [requests]);

  // One vet medicine request = the prescription items sharing a treatment
  // record; each item can be issued or marked unavailable independently.
  const requestGroups = useMemo(() => {
    const map = new Map<string, MedicineRequest[]>();
    for (const row of visibleRows) {
      const key = row.treatmentRecordId ?? row.id;
      map.set(key, [...(map.get(key) ?? []), row]);
    }
    return [...map.values()];
  }, [visibleRows]);

  const handleIssue = async (request: MedicineRequest) => {
    if (!window.confirm(`Issue ${request.quantity ?? 1} × ${request.medicineName ?? 'medicine'} to ${request.petName ?? 'the patient'}?`)) return;
    setActioningId(request.id);
    setError('');
    try {
      await issueMedicineRequest(request.id);
      setSuccess('Medicine issued and dispensed from stock.');
      await load();
    } catch (err) {
      // e.g. insufficient stock — the request stays Pending.
      setError(messageFrom(err));
    } finally {
      setActioningId(null);
    }
  };

  // Advisory only — displays the agent's validated plan; never issues,
  // reserves, or substitutes anything. Failures surface as "unavailable".
  const handleAnalyze = async (treatmentRecordId: string) => {
    setPlans((prev) => ({ ...prev, [treatmentRecordId]: 'loading' }));
    try {
      const plan = await getInventoryPlan(treatmentRecordId);
      setPlans((prev) => ({ ...prev, [treatmentRecordId]: plan }));
    } catch {
      setPlans((prev) => ({
        ...prev,
        [treatmentRecordId]: {
          source: 'unavailable',
          requestId: treatmentRecordId,
          alternativeMedicines: [],
          inventorySummary: {
            medicineFound: false, stockAvailable: false, sufficientQuantity: false,
            batchAvailable: false, notExpired: false, lowStock: false,
          },
          confidence: 'Low',
          planningNotes: 'AI inventory analysis is currently unavailable — process the medicine request normally.',
          disclaimer: 'AI-generated medicine and inventory recommendation — deterministic backend validation and authorized staff review are required before any inventory action.',
        },
      }));
    }
  };

  const handleMarkUnavailable = async () => {
    if (!unavailableTarget) return;
    if (!unavailableReason.trim()) {
      setUnavailableError('A reason is required.');
      return;
    }
    setActioningId(unavailableTarget.id);
    setUnavailableError('');
    try {
      await markMedicineRequestUnavailable(unavailableTarget.id, unavailableReason.trim());
      setUnavailableTarget(null);
      setUnavailableReason('');
      setSuccess('Marked unavailable — the veterinarian has been informed via the request status.');
      await load();
    } catch (err) {
      setUnavailableError(messageFrom(err));
    } finally {
      setActioningId(null);
    }
  };

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">INVENTORY</span>
          <h2>Medicine Requests</h2>
          <p>Pending prescriptions waiting to be issued from stock or marked unavailable.</p>
        </div>
        <div className="heading-actions" style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <Button variant="secondary" onClick={() => void load()} icon={<RefreshCw size={14} />}>Refresh</Button>
          <select value={statusFilter} onChange={(e) => changeStatus(e.target.value)} aria-label="Filter by status">
            {['Pending', 'Issued', 'Unavailable', 'All'].map((s) => (
              <option key={s} value={s}>{s === 'All' ? 'All statuses' : s}</option>
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
        <div className="empty-state"><Loader2 className="spinner" size={30} /><strong>Loading medicine requests…</strong></div>
      ) : visibleRows.length === 0 ? (
        <div className="empty-state"><ClipboardList size={28} /><strong>No {statusFilter === 'All' ? '' : statusFilter.toLowerCase() + ' '}medicine requests.</strong></div>
      ) : (
        <div style={{ display: 'grid', gap: '1rem' }}>
          {requestGroups.map((items) => {
            const head = items[0];
            const groupKey = head.treatmentRecordId ?? head.id;
            const plan = plans[groupKey];
            const planReady = plan !== undefined && plan !== 'loading' && plan.source === 'agentic-ai';
            return (
              <Card key={groupKey}>
                <div className="card-header">
                  <div>
                    <div className="eyebrow">{formatDate(head.createdAt.slice(0, 10))} · {items.length} medicine{items.length === 1 ? '' : 's'}</div>
                    <h3>{head.petName ?? head.petId ?? '—'}</h3>
                    <p className="muted">
                      Vet: {head.veterinarianName ?? '—'} · Owner: {head.ownerName ?? '—'} · Vet charge {formatLkr(head.veterinarianCharge ?? 0)}
                    </p>
                  </div>
                  {canProcess && head.treatmentRecordId && (
                    <Button
                      variant="secondary"
                      disabled={plan === 'loading'}
                      onClick={() => void handleAnalyze(head.treatmentRecordId!)}
                    >
                      {plan === 'loading' ? 'Analysing…' : 'AI Inventory Analysis'}
                    </Button>
                  )}
                </div>
                <div style={{ display: 'grid', gap: '0.6rem' }}>
                  {items.map((request, index) => (
                    <div key={request.id} className="info-strip" style={{ alignItems: 'center', gap: '0.75rem' }}>
                      <div style={{ flex: 1 }}>
                        <strong>{index + 1}. {request.medicineName ?? '—'}</strong>
                        <span className="muted"> × {request.quantity ?? 1} — {request.dosage}{request.frequency ? ` · ${request.frequency}` : ''} · {request.durationDays}d</span>
                        <div>
                          <Badge tone={requestTone[request.requestStatus ?? 'Pending'] ?? 'warning'}>
                            {request.requestStatus ?? 'Pending'}
                          </Badge>
                          {request.unavailableReason && (
                            <span className="muted" style={{ fontSize: '0.75rem' }}> {request.unavailableReason}</span>
                          )}
                        </div>
                      </div>
                      {(request.requestStatus ?? 'Pending') === 'Pending' && canProcess && (
                        <div style={{ display: 'inline-flex', gap: '0.4rem', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                          <Button
                            variant="primary"
                            disabled={actioningId === request.id}
                            onClick={() => void handleIssue(request)}
                          >
                            Issue
                          </Button>
                          <Button
                            variant="secondary"
                            disabled={actioningId === request.id}
                            onClick={() => { setUnavailableTarget(request); setUnavailableReason(''); setUnavailableError(''); }}
                          >
                            Mark Unavailable
                          </Button>
                        </div>
                      )}
                    </div>
                  ))}
                </div>

                {plan !== undefined && plan !== 'loading' && (
                  <div className="info-strip" style={{ display: 'block', marginTop: '0.75rem' }} data-testid={`ai-plan-${groupKey}`}>
                    <div className="eyebrow" style={{ marginBottom: '6px' }}>AI INVENTORY ANALYSIS — ADVISORY ONLY</div>
                    {planReady ? (
                      <>
                        {plan.medicineRecommendation && (
                          <p style={{ margin: '0 0 6px' }}>
                            <strong>{plan.medicineRecommendation.medicineName || 'Recommended medicine'}</strong>
                            <span className="muted">
                              {' '}— required {plan.medicineRecommendation.requiredQuantity}, available {plan.medicineRecommendation.availableQuantity},{' '}
                              {plan.medicineRecommendation.sufficientStock ? 'sufficient stock' : 'insufficient stock'}
                            </span>
                            {plan.medicineRecommendation.reason && (
                              <span className="muted"> · {plan.medicineRecommendation.reason}</span>
                            )}
                          </p>
                        )}
                        {plan.recommendedBatch && (
                          <p style={{ margin: '0 0 6px' }}>
                            Suggested batch: <strong>{plan.recommendedBatch.batchNumber}</strong>
                            <span className="muted">
                              {' '}— {plan.recommendedBatch.quantityAvailable} available, expires {plan.recommendedBatch.expiryDate} ({plan.recommendedBatch.expiryStatus})
                            </span>
                          </p>
                        )}
                        <p className="muted" style={{ margin: '0 0 6px', fontSize: '0.8rem' }}>
                          Checks: medicine found {plan.inventorySummary.medicineFound ? 'yes' : 'no'}
                          {' · '}stock {plan.inventorySummary.stockAvailable ? 'yes' : 'no'}
                          {' · '}sufficient qty {plan.inventorySummary.sufficientQuantity ? 'yes' : 'no'}
                          {' · '}batch {plan.inventorySummary.batchAvailable ? 'yes' : 'no'}
                          {' · '}not expired {plan.inventorySummary.notExpired ? 'yes' : 'no'}
                          {' · '}low stock {plan.inventorySummary.lowStock ? 'yes' : 'no'}
                          {' · '}confidence {plan.confidence}
                        </p>
                        {plan.alternativeMedicines.length > 0 && (
                          <p className="muted" style={{ margin: '0 0 6px', fontSize: '0.8rem' }}>
                            Alternatives (informational only — veterinarian approval required before any substitution):
                            {plan.alternativeMedicines.map((alt, i) => (
                              <span key={i}> {alt.medicineName ?? alt.medicineId ?? 'Alternative'}{alt.reason ? ` — ${alt.reason}` : ''}{i < plan.alternativeMedicines.length - 1 ? ';' : ''}</span>
                            ))}
                          </p>
                        )}
                        {plan.planningNotes && (
                          <p className="muted" style={{ margin: '0 0 6px', fontSize: '0.8rem' }}>{plan.planningNotes}</p>
                        )}
                      </>
                    ) : (
                      <p className="muted" style={{ margin: '0 0 6px' }}>
                        {plan.planningNotes || 'AI inventory analysis is currently unavailable — process the medicine request normally.'}
                      </p>
                    )}
                    <p className="muted" style={{ margin: 0, fontSize: '0.75rem', fontStyle: 'italic' }}>{plan.disclaimer}</p>
                  </div>
                )}
              </Card>
            );
          })}
        </div>
      )}

      {unavailableTarget && (
        <Modal
          title={`Mark unavailable — ${unavailableTarget.medicineName ?? 'medicine'}`}
          onClose={() => setUnavailableTarget(null)}
        >
          <div style={{ padding: '0 4px 8px' }}>
            <p className="muted" style={{ fontSize: '0.85rem' }}>
              The prescription stays on record but will not be issued or billed.
            </p>
            <label htmlFor="unavailable-reason" style={{ display: 'block', marginBottom: '5px', fontSize: '11px', fontWeight: 700 }}>REASON</label>
            <textarea
              id="unavailable-reason"
              aria-label="Unavailable reason"
              rows={3}
              value={unavailableReason}
              onChange={(e) => setUnavailableReason(e.target.value)}
              style={{ width: '100%', padding: '9px 10px', border: '1px solid #d8d8d2', borderRadius: '8px', fontSize: '12px', boxSizing: 'border-box' }}
            />
            {unavailableError && <div className="form-error" role="alert" style={{ marginTop: '8px' }}>{unavailableError}</div>}
            <div style={{ display: 'flex', gap: '0.5rem', marginTop: '12px' }}>
              <Button onClick={() => void handleMarkUnavailable()} disabled={actioningId === unavailableTarget.id}>
                Confirm unavailable
              </Button>
              <Button variant="secondary" onClick={() => setUnavailableTarget(null)}>Cancel</Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}

export default MedicineRequestsPage;
