import { useState, useEffect, useCallback } from 'react';
import {
  Calendar, Clock, Plus, Trash2, CheckCircle2, AlertCircle,
  Stethoscope, RefreshCw, User, MapPin, X
} from 'lucide-react';
import {
  getOrganizationVeterinarians,
  getScheduleSlots,
  createScheduleSlot,
  deleteScheduleSlot
} from '../services/adminApi';

export default function VeterinarianScheduleView() {
  const [veterinarians, setVeterinarians] = useState([]);
  const [selectedVetId, setSelectedVetId] = useState('');
  const [selectedDate, setSelectedDate] = useState(() => new Date().toISOString().split('T')[0]);
  const [slots, setSlots] = useState([]);
  const [loading, setLoading] = useState(true);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  // Add Slot Modal
  const [modalOpen, setModalOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [slotForm, setSlotForm] = useState({
    veterinarianId: '',
    date: new Date().toISOString().split('T')[0],
    startTime: '09:00',
    endTime: '09:30',
    branch: 'Main Clinic'
  });
  const [formError, setFormError] = useState('');

  // Fetch Veterinarians list
  const fetchVeterinarians = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const vets = await getOrganizationVeterinarians();
      setVeterinarians(vets);
      if (vets.length > 0 && !selectedVetId) {
        // default to first vet or empty for all
      }
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to load clinic veterinarians.');
    } finally {
      setLoading(false);
    }
  }, [selectedVetId]);

  // Fetch Slots
  const fetchSlots = useCallback(async () => {
    setSlotsLoading(true);
    try {
      const data = await getScheduleSlots({
        veterinarianId: selectedVetId || undefined,
        date: selectedDate || undefined
      });
      setSlots(data);
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to load schedule slots.');
    } finally {
      setSlotsLoading(false);
    }
  }, [selectedVetId, selectedDate]);

  useEffect(() => {
    fetchVeterinarians();
  }, [fetchVeterinarians]);

  useEffect(() => {
    fetchSlots();
  }, [fetchSlots]);

  const handleOpenAddModal = () => {
    setSlotForm({
      veterinarianId: selectedVetId || (veterinarians[0]?.id || ''),
      date: selectedDate || new Date().toISOString().split('T')[0],
      startTime: '09:00',
      endTime: '09:30',
      branch: 'Main Clinic'
    });
    setFormError('');
    setModalOpen(true);
  };

  const handleCreateSlot = async (e) => {
    e.preventDefault();
    setFormError('');

    if (!slotForm.veterinarianId) {
      setFormError('Please select a veterinarian.');
      return;
    }
    if (!slotForm.date) {
      setFormError('Please specify a date.');
      return;
    }
    if (slotForm.startTime >= slotForm.endTime) {
      setFormError('Start time must precede end time.');
      return;
    }

    setSubmitting(true);
    try {
      await createScheduleSlot({
        veterinarianId: slotForm.veterinarianId,
        date: slotForm.date,
        startTime: slotForm.startTime + ':00',
        endTime: slotForm.endTime + ':00',
        branch: slotForm.branch
      });
      setSuccessMsg('Schedule slot created successfully.');
      setModalOpen(false);
      fetchSlots();
      setTimeout(() => setSuccessMsg(''), 4000);
    } catch (err) {
      setFormError(err.response?.data?.message || 'Failed to create slot. Slot may conflict with existing schedule.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleDeleteSlot = async (slotId) => {
    if (!window.confirm('Are you sure you want to remove this open schedule slot?')) return;
    try {
      await deleteScheduleSlot(slotId);
      setSuccessMsg('Slot removed successfully.');
      fetchSlots();
      setTimeout(() => setSuccessMsg(''), 4000);
    } catch (err) {
      alert(err.response?.data?.message || 'Failed to remove slot.');
    }
  };

  const getStatusBadge = (status) => {
    switch (status) {
      case 'Available':
      case 'Open':
        return { bg: '#dcfce7', text: '#15803d', label: 'Available (Open)' };
      case 'Booked':
        return { bg: '#e0e7ff', text: '#4338ca', label: 'Booked' };
      case 'Held':
        return { bg: '#fef3c7', text: '#b45309', label: 'Held' };
      case 'Blocked':
        return { bg: '#fee2e2', text: '#b91c1c', label: 'Blocked' };
      default:
        return { bg: '#f3f4f6', text: '#4b5563', label: status };
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Header Controls Bar */}
      <div style={{
        backgroundColor: '#ffffff',
        borderRadius: '0.875rem',
        border: '1px solid #e5e7eb',
        padding: '1.25rem',
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '1rem'
      }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '1rem' }}>
          {/* Vet Filter */}
          <div>
            <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: '600', color: '#6b7280', marginBottom: '0.25rem' }}>
              VETERINARIAN
            </label>
            <select
              value={selectedVetId}
              onChange={(e) => setSelectedVetId(e.target.value)}
              style={{
                padding: '0.5rem 0.75rem',
                borderRadius: '0.5rem',
                border: '1px solid #d1d5db',
                fontSize: '0.875rem',
                backgroundColor: '#fff',
                minWidth: '200px'
              }}
            >
              <option value="">All Veterinarians ({veterinarians.length})</option>
              {veterinarians.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.name} ({v.specialisation})
                </option>
              ))}
            </select>
          </div>

          {/* Date Picker */}
          <div>
            <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: '600', color: '#6b7280', marginBottom: '0.25rem' }}>
              DATE
            </label>
            <input
              type="date"
              value={selectedDate}
              onChange={(e) => setSelectedDate(e.target.value)}
              style={{
                padding: '0.5rem 0.75rem',
                borderRadius: '0.5rem',
                border: '1px solid #d1d5db',
                fontSize: '0.875rem',
                backgroundColor: '#fff'
              }}
            />
          </div>

          <button
            onClick={() => {
              setSelectedDate(new Date().toISOString().split('T')[0]);
            }}
            style={{
              alignSelf: 'flex-end',
              padding: '0.5rem 0.875rem',
              borderRadius: '0.5rem',
              border: '1px solid #d1d5db',
              backgroundColor: '#f9fafb',
              fontSize: '0.85rem',
              fontWeight: '500',
              color: '#374151',
              cursor: 'pointer'
            }}
          >
            Today
          </button>
        </div>

        {/* Action Button */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <button
            onClick={fetchSlots}
            title="Refresh"
            style={{
              padding: '0.55rem',
              borderRadius: '0.5rem',
              border: '1px solid #d1d5db',
              backgroundColor: '#fff',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center'
            }}
          >
            <RefreshCw size={16} className={slotsLoading ? 'animate-spin' : ''} style={{ color: '#4b5563' }} />
          </button>

          <button
            onClick={handleOpenAddModal}
            style={{
              padding: '0.55rem 1.125rem',
              backgroundColor: '#0d9488',
              color: '#ffffff',
              borderRadius: '0.5rem',
              border: 'none',
              fontWeight: '600',
              fontSize: '0.875rem',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '0.375rem',
              boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
            }}
          >
            <Plus size={16} /> Define Availability Slot
          </button>
        </div>
      </div>

      {/* Alert Banners */}
      {successMsg && (
        <div style={{ padding: '0.875rem 1rem', backgroundColor: '#ecfdf5', border: '1px solid #a7f3d0', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#065f46', fontSize: '0.875rem' }}>
          <CheckCircle2 size={18} />
          <span>{successMsg}</span>
        </div>
      )}
      {error && (
        <div style={{ padding: '0.875rem 1rem', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#991b1b', fontSize: '0.875rem' }}>
          <AlertCircle size={18} />
          <span>{error}</span>
        </div>
      )}

      {/* Schedule Slots Grid / Table */}
      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.875rem', border: '1px solid #e5e7eb', overflow: 'hidden' }}>
        <div style={{ padding: '1.25rem', borderBottom: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <h3 style={{ fontSize: '1rem', fontWeight: '600', color: '#111827', margin: 0 }}>
              Veterinarian Time Slots ({slots.length})
            </h3>
            <span style={{ fontSize: '0.8rem', color: '#6b7280' }}>
              Showing slots for {selectedDate || 'all dates'}
            </span>
          </div>
        </div>

        {slotsLoading ? (
          <div style={{ padding: '3rem', textAlign: 'center', color: '#6b7280' }}>
            <RefreshCw className="animate-spin" size={24} style={{ color: '#0d9488', margin: '0 auto 0.5rem' }} />
            <p style={{ margin: 0, fontSize: '0.875rem' }}>Loading schedule slots...</p>
          </div>
        ) : slots.length === 0 ? (
          <div style={{ padding: '3.5rem 2rem', textAlign: 'center', color: '#6b7280' }}>
            <Calendar size={36} style={{ color: '#9ca3af', margin: '0 auto 0.75rem' }} />
            <h4 style={{ fontWeight: '600', color: '#374151', margin: '0 0 0.25rem' }}>No slots scheduled for this date</h4>
            <p style={{ fontSize: '0.85rem', color: '#9ca3af', margin: '0 0 1.25rem' }}>
              Click "Define Availability Slot" above to configure consultation slots for your veterinarians.
            </p>
            <button
              onClick={handleOpenAddModal}
              style={{
                padding: '0.5rem 1rem',
                backgroundColor: '#f0fdfa',
                color: '#0d9488',
                border: '1px solid #99f6e4',
                borderRadius: '0.5rem',
                fontWeight: '600',
                fontSize: '0.85rem',
                cursor: 'pointer'
              }}
            >
              + Add First Slot
            </button>
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
              <thead>
                <tr style={{ backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Veterinarian</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Time Range</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Status</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Assigned Pet / Owner</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Location / Branch</th>
                  <th style={{ padding: '0.75rem 1.25rem', textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody style={{ divideY: '1px solid #f3f4f6' }}>
                {slots.map((slot) => {
                  const badge = getStatusBadge(slot.status);
                  const isAvailable = slot.status === 'Available' || slot.status === 'Open';
                  return (
                    <tr key={slot.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                          <div style={{ width: 28, height: 28, borderRadius: '50%', backgroundColor: '#f0fdfa', color: '#0d9488', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            <Stethoscope size={14} />
                          </div>
                          <span style={{ fontWeight: '600', color: '#111827' }}>{slot.veterinarianName}</span>
                        </div>
                      </td>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', color: '#374151', fontWeight: '500' }}>
                          <Clock size={14} style={{ color: '#6b7280' }} />
                          <span>
                            {slot.startTime} – {slot.endTime}
                          </span>
                        </div>
                      </td>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <span style={{
                          padding: '0.2rem 0.6rem',
                          borderRadius: '9999px',
                          backgroundColor: badge.bg,
                          color: badge.text,
                          fontSize: '0.75rem',
                          fontWeight: '600'
                        }}>
                          {badge.label}
                        </span>
                      </td>
                      <td style={{ padding: '1rem 1.25rem', color: '#4b5563' }}>
                        {slot.petName ? (
                          <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                            <User size={13} style={{ color: '#6b7280' }} />
                            <span><strong>{slot.petName}</strong> ({slot.ownerName})</span>
                          </div>
                        ) : (
                          <span style={{ color: '#9ca3af', fontStyle: 'italic' }}>Unassigned</span>
                        )}
                      </td>
                      <td style={{ padding: '1rem 1.25rem', color: '#4b5563' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                          <MapPin size={13} style={{ color: '#6b7280' }} />
                          <span>{slot.branch || 'Main Clinic'}</span>
                        </div>
                      </td>
                      <td style={{ padding: '1rem 1.25rem', textAlign: 'right' }}>
                        {isAvailable ? (
                          <button
                            onClick={() => handleDeleteSlot(slot.id)}
                            title="Delete Slot"
                            style={{
                              padding: '0.35rem 0.6rem',
                              borderRadius: '0.375rem',
                              border: '1px solid #fecaca',
                              backgroundColor: '#fff',
                              color: '#b91c1c',
                              fontSize: '0.75rem',
                              cursor: 'pointer',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '0.25rem'
                            }}
                          >
                            <Trash2 size={13} /> Remove
                          </button>
                        ) : (
                          <span style={{ fontSize: '0.75rem', color: '#9ca3af' }}>Booked</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Define Availability Slot Modal */}
      {modalOpen && (
        <div style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 50,
          padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: '0.875rem',
            width: '100%',
            maxWidth: '500px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)',
            overflow: 'hidden'
          }}>
            <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Clock size={20} style={{ color: '#0d9488' }} />
                <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', margin: 0 }}>
                  Define Veterinarian Slot
                </h3>
              </div>
              <button
                onClick={() => setModalOpen(false)}
                style={{ background: 'none', border: 'none', color: '#6b7280', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleCreateSlot} style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              {formError && (
                <div style={{ padding: '0.75rem', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '0.5rem', color: '#b91c1c', fontSize: '0.85rem' }}>
                  {formError}
                </div>
              )}

              {/* Select Vet */}
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                  Select Veterinarian *
                </label>
                <select
                  value={slotForm.veterinarianId}
                  onChange={(e) => setSlotForm({ ...slotForm, veterinarianId: e.target.value })}
                  style={{
                    width: '100%',
                    padding: '0.55rem 0.75rem',
                    borderRadius: '0.5rem',
                    border: '1px solid #d1d5db',
                    fontSize: '0.875rem'
                  }}
                  required
                >
                  <option value="">Select Doctor...</option>
                  {veterinarians.map((v) => (
                    <option key={v.id} value={v.id}>
                      {v.name} — {v.specialisation} ({v.branch})
                    </option>
                  ))}
                </select>
              </div>

              {/* Date */}
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                  Slot Date *
                </label>
                <input
                  type="date"
                  value={slotForm.date}
                  onChange={(e) => setSlotForm({ ...slotForm, date: e.target.value })}
                  style={{
                    width: '100%',
                    padding: '0.55rem 0.75rem',
                    borderRadius: '0.5rem',
                    border: '1px solid #d1d5db',
                    fontSize: '0.875rem'
                  }}
                  required
                />
              </div>

              {/* Time Range */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                    Start Time *
                  </label>
                  <input
                    type="time"
                    value={slotForm.startTime}
                    onChange={(e) => setSlotForm({ ...slotForm, startTime: e.target.value })}
                    style={{
                      width: '100%',
                      padding: '0.55rem 0.75rem',
                      borderRadius: '0.5rem',
                      border: '1px solid #d1d5db',
                      fontSize: '0.875rem'
                    }}
                    required
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                    End Time *
                  </label>
                  <input
                    type="time"
                    value={slotForm.endTime}
                    onChange={(e) => setSlotForm({ ...slotForm, endTime: e.target.value })}
                    style={{
                      width: '100%',
                      padding: '0.55rem 0.75rem',
                      borderRadius: '0.5rem',
                      border: '1px solid #d1d5db',
                      fontSize: '0.875rem'
                    }}
                    required
                  />
                </div>
              </div>

              {/* Location Branch */}
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                  Branch / Room Location
                </label>
                <input
                  type="text"
                  value={slotForm.branch}
                  onChange={(e) => setSlotForm({ ...slotForm, branch: e.target.value })}
                  placeholder="e.g. Consultation Room 1 / Main Clinic"
                  style={{
                    width: '100%',
                    padding: '0.55rem 0.75rem',
                    borderRadius: '0.5rem',
                    border: '1px solid #d1d5db',
                    fontSize: '0.875rem'
                  }}
                />
              </div>

              {/* Buttons */}
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '1rem' }}>
                <button
                  type="button"
                  onClick={() => setModalOpen(false)}
                  style={{
                    padding: '0.55rem 1rem',
                    borderRadius: '0.5rem',
                    border: '1px solid #d1d5db',
                    backgroundColor: '#fff',
                    color: '#374151',
                    fontSize: '0.875rem',
                    fontWeight: '500',
                    cursor: 'pointer'
                  }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  style={{
                    padding: '0.55rem 1.25rem',
                    borderRadius: '0.5rem',
                    border: 'none',
                    backgroundColor: '#0d9488',
                    color: '#fff',
                    fontSize: '0.875rem',
                    fontWeight: '600',
                    cursor: submitting ? 'not-allowed' : 'pointer',
                    opacity: submitting ? 0.7 : 1
                  }}
                >
                  {submitting ? 'Creating...' : 'Save Availability Slot'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
