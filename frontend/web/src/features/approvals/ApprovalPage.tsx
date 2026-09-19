import { useEffect, useMemo, useState } from 'react';
import { CheckCircle2, Clock3, FileCheck2, History, Loader2, RotateCcw, Search, ShieldAlert, XCircle } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Modal } from '../../components/ui/Modal';
import { approve, getApprovalHistory, getApprovalProposals, reject, requestRevision } from '../../services/approvalService';
import type { ApprovalHistoryResponse } from '../../services/approvalService';
import type { ApprovalProposal } from '../../types/domain';
import { messageFrom } from '../../utils/errors';
import { formatDate, formatLkr } from '../../utils/format';
import { useAuth } from '../auth/AuthContext';

const tone: Record<ApprovalProposal['status'], 'warning' | 'success' | 'danger' | 'info'> = { Pending: 'warning', Approved: 'success', Rejected: 'danger', RevisionRequested: 'info' };

export function ApprovalPage() {
  const { hasRole } = useAuth();
  const canDecide = hasRole('ClinicManager');
  const [proposals, setProposals] = useState<ApprovalProposal[]>([]);
  const [selected, setSelected] = useState<ApprovalProposal | null>(null);
  const [query, setQuery] = useState('');
  const [decision, setDecision] = useState<'Approved' | 'Rejected' | 'RevisionRequested' | null>(null);
  const [note, setNote] = useState('');
  const [history, setHistory] = useState<ApprovalHistoryResponse[]>([]);
  const [showHistory, setShowHistory] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const pendingCount = useMemo(() => proposals.filter((item) => item.status === 'Pending').length, [proposals]);
  const filtered = useMemo(() => proposals.filter((item) => `${item.id} ${item.petName} ${item.ownerName}`.toLowerCase().includes(query.toLowerCase())), [proposals, query]);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      setError('');
      try {
        const data = await getApprovalProposals();
        if (!cancelled) {
          setProposals(data);
          setSelected(data[0] ?? null);
        }
      } catch (err) {
        if (!cancelled) setError(messageFrom(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (!success) return;
    const t = setTimeout(() => setSuccess(''), 3000);
    return () => clearTimeout(t);
  }, [success]);

  const openHistory = async () => {
    if (!selected) return;
    try {
      const data = await getApprovalHistory(selected.id);
      setHistory(data);
      setShowHistory(true);
    } catch (err) {
      setError(messageFrom(err));
    }
  };

  const applyDecision = async () => {
    if (!selected || !decision) return;
    if (decision !== 'Approved' && !note.trim()) {
      setError('A reason is required for reject and revision requests');
      return;
    }
    setSaving(true);
    setError('');
    setSuccess('');
    try {
      let updated: ApprovalProposal;
      if (decision === 'Approved') updated = await approve(selected.id, note.trim() || undefined);
      else if (decision === 'Rejected') updated = await reject(selected.id, note.trim());
      else updated = await requestRevision(selected.id, note.trim());
      const data = await getApprovalProposals();
      setProposals(data);
      setSelected(data.find((p) => p.id === updated.id) ?? data[0] ?? null);
      setDecision(null);
      setNote('');
      setSuccess(`Decision recorded: ${updated.status}`);
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setSaving(false);
    }
  };

  const startDecision = (d: typeof decision) => { setDecision(d); setNote(''); };

  return <div className='page-wrap'>
    {error && <div className='error-banner'>{error}</div>}
    {success && <div className='success-banner'>{success}</div>}
    <div className='page-heading'><div><div className='eyebrow'>Component · Approval</div><h2>Approval center</h2><p>Review scheduling + billing proposals before any high-impact execution step.</p></div><div className='approval-head-count'><span>{pendingCount}</span><div><strong>Pending decisions</strong><small>Manager checkpoint</small></div></div></div>
    <div className='approval-layout'>
      <Card className='approval-list-card'><div className='card-header'><div><div className='eyebrow'>Human approval</div><h3>Proposal queue</h3></div><Badge tone='warning'>{pendingCount} pending</Badge></div><div className='search-input compact'><Search size={16}/><input value={query} onChange={(e)=>setQuery(e.target.value)} placeholder='Search request, pet or owner'/></div><div className='approval-list'>{loading ? <div className='empty-state'><Loader2 className='spinner' size={30}/><h3>Loading approvals...</h3></div> : (filtered.length === 0 ? <div className='empty-state'><h3>No pending approvals</h3></div> : filtered.map((proposal)=><button key={proposal.id} onClick={()=>setSelected(proposal)} className={`approval-list-item ${selected?.id === proposal.id ? 'selected' : ''}`}><div className='approval-item-main'><div className='queue-title'><strong>{proposal.petName}</strong><Badge tone={proposal.urgency==='Urgent'?'danger':'warning'}>{proposal.urgency}</Badge></div><p>{proposal.ownerName} · {proposal.proposedVet}</p><small><Clock3 size={12}/> {formatDate(proposal.submittedAt.slice(0,10))} · {proposal.proposedTime}</small></div><div className='approval-item-side'><strong>{formatLkr(proposal.quotationTotal)}</strong><Badge tone={tone[proposal.status]}>{proposal.status === 'RevisionRequested' ? 'Revision' : proposal.status}</Badge></div></button>))}</div></Card>
      <Card className='proposal-detail'>{selected ? <><div className='proposal-header'><div><div className='eyebrow'>{selected.workflowId} · {selected.id}</div><h3>{selected.petName} appointment proposal</h3><p>{selected.summary}</p></div><Badge tone={tone[selected.status]}>{selected.status}</Badge></div><div className='proposal-grid'><div className='detail-block'><span>Recommended veterinarian</span><strong>{selected.proposedVet}</strong><small>{selected.branch}</small></div><div className='detail-block'><span>Appointment</span><strong>{formatDate(selected.proposedDate)}</strong><small>{selected.proposedTime}</small></div><div className='detail-block'><span>Quotation</span><strong>{formatLkr(selected.quotationTotal)}</strong><small>Budget {formatLkr(selected.budget)}</small></div></div><div className='validation-section'><div className='section-title'><div><div className='eyebrow'>Deterministic validation</div><h4>Proposal checks</h4></div><FileCheck2 size={18}/></div><div className='validation-grid'>{selected.validationChecks.map((check)=><div key={check.key} className={`validation-check ${check.passed?'pass':'fail'}`}><div className='validation-icon'>{check.passed?<CheckCircle2 size={17}/>:<ShieldAlert size={17}/>}</div><div><strong>{check.label}</strong><p>{check.detail}</p></div></div>)}</div></div><div className='approval-action-box'><div><div className='eyebrow'>High-impact action</div><strong>Only an authorized Clinic Manager can decide.</strong><p>{canDecide ? 'Approve finalises the UI state. Backend transaction, appointment creation, medicine reservation and quotation finalisation will be wired later.' : 'You are signed in as staff without Clinic Manager permissions, so decisions are read-only for your account.'}</p></div><div className='approval-actions'><Button onClick={openHistory} variant='secondary' icon={<History size={16}/>}>View history</Button>{canDecide && <><Button disabled={selected.status !== 'Pending' || saving} onClick={()=>startDecision('Rejected')} variant='secondary' icon={<XCircle size={16}/>}>Reject</Button><Button disabled={selected.status !== 'Pending' || saving} onClick={()=>startDecision('RevisionRequested')} variant='secondary' icon={<RotateCcw size={16}/>}>Request revision</Button><Button disabled={selected.status !== 'Pending' || saving} onClick={()=>startDecision('Approved')} icon={<CheckCircle2 size={16}/>}>Approve proposal</Button></>}</div></div></> : <div className='empty-state'><h3>No proposal selected</h3></div>}</Card>
    </div>
    {decision && selected && <Modal title={`Confirm ${decision === 'RevisionRequested' ? 'revision request' : decision.toLowerCase()}`} onClose={()=>setDecision(null)}><p className='modal-copy'>You are about to change <strong>{selected.id}</strong> for <strong>{selected.petName}</strong> to <strong>{decision === 'RevisionRequested' ? 'Revision Requested' : decision}</strong>.</p><label>Manager note / reason<textarea placeholder='Add a comment or the reason for this decision' value={note} onChange={(e)=>setNote(e.target.value)} rows={4}/></label><div className='modal-actions'><Button variant='secondary' onClick={()=>setDecision(null)}>Cancel</Button><Button variant={decision === 'Rejected' ? 'danger' : 'primary'} onClick={applyDecision} disabled={saving || (decision !== 'Approved' && !note.trim())}>{saving ? 'Saving...' : 'Confirm decision'}</Button></div></Modal>}
    {showHistory && selected && <Modal title='Approval history' onClose={()=>setShowHistory(false)}><div className='history-list'>{history.length === 0 ? <p>No history found.</p> : history.map((h)=><div key={h.id} className='history-row'><strong>{h.previousStatus} → {h.newStatus}</strong><span>by {h.changedBy} on {formatDate(h.changedAt.slice(0,10))}</span><p>Reason: {h.reason || '—'}</p></div>)}</div></Modal>}
  </div>;
}
