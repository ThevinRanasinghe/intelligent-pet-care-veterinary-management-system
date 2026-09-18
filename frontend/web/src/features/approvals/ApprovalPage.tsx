import { useMemo, useState } from 'react';
import { CheckCircle2, Clock3, FileCheck2, RotateCcw, Search, ShieldAlert, XCircle } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Modal } from '../../components/ui/Modal';
import { getApprovalProposals } from '../../services/approvalService';
import type { ApprovalProposal } from '../../types/domain';
import { formatDate, formatLkr } from '../../utils/format';

const tone: Record<ApprovalProposal['status'], 'warning' | 'success' | 'danger' | 'info'> = { Pending:'warning', Approved:'success', Rejected:'danger', RevisionRequested:'info' };

export function ApprovalPage() {
  const [proposals, setProposals] = useState(getApprovalProposals());
  const [selected, setSelected] = useState<ApprovalProposal | null>(proposals[0] ?? null);
  const [query, setQuery] = useState('');
  const [decision, setDecision] = useState<'Approved' | 'Rejected' | 'RevisionRequested' | null>(null);
  const pendingCount = proposals.filter((item) => item.status === 'Pending').length;
  const filtered = useMemo(() => proposals.filter((item) => `${item.id} ${item.petName} ${item.ownerName}`.toLowerCase().includes(query.toLowerCase())), [proposals, query]);

  const applyDecision = () => {
    if (!selected || !decision) return;
    setProposals((current) => current.map((proposal) => proposal.id === selected.id ? { ...proposal, status: decision } : proposal));
    setSelected((current) => current ? { ...current, status: decision } : current);
    setDecision(null);
  };

  return <div className="page-wrap">
    <div className="page-heading"><div><div className="eyebrow">Component · Approval</div><h2>Approval center</h2><p>Review scheduling + billing proposals before any high-impact execution step.</p></div><div className="approval-head-count"><span>{pendingCount}</span><div><strong>Pending decisions</strong><small>Manager checkpoint</small></div></div></div>
    <div className="approval-layout">
      <Card className="approval-list-card"><div className="card-header"><div><div className="eyebrow">Human approval</div><h3>Proposal queue</h3></div><Badge tone="warning">{pendingCount} pending</Badge></div><div className="search-input compact"><Search size={16}/><input value={query} onChange={(e)=>setQuery(e.target.value)} placeholder="Search request, pet or owner"/></div><div className="approval-list">{filtered.map((proposal)=><button key={proposal.id} onClick={()=>setSelected(proposal)} className={`approval-list-item ${selected?.id === proposal.id ? 'selected' : ''}`}><div className="approval-item-main"><div className="queue-title"><strong>{proposal.petName}</strong><Badge tone={proposal.urgency==='Urgent'?'danger':'warning'}>{proposal.urgency}</Badge></div><p>{proposal.ownerName} · {proposal.proposedVet}</p><small><Clock3 size={12}/> {formatDate(proposal.proposedDate)} · {proposal.proposedTime}</small></div><div className="approval-item-side"><strong>{formatLkr(proposal.quotationTotal)}</strong><Badge tone={tone[proposal.status]}>{proposal.status === 'RevisionRequested' ? 'Revision' : proposal.status}</Badge></div></button>)}</div></Card>
      <Card className="proposal-detail">{selected ? <><div className="proposal-header"><div><div className="eyebrow">{selected.workflowId} · {selected.id}</div><h3>{selected.petName} appointment proposal</h3><p>{selected.summary}</p></div><Badge tone={tone[selected.status]}>{selected.status}</Badge></div><div className="proposal-grid"><div className="detail-block"><span>Recommended veterinarian</span><strong>{selected.proposedVet}</strong><small>{selected.branch}</small></div><div className="detail-block"><span>Appointment</span><strong>{formatDate(selected.proposedDate)}</strong><small>{selected.proposedTime}</small></div><div className="detail-block"><span>Quotation</span><strong>{formatLkr(selected.quotationTotal)}</strong><small>Budget {formatLkr(selected.budget)}</small></div></div><div className="validation-section"><div className="section-title"><div><div className="eyebrow">Deterministic validation</div><h4>Proposal checks</h4></div><FileCheck2 size={18}/></div><div className="validation-grid">{selected.validationChecks.map((check)=><div key={check.key} className={`validation-check ${check.passed?'pass':'fail'}`}><div className="validation-icon">{check.passed?<CheckCircle2 size={17}/>:<ShieldAlert size={17}/>}</div><div><strong>{check.label}</strong><p>{check.detail}</p></div></div>)}</div></div><div className="approval-action-box"><div><div className="eyebrow">High-impact action</div><strong>Only an authorized Clinic Manager can decide.</strong><p>Approve finalises the UI state. Backend transaction, appointment creation, medicine reservation and quotation finalisation will be wired later.</p></div><div className="approval-actions"><Button disabled={selected.status !== 'Pending'} onClick={()=>setDecision('Rejected')} variant="secondary" icon={<XCircle size={16}/>}>Reject</Button><Button disabled={selected.status !== 'Pending'} onClick={()=>setDecision('RevisionRequested')} variant="secondary" icon={<RotateCcw size={16}/>}>Request revision</Button><Button disabled={selected.status !== 'Pending'} onClick={()=>setDecision('Approved')} icon={<CheckCircle2 size={16}/>}>Approve proposal</Button></div></div></> : <div className="empty-state"><h3>No proposal selected</h3></div>}</Card>
    </div>
    {decision && selected && <Modal title={`Confirm ${decision === 'RevisionRequested' ? 'revision request' : decision.toLowerCase()}`} onClose={()=>setDecision(null)}><p className="modal-copy">You are about to change <strong>{selected.id}</strong> for <strong>{selected.petName}</strong> to <strong>{decision === 'RevisionRequested' ? 'Revision Requested' : decision}</strong>.</p>{decision === 'RevisionRequested' && <label>Manager note<textarea placeholder="Explain what must be changed before approval." rows={4}/></label>}<div className="modal-actions"><Button variant="secondary" onClick={()=>setDecision(null)}>Cancel</Button><Button variant={decision === 'Rejected' ? 'danger' : 'primary'} onClick={applyDecision}>Confirm decision</Button></div></Modal>}
  </div>;
}
