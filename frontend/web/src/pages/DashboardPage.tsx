import { CalendarClock, ClipboardCheck, FileText, PawPrint, TrendingUp, UsersRound } from 'lucide-react';
import { Card } from '../components/ui/Card';
import { Badge } from '../components/ui/Badge';
import { approvalProposals, appointmentSlots, quotations } from '../services/mockData';
import { formatLkr } from '../utils/format';

export function DashboardPage() {
  const confirmed = appointmentSlots.filter((item) => item.status === 'Confirmed').length;
  const pending = approvalProposals.filter((item) => item.status === 'Pending').length;
  const approvedQuotes = quotations.filter((item) => ['Approved', 'Finalised'].includes(item.status));
  const expectedRevenue = approvedQuotes.reduce((sum, q) => sum + q.items.reduce((inner, line) => inner + line.quantity * line.unitPrice, 0), 0);
  return <div className="page-wrap">
    <div className="page-heading"><div><div className="eyebrow">Clinic overview</div><h2>Good evening, Clinic Manager</h2><p>Monitor today's operations and move approval-ready cases forward.</p></div><div className="heading-actions"><Badge tone="success">System healthy</Badge><span className="heading-date">18 Aug 2026</span></div></div>
    <div className="stat-grid">
      {[{label:'Appointments today', value:confirmed.toString(), trend:'+2', icon:CalendarClock}, {label:'Pending approvals', value:pending.toString(), trend:'Needs review', icon:ClipboardCheck}, {label:'Approved quotes', value:approvedQuotes.length.toString(), trend:formatLkr(expectedRevenue), icon:FileText}, {label:'Active vets', value:'4', trend:'All branches', icon:UsersRound}].map(({label,value,trend,icon:Icon}) => <Card key={label} className="stat-card"><div className="stat-top"><div className="stat-icon"><Icon size={18}/></div><span>{trend}</span></div><strong>{value}</strong><p>{label}</p></Card>)}
    </div>
    <div className="content-grid dashboard-grid">
      <Card><div className="card-header"><div><div className="eyebrow">Approval queue</div><h3>Requests needing attention</h3></div><a href="/approvals">View all</a></div><div className="queue-list">{approvalProposals.map((proposal) => <div className="queue-item" key={proposal.id}><div><div className="queue-title"><span>{proposal.petName}</span><Badge tone={proposal.urgency === 'Urgent' ? 'danger' : 'warning'}>{proposal.urgency}</Badge></div><p>{proposal.ownerName} · {proposal.proposedVet}</p></div><strong>{formatLkr(proposal.quotationTotal)}</strong></div>)}</div></Card>
      <Card><div className="card-header"><div><div className="eyebrow">Today</div><h3>Clinic pulse</h3></div><TrendingUp size={18}/></div><div className="pulse-row"><div className="pulse-number">84%</div><div><strong>On-time capacity</strong><p>Available slots are healthy across active branches.</p></div></div><div className="mini-bar"><span style={{width:'84%'}} /></div><div className="pulse-note"><PawPrint size={16}/><span>Appointment conflicts are checked before a slot is saved.</span></div></Card>
    </div>
  </div>;
}
