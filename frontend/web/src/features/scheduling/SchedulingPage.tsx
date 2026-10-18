import { useEffect, useMemo, useState } from 'react';
import { CalendarPlus, Check, Clock3, Eye, Filter, Loader2, Pencil, Search, UserRound, X } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Modal } from '../../components/ui/Modal';
import { cancelAppointment, getAppointmentSlots, getAppointments, updateAppointment } from '../../services/schedulingService';
import type { AppointmentSlot, AppointmentStatus } from '../../types/domain';
import { getVeterinarians, type ManagerVeterinarian } from '../../services/managerService';
import { consultationService, type ConsultationRequestApi } from '../../services/api';
import { WalkInBookingModal, type WalkInPrefill } from './WalkInBookingModal';
import { messageFrom } from '../../utils/errors';
import { formatDate } from '../../utils/format';

type StatusFilter = 'All' | 'Available' | 'Reserved' | 'Confirmed' | 'Cancelled';

interface SchedulingRow {
  id: string;
  slotId?: string;
  appointmentId?: string;
  veterinarianId: string;
  date: string;
  startTime: string;
  endTime: string;
  branch: string;
  petId?: string;
  petName?: string | null;
  ownerName?: string | null;
  veterinarianName?: string | null;
  consultationRequestId?: string | null;
  appointmentType?: string | null;
  symptoms?: string | null;
  notes?: string | null;
  status: AppointmentStatus;
}

const toneForStatus: Record<AppointmentStatus, 'success' | 'warning' | 'neutral' | 'danger' | 'info'> = {
  Available: 'success', Reserved: 'warning', Confirmed: 'info', Completed: 'neutral', Cancelled: 'danger'
};

function extractDate(iso: string): string { return iso.slice(0, 10); }
function extractTime(iso: string): string { return iso.slice(11, 19); }

export function SchedulingPage() {
  const [rows, setRows] = useState<SchedulingRow[]>([]);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState<StatusFilter>('All');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const [walkInOpen, setWalkInOpen] = useState(false);
  const [walkInPrefill, setWalkInPrefill] = useState<WalkInPrefill | null>(null);
  const [viewing, setViewing] = useState<SchedulingRow | null>(null);
  const [viewConsultation, setViewConsultation] = useState<ConsultationRequestApi | null>(null);
  const [editing, setEditing] = useState<SchedulingRow | null>(null);
  const [cancelling, setCancelling] = useState<SchedulingRow | null>(null);

  const [vets, setVets] = useState<ManagerVeterinarian[]>([]);
  const [editVetId, setEditVetId] = useState('');
  const [notes, setNotes] = useState('');
  const [editDate, setEditDate] = useState('');
  const [editStart, setEditStart] = useState('');
  const [editEnd, setEditEnd] = useState('');
  const [formError, setFormError] = useState('');

  useEffect(() => { load(); }, []);

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const [slots, appointments] = await Promise.all([getAppointmentSlots(), getAppointments()]);
      // Vet names for slot rows — best effort: the endpoint is manager-scoped
      // and appointment rows already carry denormalised names.
      getVeterinarians()
        .then((list) => setVets(list.filter((v) => v.active)))
        .catch(() => {});
      const slotRows: SchedulingRow[] = slots.map((slot: AppointmentSlot) => ({
        id: `slot-${slot.id}`,
        slotId: slot.id,
        veterinarianId: slot.veterinarianId,
        date: slot.date,
        startTime: slot.startTime,
        endTime: slot.endTime,
        branch: slot.branch,
        status: 'Available',
      }));
      const apptRows: SchedulingRow[] = appointments.map((appt) => ({
        id: `appt-${appt.id}`,
        slotId: appt.appointmentSlotId,
        appointmentId: appt.id,
        veterinarianId: appt.veterinarianId,
        date: extractDate(appt.scheduledStart),
        startTime: extractTime(appt.scheduledStart),
        endTime: extractTime(appt.scheduledEnd),
        branch: '—',
        petId: appt.petId,
        petName: appt.petName,
        ownerName: appt.ownerName,
        veterinarianName: appt.veterinarianName,
        consultationRequestId: appt.consultationRequestId,
        appointmentType: appt.type,
        symptoms: appt.symptoms,
        notes: appt.notes,
        status: appt.status as AppointmentStatus,
      }));
      const all = [...slotRows, ...apptRows];
      all.sort((a, b) => a.date.localeCompare(b.date) || a.startTime.localeCompare(b.startTime));
      setRows(all);
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!success) return;
    const t = setTimeout(() => setSuccess(''), 3000);
    return () => clearTimeout(t);
  }, [success]);

  const filtered = useMemo(() => {
    const q = query.toLowerCase().trim();
    return rows.filter((row) => {
      const haystack = `${row.veterinarianId} ${row.veterinarianName ?? ''} ${row.petId ?? ''} ${row.petName ?? ''} ${row.ownerName ?? ''} ${row.branch}`.toLowerCase();
      const matchesQuery = !q || haystack.includes(q);
      const matchesStatus = status === 'All' || row.status === status;
      return matchesQuery && matchesStatus;
    });
  }, [rows, query, status]);

  const vetNameFor = (row: SchedulingRow) =>
    row.veterinarianName
    ?? vets.find((v) => v.id === row.veterinarianId)?.name
    ?? row.veterinarianId;

  // Fetch the linked consultation while the detail modal is open so the
  // manager can see the workflow record behind the appointment.
  useEffect(() => {
    setViewConsultation(null);
    const id = viewing?.consultationRequestId;
    if (!viewing || !id) return;
    let cancelled = false;
    consultationService.getConsultationById(id)
      .then((c) => { if (!cancelled) setViewConsultation(c); })
      .catch(() => {});
    return () => { cancelled = true; };
  }, [viewing]);

  const openBook = (row: SchedulingRow) => {
    // Direct booking goes through the walk-in workflow now — the slot's
    // vet/date/time pre-fill the intake form.
    setWalkInPrefill({
      veterinarianId: row.veterinarianId,
      date: row.date,
      startTime: row.startTime.slice(0, 5),
    });
    setWalkInOpen(true);
    setFormError('');
  };

  const openEdit = (row: SchedulingRow) => {
    setEditing(row);
    setEditVetId(row.veterinarianId);
    setEditDate(row.date);
    setEditStart(row.startTime.slice(0, 5));
    setEditEnd(row.endTime.slice(0, 5));
    setNotes(row.notes ?? '');
    setFormError('');
  };

  const handleEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editing) return;
    if (editEnd <= editStart) { setFormError('End time must be after start time'); return; }
    try {
      setFormError('');
      await updateAppointment(editing.appointmentId!, {
        scheduledStart: `${editDate}T${editStart}:00`,
        scheduledEnd: `${editDate}T${editEnd}:00`,
        // Notes contract: send the field so an emptied box clears the note.
        notes: notes.trim(),
        veterinarianId: editVetId !== editing.veterinarianId ? editVetId : undefined,
      });
      setEditing(null);
      setNotes('');
      setSuccess('Appointment updated');
      await load();
    } catch (err) {
      setFormError(messageFrom(err));
    }
  };

  const handleCancel = async () => {
    if (!cancelling?.appointmentId) return;
    try {
      await cancelAppointment(cancelling.appointmentId);
      setCancelling(null);
      setSuccess('Appointment cancelled');
      await load();
    } catch (err) {
      setError(messageFrom(err));
      setCancelling(null);
    }
  };

  return <div className='page-wrap'>
    {error && <div className='error-banner'>{error}</div>}
    {success && <div className='success-banner'>{success}</div>}
    <div className='page-heading'>
      <div>
        <div className='eyebrow'>Component · Scheduling</div>
        <h2>Veterinarian scheduling</h2>
        <p>Manage appointment slots, walk-in bookings and the consultation workflow.</p>
      </div>
      <Button
        onClick={() => { setWalkInPrefill(null); setWalkInOpen(true); }}
        icon={<CalendarPlus size={16} />}
      >
        Create walk-in booking
      </Button>
    </div>
    <div className='filter-bar'>
      <div className='search-input' style={{ minWidth: 'min(280px, 100%)' }}>
        <Search size={17} />
        <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder='Search vet, pet or owner' />
      </div>
      <div className='select-input'>
        <Filter size={15} />
        <select value={status} onChange={(e) => setStatus(e.target.value as StatusFilter)}>
          <option>All</option>
          <option>Available</option>
          <option>Reserved</option>
          <option>Confirmed</option>
          <option>Cancelled</option>
        </select>
      </div>
      <div className='filter-summary'>{filtered.length} result{filtered.length === 1 ? '' : 's'}</div>
    </div>
    <Card>
      <div className='table-wrap'>
        <table style={{ minWidth: '760px' }}>
          <thead>
            <tr>
              <th>Date</th>
              <th>Time</th>
              <th>Veterinarian</th>
              <th>Branch</th>
              <th>Pet / Owner</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={7} className='empty-state'>
                  <Loader2 className='spinner' size={24} /> Loading schedule...
                </td>
              </tr>
            ) : filtered.length === 0 ? (
              <tr>
                <td colSpan={7} className='empty-state'>No slots found</td>
              </tr>
            ) : filtered.map((row) => (
              <tr key={row.id}>
                <td><strong>{formatDate(row.date)}</strong></td>
                <td>
                  <span className='time-cell'>
                    <Clock3 size={14} /> {row.startTime.slice(0, 5)}–{row.endTime.slice(0, 5)}
                  </span>
                </td>
                <td>
                  <span className='person-cell'>
                    <span className='person-avatar'><UserRound size={14} /></span>
                    <span style={{ maxWidth: 120, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{vetNameFor(row)}</span>
                  </span>
                </td>
                <td>{row.branch}</td>
                <td>
                  {row.petId ? (
                    <>
                      <strong style={{ display: 'block', maxWidth: 140, overflow: 'hidden', textOverflow: 'ellipsis' }}>
                        {row.petName ?? `Pet: ${row.petId}`}
                      </strong>
                      <span className='muted-line'>{row.ownerName ?? 'Owner: —'}</span>
                    </>
                  ) : (
                    <span className='muted'>Open slot</span>
                  )}
                </td>
                <td><Badge tone={toneForStatus[row.status]}>{row.status}</Badge></td>
                <td>
                  {row.status === 'Available' ? (
                    <Button onClick={() => openBook(row)} icon={<CalendarPlus size={15} />}>Book</Button>
                  ) : (
                    <div className='scheduling-actions'>
                      <Button variant='ghost' onClick={() => setViewing(row)} icon={<Eye size={15} />} aria-label='View' />
                      <Button variant='ghost' onClick={() => openEdit(row)} icon={<Pencil size={15} />} aria-label='Edit' />
                      <Button variant='ghost' onClick={() => setCancelling(row)} icon={<X size={15} />} aria-label='Cancel' />
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>

    {walkInOpen && (
      <WalkInBookingModal
        prefill={walkInPrefill}
        onClose={() => setWalkInOpen(false)}
        onCreated={async (result) => {
          setWalkInOpen(false);
          setSuccess(
            `Walk-in booked — consultation ${result.consultation.id} confirmed, appointment on ${formatDate(result.appointment.scheduledStart.slice(0, 10))}.`,
          );
          await load();
        }}
      />
    )}

    {viewing && (
      <Modal title='Appointment details' onClose={() => setViewing(null)}>
        <div className='proposal-grid' style={{ marginTop: '6px', gridTemplateColumns: 'repeat(auto-fit, minmax(min(200px, 100%), 1fr))' }}>
          <div className='detail-block'>
            <span>Appointment ID</span>
            <strong style={{ fontSize: '11px', wordBreak: 'break-all' }}>{viewing.appointmentId ?? 'Open slot'}</strong>
          </div>
          <div className='detail-block'>
            <span>Slot ID</span>
            <strong style={{ fontSize: '11px', wordBreak: 'break-all' }}>{viewing.slotId ?? '—'}</strong>
          </div>
          <div className='detail-block'><span>Date</span><strong>{formatDate(viewing.date)}</strong></div>
          <div className='detail-block'><span>Time</span><strong>{viewing.startTime.slice(0, 5)}–{viewing.endTime.slice(0, 5)}</strong></div>
          <div className='detail-block'>
            <span>Veterinarian</span>
            <strong style={{ fontSize: '11px', wordBreak: 'break-all' }}>{vetNameFor(viewing)}</strong>
          </div>
          <div className='detail-block'><span>Branch</span><strong>{viewing.branch}</strong></div>
          <div className='detail-block'>
            <span>Pet</span>
            <strong style={{ fontSize: '11px', wordBreak: 'break-all' }}>
              {viewing.petName ?? viewing.petId ?? '—'}
            </strong>
          </div>
          <div className='detail-block'>
            <span>Owner</span>
            <strong>{viewing.ownerName ?? '—'}</strong>
          </div>
          <div className='detail-block'>
            <span>Status</span>
            <strong><Badge tone={toneForStatus[viewing.status]}>{viewing.status}</Badge></strong>
          </div>
          <div className='detail-block'>
            <span>Booking type</span>
            <strong>
              {viewing.appointmentType ?? (viewing.appointmentId ? 'Initial' : '—')}
              {viewConsultation?.source === 'WalkIn' && (
                <> <Badge tone='warning'>Walk-In</Badge></>
              )}
            </strong>
          </div>
          <div className='detail-block'>
            <span>Consultation</span>
            <strong style={{ fontSize: '11px', wordBreak: 'break-all' }}>
              {viewing.consultationRequestId ?? '—'}
            </strong>
          </div>
          {viewing.symptoms && (
            <div className='detail-block'>
              <span>Symptoms</span>
              <strong style={{ fontWeight: 400 }}>{viewing.symptoms}</strong>
            </div>
          )}
          {viewConsultation && (
            <div className='detail-block'>
              <span>Consultation status</span>
              <strong>{viewConsultation.status}</strong>
            </div>
          )}
          {viewing.notes && (
            <div className='detail-block'>
              <span>Notes</span>
              <strong style={{ fontWeight: 400 }}>{viewing.notes}</strong>
            </div>
          )}
        </div>
        <div className='modal-actions'>
          <Button variant='secondary' onClick={() => setViewing(null)}>Close</Button>
        </div>
      </Modal>
    )}

    {editing && (
      <Modal title='Edit appointment' onClose={() => setEditing(null)}>
        <form onSubmit={handleEdit} className='form-grid' style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <div className='detail-block' style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(200px, 100%), 1fr))', gap: '10px', marginBottom: '6px' }}>
            <div>
              <span>Pet</span>
              <strong style={{ fontSize: '11px', wordBreak: 'break-all' }}>{editing.petName ?? editing.petId}</strong>
            </div>
            <div>
              <span>Linked consultation</span>
              <strong style={{ fontSize: '11px', wordBreak: 'break-all' }}>{editing.consultationRequestId ?? '—'}</strong>
            </div>
          </div>
          <label>
            Veterinarian
            <select value={editVetId} onChange={(e) => setEditVetId(e.target.value)}>
              {!vets.some((v) => v.id === editing.veterinarianId) && (
                <option value={editing.veterinarianId}>{vetNameFor(editing)}</option>
              )}
              {vets.map((v) => (
                <option key={v.id} value={v.id}>{v.name} — {v.specialisation}</option>
              ))}
            </select>
          </label>
          <label>
            Date
            <input type='date' value={editDate} onChange={(e) => setEditDate(e.target.value)} />
          </label>
          <div className='form-grid'>
            <label>
              Start time
              <input type='time' value={editStart} onChange={(e) => setEditStart(e.target.value)} step={60} />
            </label>
            <label>
              End time
              <input type='time' value={editEnd} onChange={(e) => setEditEnd(e.target.value)} step={60} />
            </label>
          </div>
          <label>
            Notes
            <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={3} placeholder='Optional notes' />
          </label>
          {formError && <div className='form-error'>{formError}</div>}
          <div className='modal-actions'>
            <Button type='button' variant='secondary' onClick={() => setEditing(null)}>Cancel</Button>
            <Button type='submit' icon={<Check size={16} />}>Save changes</Button>
          </div>
        </form>
      </Modal>
    )}

    {cancelling && (
      <Modal title='Cancel appointment' onClose={() => setCancelling(null)}>
        <p className='modal-copy'>This will cancel the appointment and free the slot. Are you sure?</p>
        <div className='modal-actions'>
          <Button variant='secondary' onClick={() => setCancelling(null)}>Keep</Button>
          <Button variant='danger' onClick={handleCancel} icon={<X size={16} />}>Cancel appointment</Button>
        </div>
      </Modal>
    )}
  </div>;
}
