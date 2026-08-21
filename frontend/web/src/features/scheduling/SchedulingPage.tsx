import { useEffect, useMemo, useState } from 'react';
import { CalendarPlus, Clock3, Filter, Loader2, Search, ShieldCheck, UserRound } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Modal } from '../../components/ui/Modal';
import { checkVetConflict, createAppointment, getAppointmentSlots } from '../../services/schedulingService';
import type { AppointmentSlot } from '../../types/domain';
import { formatDate } from '../../utils/format';

const toneForStatus: Record<AppointmentSlot['status'], 'success' | 'warning' | 'neutral' | 'danger' | 'info'> = {
  Available: 'success', Reserved: 'warning', Confirmed: 'info', Completed: 'neutral', Cancelled: 'danger'
};

export function SchedulingPage() {
  const [slots, setSlots] = useState<AppointmentSlot[]>([]);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState<'All' | AppointmentSlot['status']>('All');
  const [showModal, setShowModal] = useState(false);
  const [form, setForm] = useState({ veterinarianId: '', branch: 'Colombo', date: '2026-08-22', startTime: '10:00', endTime: '10:30', petId: '' });
  const [formError, setFormError] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const filtered = useMemo(() => slots.filter((slot) => {
    const matchesQuery = `${slot.veterinarianName} ${slot.petName ?? ''} ${slot.ownerName ?? ''}`.toLowerCase().includes(query.toLowerCase());
    return matchesQuery && (status === 'All' || slot.status === status);
  }), [slots, query, status]);

  const vets = useMemo(() => {
    const map = new Map<string, { id: string; name: string; branch: string }>();
    slots.forEach((slot) => {
      if (!map.has(slot.veterinarianId)) {
        map.set(slot.veterinarianId, { id: slot.veterinarianId, name: slot.veterinarianName, branch: slot.branch });
      }
    });
    return Array.from(map.values());
  }, [slots]);

  useEffect(() => {
    if (vets.length && !form.veterinarianId) {
      setForm((f) => ({ ...f, veterinarianId: vets[0].id, branch: vets[0].branch }));
    }
  }, [vets]);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      setError('');
      try {
        const data = await getAppointmentSlots();
        if (!cancelled) setSlots(data);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Failed to load slots');
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

  const toIso = (date: string, time: string) => `${date}T${time}:00`;

  const handleCreate = async () => {
    setFormError('');
    if (form.endTime <= form.startTime) { setFormError('End time must be after start time.'); return; }
    const scheduledStart = toIso(form.date, form.startTime);
    const scheduledEnd = toIso(form.date, form.endTime);
    try {
      const conflict = await checkVetConflict({ veterinarianId: form.veterinarianId, scheduledStart, scheduledEnd });
      if (conflict) { setFormError('This veterinarian already has an overlapping appointment. Choose another time.'); return; }
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Conflict check failed');
      return;
    }
    const matchingSlot = slots.find((slot) =>
      slot.veterinarianId === form.veterinarianId &&
      slot.date === form.date &&
      slot.startTime <= form.startTime &&
      slot.endTime >= form.endTime &&
      slot.status === 'Available'
    );
    if (!matchingSlot) { setFormError('No matching open slot was found. Choose a time that fits inside an available slot.'); return; }
    if (!form.petId.trim()) { setFormError('Pet ID is required to create an appointment.'); return; }
    try {
      await createAppointment({
        petId: form.petId.trim(),
        veterinarianId: form.veterinarianId,
        appointmentSlotId: matchingSlot.id,
        scheduledStart,
        scheduledEnd,
      });
      const data = await getAppointmentSlots();
      setSlots(data);
      setSuccess('Appointment created');
      setShowModal(false);
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Create failed');
    }
  };

  return <div className='page-wrap'>
    {error && <div className='error-banner'>{error}</div>}
    {success && <div className='success-banner'>{success}</div>}
    <div className='page-heading'><div><div className='eyebrow'>Component · Scheduling</div><h2>Veterinarian scheduling</h2><p>Manage conflict-free appointment slots across clinic branches.</p></div><Button icon={<CalendarPlus size={17}/>} onClick={() => setShowModal(true)}>Create appointment</Button></div>
    <div className='filter-bar'><div className='search-input'><Search size={17}/><input value={query} onChange={(e) => setQuery(e.target.value)} placeholder='Search vet, pet or owner' /></div><div className='select-input'><Filter size={15}/><select value={status} onChange={(e) => setStatus(e.target.value as typeof status)}><option>All</option><option>Available</option><option>Reserved</option><option>Confirmed</option><option>Completed</option><option>Cancelled</option></select></div><div className='filter-summary'>{filtered.length} slot{filtered.length === 1 ? '' : 's'}</div></div>
    <Card><div className='table-wrap'><table><thead><tr><th>Date</th><th>Time</th><th>Veterinarian</th><th>Branch</th><th>Pet / Owner</th><th>Status</th></tr></thead><tbody>{loading ? <tr><td colSpan={6} className='empty-state'><Loader2 className='spinner' size={24}/> Loading slots...</td></tr> : (filtered.length === 0 ? <tr><td colSpan={6} className='empty-state'>No slots found</td></tr> : filtered.map((slot) => <tr key={slot.id}><td><strong>{formatDate(slot.date)}</strong></td><td><span className='time-cell'><Clock3 size={14}/>{slot.startTime}–{slot.endTime}</span></td><td><span className='person-cell'><span className='person-avatar'><UserRound size={14}/></span>{slot.veterinarianName}</span></td><td>{slot.branch}</td><td>{slot.petName ? <><strong>{slot.petName}</strong><span className='muted-line'>{slot.ownerName}</span></> : <span className='muted'>Open slot</span>}</td><td><Badge tone={toneForStatus[slot.status]}>{slot.status}</Badge></td></tr>))}</tbody></table></div></Card>
    <div className='info-strip'><ShieldCheck size={18}/><div><strong>Backend rule ready</strong><span>Appointments are validated against veterinarian/date/time overlaps via the ASP.NET Core API.</span></div></div>
    {showModal && <Modal title='Create appointment' onClose={() => setShowModal(false)}><div className='form-grid'><label>Veterinarian<select value={form.veterinarianId} onChange={(e) => { const v = vets.find((x) => x.id === e.target.value); setForm({...form, veterinarianId: e.target.value, branch: v?.branch ?? form.branch}); }}>{vets.map((vet) => <option key={vet.id} value={vet.id}>{vet.name} · {vet.branch}</option>)}</select></label><label>Branch<select value={form.branch} onChange={(e) => setForm({...form, branch: e.target.value})}><option>Colombo</option><option>Kandy</option><option>Nugegoda</option></select></label><label>Date<input type='date' value={form.date} onChange={(e) => setForm({...form, date: e.target.value})}/></label><label>Start time<input type='time' value={form.startTime} onChange={(e) => setForm({...form, startTime: e.target.value})}/></label><label>End time<input type='time' value={form.endTime} onChange={(e) => setForm({...form, endTime: e.target.value})}/></label><label>Pet ID<input type='text' value={form.petId} onChange={(e) => setForm({...form, petId: e.target.value})} placeholder='Enter the pet GUID'/></label></div>{formError && <div className='form-error'>{formError}</div>}<div className='modal-actions'><Button variant='secondary' onClick={() => setShowModal(false)}>Cancel</Button><Button onClick={handleCreate}>Save appointment</Button></div></Modal>}
  </div>;
}
