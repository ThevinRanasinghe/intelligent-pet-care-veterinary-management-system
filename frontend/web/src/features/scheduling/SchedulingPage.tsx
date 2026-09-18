import { useMemo, useState } from 'react';
import { CalendarPlus, Clock3, Filter, Search, ShieldCheck, UserRound } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Modal } from '../../components/ui/Modal';
import { getAppointmentSlots, hasVetConflict } from '../../services/schedulingService';
import { veterinarians } from '../../services/mockData';
import type { AppointmentSlot } from '../../types/domain';
import { formatDate } from '../../utils/format';

const toneForStatus: Record<AppointmentSlot['status'], 'success' | 'warning' | 'neutral' | 'danger' | 'info'> = {
  Available: 'success', Reserved: 'warning', Confirmed: 'info', Completed: 'neutral', Cancelled: 'danger'
};

export function SchedulingPage() {
  const [slots, setSlots] = useState(getAppointmentSlots());
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState<'All' | AppointmentSlot['status']>('All');
  const [showModal, setShowModal] = useState(false);
  const [form, setForm] = useState({ veterinarianId: 'vet-01', date: '2026-08-22', startTime: '10:00', endTime: '10:30', branch: 'Colombo' });
  const [formError, setFormError] = useState('');
  const filtered = useMemo(() => slots.filter((slot) => {
    const matchesQuery = `${slot.veterinarianName} ${slot.petName ?? ''} ${slot.ownerName ?? ''}`.toLowerCase().includes(query.toLowerCase());
    return matchesQuery && (status === 'All' || slot.status === status);
  }), [slots, query, status]);

  const handleCreate = () => {
    setFormError('');
    if (form.endTime <= form.startTime) { setFormError('End time must be after start time.'); return; }
    if (hasVetConflict(slots, form)) { setFormError('This veterinarian already has an overlapping slot. Choose another time.'); return; }
    const vet = veterinarians.find((item) => item.id === form.veterinarianId)!;
    setSlots((current) => [...current, { id: `slot-${Date.now()}`, veterinarianId: vet.id, veterinarianName: vet.name, date: form.date, startTime: form.startTime, endTime: form.endTime, branch: form.branch, status: 'Available' }]);
    setShowModal(false);
  };

  return <div className="page-wrap">
    <div className="page-heading"><div><div className="eyebrow">Component · Scheduling</div><h2>Veterinarian scheduling</h2><p>Manage conflict-free appointment slots across clinic branches.</p></div><Button icon={<CalendarPlus size={17}/>} onClick={() => setShowModal(true)}>Create appointment slot</Button></div>
    <div className="filter-bar"><div className="search-input"><Search size={17}/><input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search vet, pet or owner" /></div><div className="select-input"><Filter size={15}/><select value={status} onChange={(e) => setStatus(e.target.value as typeof status)}><option>All</option><option>Available</option><option>Reserved</option><option>Confirmed</option><option>Completed</option><option>Cancelled</option></select></div><div className="filter-summary">{filtered.length} slot{filtered.length === 1 ? '' : 's'}</div></div>
    <Card><div className="table-wrap"><table><thead><tr><th>Date</th><th>Time</th><th>Veterinarian</th><th>Branch</th><th>Pet / Owner</th><th>Status</th></tr></thead><tbody>{filtered.map((slot) => <tr key={slot.id}><td><strong>{formatDate(slot.date)}</strong></td><td><span className="time-cell"><Clock3 size={14}/>{slot.startTime}–{slot.endTime}</span></td><td><span className="person-cell"><span className="person-avatar"><UserRound size={14}/></span>{slot.veterinarianName}</span></td><td>{slot.branch}</td><td>{slot.petName ? <><strong>{slot.petName}</strong><span className="muted-line">{slot.ownerName}</span></> : <span className="muted">Open slot</span>}</td><td><Badge tone={toneForStatus[slot.status]}>{slot.status}</Badge></td></tr>)}</tbody></table></div></Card>
    <div className="info-strip"><ShieldCheck size={18}/><div><strong>Business rule ready</strong><span>Appointments are validated against veterinarian/date/time overlaps before a slot can be added. Backend enforcement will be wired to the ASP.NET Core service later.</span></div></div>
    {showModal && <Modal title="Create appointment slot" onClose={() => setShowModal(false)}><div className="form-grid"><label>Veterinarian<select value={form.veterinarianId} onChange={(e) => setForm({...form, veterinarianId:e.target.value})}>{veterinarians.map((vet) => <option key={vet.id} value={vet.id}>{vet.name} · {vet.specialisation}</option>)}</select></label><label>Branch<select value={form.branch} onChange={(e) => setForm({...form, branch:e.target.value})}><option>Colombo</option><option>Kandy</option><option>Nugegoda</option></select></label><label>Date<input type="date" value={form.date} onChange={(e) => setForm({...form,date:e.target.value})}/></label><label>Start time<input type="time" value={form.startTime} onChange={(e) => setForm({...form,startTime:e.target.value})}/></label><label>End time<input type="time" value={form.endTime} onChange={(e) => setForm({...form,endTime:e.target.value})}/></label></div>{formError && <div className="form-error">{formError}</div>}<div className="modal-actions"><Button variant="secondary" onClick={() => setShowModal(false)}>Cancel</Button><Button onClick={handleCreate}>Save slot</Button></div></Modal>}
  </div>;
}
