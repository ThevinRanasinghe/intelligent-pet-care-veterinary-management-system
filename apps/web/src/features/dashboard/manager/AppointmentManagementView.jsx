import { useState, useEffect, useCallback } from 'react';
import {
  Calendar, Clock, Search, Filter, Plus, CheckCircle,
  AlertCircle, Stethoscope, User, DollarSign, X, Eye, Ban, RefreshCw
} from 'lucide-react';
import {
  getAppointments,
  getOrganizationVeterinarians,
  getAvailableSlots,
  createAppointment,
  updateAppointmentStatus,
  cancelAppointment,
  checkScheduleConflict
} from '../services/adminApi';

export default function AppointmentManagementView() {
  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  // Filters
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [dateFilter, setDateFilter] = useState('');

  // Detail Modal
  const [selectedAppt, setSelectedAppt] = useState(null);
  const [statusUpdating, setStatusUpdating] = useState(false);

  // Book Modal
  const [bookModalOpen, setBookModalOpen] = useState(false);
  const [veterinarians, setVeterinarians] = useState([]);
  const [availableSlots, setAvailableSlots] = useState([]);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [bookForm, setBookForm] = useState({
    petId: '',
    petName: '',
    ownerName: '',
    veterinarianId: '',
    appointmentSlotId: '',
    date: new Date().toISOString().split('T')[0],
    startTime: '10:00',
    endTime: '10:30',
    notes: ''
  });
  const [bookSubmitting, setBookSubmitting] = useState(false);
  const [bookError, setBookError] = useState('');
  const [hasConflict, setHasConflict] = useState(false);

  const fetchAppointmentsList = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getAppointments({
        search: searchQuery || undefined,
        status: statusFilter || undefined,
        date: dateFilter || undefined
      });
      setAppointments(data);
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to load appointments.');
    } finally {
      setLoading(false);
    }
  }, [searchQuery, statusFilter, dateFilter]);

  useEffect(() => {
    fetchAppointmentsList();
  }, [fetchAppointmentsList]);

  // Load vets for booking
  const handleOpenBookModal = async () => {
    setBookError('');
    setHasConflict(false);
    try {
      const vets = await getOrganizationVeterinarians();
      setVeterinarians(vets);
      const defaultVetId = vets[0]?.id || '';
      setBookForm({
        petId: '00000000-0000-0000-0000-000000000000', // Default / will create or link
        petName: '',
        ownerName: '',
        veterinarianId: defaultVetId,
        appointmentSlotId: '',
        date: new Date().toISOString().split('T')[0],
        startTime: '10:00',
        endTime: '10:30',
        notes: ''
      });
      if (defaultVetId) {
        loadAvailableSlotsForBooking(defaultVetId, new Date().toISOString().split('T')[0]);
      }
      setBookModalOpen(true);
    } catch (err) {
      alert('Failed to load clinic veterinarians.');
    }
  };

  const loadAvailableSlotsForBooking = async (vetId, date) => {
    if (!vetId || !date) return;
    setSlotsLoading(true);
    try {
      const slots = await getAvailableSlots({ veterinarianId: vetId, date });
      setAvailableSlots(slots);
      if (slots.length > 0) {
        setBookForm(prev => ({
          ...prev,
          appointmentSlotId: slots[0].id,
          startTime: slots[0].startTime,
          endTime: slots[0].endTime
        }));
      }
    } catch (err) {
      console.error(err);
    } finally {
      setSlotsLoading(false);
    }
  };

  const handleVetChangeInBooking = (vetId) => {
    setBookForm(prev => ({ ...prev, veterinarianId: vetId, appointmentSlotId: '' }));
    loadAvailableSlotsForBooking(vetId, bookForm.date);
  };

  const handleDateChangeInBooking = (date) => {
    setBookForm(prev => ({ ...prev, date, appointmentSlotId: '' }));
    loadAvailableSlotsForBooking(bookForm.veterinarianId, date);
  };

  const handleSlotSelect = (slotId) => {
    const slot = availableSlots.find(s => s.id === slotId);
    if (slot) {
      setBookForm(prev => ({
        ...prev,
        appointmentSlotId: slot.id,
        startTime: slot.startTime,
        endTime: slot.endTime
      }));
      setHasConflict(false);
    }
  };

  const handleCheckConflict = async () => {
    if (!bookForm.veterinarianId || !bookForm.date || !bookForm.startTime || !bookForm.endTime) return;
    try {
      const conflict = await checkScheduleConflict({
        veterinarianId: bookForm.veterinarianId,
        date: bookForm.date,
        startTime: bookForm.startTime.length === 5 ? bookForm.startTime + ':00' : bookForm.startTime,
        endTime: bookForm.endTime.length === 5 ? bookForm.endTime + ':00' : bookForm.endTime
      });
      setHasConflict(conflict);
    } catch (err) {
      console.error(err);
    }
  };

  const handleCreateAppointmentSubmit = async (e) => {
    e.preventDefault();
    setBookError('');

    if (!bookForm.appointmentSlotId) {
      setBookError('Please select an open time slot for the veterinarian.');
      return;
    }

    setBookSubmitting(true);
    try {
      await createAppointment({
        petId: bookForm.petId === '00000000-0000-0000-0000-000000000000' ? '11111111-1111-1111-1111-111111111111' : bookForm.petId,
        veterinarianId: bookForm.veterinarianId,
        appointmentSlotId: bookForm.appointmentSlotId,
        date: bookForm.date,
        startTime: bookForm.startTime.length === 5 ? bookForm.startTime + ':00' : bookForm.startTime,
        endTime: bookForm.endTime.length === 5 ? bookForm.endTime + ':00' : bookForm.endTime,
        notes: bookForm.notes
      });
      setSuccessMsg('Appointment booked successfully.');
      setBookModalOpen(false);
      fetchAppointmentsList();
      setTimeout(() => setSuccessMsg(''), 4000);
    } catch (err) {
      setBookError(err.response?.data?.message || 'Failed to book appointment. Please check for scheduling conflicts.');
    } finally {
      setBookSubmitting(false);
    }
  };

  const handleStatusChange = async (apptId, newStatus) => {
    setStatusUpdating(true);
    try {
      await updateAppointmentStatus(apptId, newStatus);
      setSuccessMsg(`Appointment status updated to ${newStatus}.`);
      fetchAppointmentsList();
      if (selectedAppt && selectedAppt.id === apptId) {
        setSelectedAppt(prev => ({ ...prev, status: newStatus }));
      }
      setTimeout(() => setSuccessMsg(''), 4000);
    } catch (err) {
      alert(err.response?.data?.message || 'Failed to update appointment status.');
    } finally {
      setStatusUpdating(false);
    }
  };

  const handleCancel = async (apptId) => {
    if (!window.confirm('Are you sure you want to cancel this appointment? The time slot will be reopened.')) return;
    try {
      await cancelAppointment(apptId);
      setSuccessMsg('Appointment cancelled.');
      fetchAppointmentsList();
      if (selectedAppt && selectedAppt.id === apptId) {
        setSelectedAppt(null);
      }
      setTimeout(() => setSuccessMsg(''), 4000);
    } catch (err) {
      alert(err.response?.data?.message || 'Failed to cancel appointment.');
    }
  };

  const getStatusBadge = (status) => {
    switch (status) {
      case 'Confirmed':
        return { bg: '#dcfce7', text: '#15803d' };
      case 'Scheduled':
        return { bg: '#e0f2fe', text: '#0369a1' };
      case 'InProgress':
        return { bg: '#fef3c7', text: '#b45309' };
      case 'Completed':
        return { bg: '#e0e7ff', text: '#4338ca' };
      case 'Cancelled':
        return { bg: '#fee2e2', text: '#b91c1c' };
      default:
        return { bg: '#f3f4f6', text: '#4b5563' };
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Top Filter and Search Bar */}
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
        <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '0.875rem', flex: 1 }}>
          {/* Search Input */}
          <div style={{ position: 'relative', minWidth: '240px', flex: '1 1 240px' }}>
            <Search size={16} style={{ position: 'absolute', left: '0.75rem', top: '50%', transform: 'translateY(-50%)', color: '#9ca3af' }} />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search by pet, owner, or vet..."
              style={{
                width: '100%',
                padding: '0.55rem 0.75rem 0.55rem 2.25rem',
                borderRadius: '0.5rem',
                border: '1px solid #d1d5db',
                fontSize: '0.875rem',
                outline: 'none'
              }}
            />
          </div>

          {/* Status Dropdown */}
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            style={{
              padding: '0.55rem 0.75rem',
              borderRadius: '0.5rem',
              border: '1px solid #d1d5db',
              fontSize: '0.875rem',
              backgroundColor: '#fff',
              color: '#374151'
            }}
          >
            <option value="">All Statuses</option>
            <option value="Scheduled">Scheduled</option>
            <option value="Confirmed">Confirmed</option>
            <option value="InProgress">In Progress</option>
            <option value="Completed">Completed</option>
            <option value="Cancelled">Cancelled</option>
          </select>

          {/* Date Filter */}
          <input
            type="date"
            value={dateFilter}
            onChange={(e) => setDateFilter(e.target.value)}
            style={{
              padding: '0.55rem 0.75rem',
              borderRadius: '0.5rem',
              border: '1px solid #d1d5db',
              fontSize: '0.875rem',
              backgroundColor: '#fff',
              color: '#374151'
            }}
          />

          {(searchQuery || statusFilter || dateFilter) && (
            <button
              onClick={() => {
                setSearchQuery('');
                setStatusFilter('');
                setDateFilter('');
              }}
              style={{
                background: 'none',
                border: 'none',
                color: '#6b7280',
                fontSize: '0.8rem',
                cursor: 'pointer',
                textDecoration: 'underline'
              }}
            >
              Reset filters
            </button>
          )}
        </div>

        {/* Action Buttons */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <button
            onClick={fetchAppointmentsList}
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
            <RefreshCw size={16} className={loading ? 'animate-spin' : ''} style={{ color: '#4b5563' }} />
          </button>

          <button
            onClick={handleOpenBookModal}
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
            <Plus size={16} /> Book Appointment
          </button>
        </div>
      </div>

      {/* Notifications */}
      {successMsg && (
        <div style={{ padding: '0.875rem 1rem', backgroundColor: '#ecfdf5', border: '1px solid #a7f3d0', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#065f46', fontSize: '0.875rem' }}>
          <CheckCircle size={18} />
          <span>{successMsg}</span>
        </div>
      )}
      {error && (
        <div style={{ padding: '0.875rem 1rem', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#991b1b', fontSize: '0.875rem' }}>
          <AlertCircle size={18} />
          <span>{error}</span>
        </div>
      )}

      {/* Appointments Table */}
      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.875rem', border: '1px solid #e5e7eb', overflow: 'hidden' }}>
        <div style={{ padding: '1.25rem', borderBottom: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h3 style={{ fontSize: '1rem', fontWeight: '600', color: '#111827', margin: 0 }}>
            Clinic Appointments ({appointments.length})
          </h3>
          <span style={{ fontSize: '0.8rem', color: '#6b7280' }}>
            Multi-tenant isolated for your clinic
          </span>
        </div>

        {loading ? (
          <div style={{ padding: '3.5rem', textAlign: 'center', color: '#6b7280' }}>
            <RefreshCw className="animate-spin" size={24} style={{ color: '#0d9488', margin: '0 auto 0.5rem' }} />
            <p style={{ margin: 0, fontSize: '0.875rem' }}>Loading clinic appointments...</p>
          </div>
        ) : appointments.length === 0 ? (
          <div style={{ padding: '3.5rem 2rem', textAlign: 'center', color: '#6b7280' }}>
            <Calendar size={36} style={{ color: '#9ca3af', margin: '0 auto 0.75rem' }} />
            <h4 style={{ fontWeight: '600', color: '#374151', margin: '0 0 0.25rem' }}>No appointments match your filter</h4>
            <p style={{ fontSize: '0.85rem', color: '#9ca3af', margin: '0 0 1.25rem' }}>
              Create an appointment directly or review proposals under the Approvals tab.
            </p>
            <button
              onClick={handleOpenBookModal}
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
              + Book New Appointment
            </button>
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
              <thead>
                <tr style={{ backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Pet & Owner</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Veterinarian</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Date & Time</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Status</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Quotation</th>
                  <th style={{ padding: '0.75rem 1.25rem', textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {appointments.map((appt) => {
                  const badge = getStatusBadge(appt.status);
                  return (
                    <tr key={appt.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <div>
                          <div style={{ fontWeight: '600', color: '#111827' }}>{appt.petName}</div>
                          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Owner: {appt.ownerName}</div>
                        </div>
                      </td>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', color: '#374151', fontWeight: '500' }}>
                          <Stethoscope size={14} style={{ color: '#0d9488' }} />
                          <span>{appt.veterinarianName}</span>
                        </div>
                      </td>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <div>
                          <div style={{ fontWeight: '500', color: '#111827' }}>{appt.date}</div>
                          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>
                            {appt.startTime} – {appt.endTime}
                          </div>
                        </div>
                      </td>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <span style={{
                          padding: '0.25rem 0.65rem',
                          borderRadius: '9999px',
                          backgroundColor: badge.bg,
                          color: badge.text,
                          fontSize: '0.75rem',
                          fontWeight: '600'
                        }}>
                          {appt.status}
                        </span>
                      </td>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        {appt.quotationTotal != null ? (
                          <span style={{ fontWeight: '600', color: '#059669' }}>
                            ${appt.quotationTotal.toFixed(2)}
                          </span>
                        ) : (
                          <span style={{ color: '#9ca3af', fontSize: '0.8rem' }}>Pending</span>
                        )}
                      </td>
                      <td style={{ padding: '1rem 1.25rem', textAlign: 'right' }}>
                        <div style={{ display: 'inline-flex', alignItems: 'center', gap: '0.5rem' }}>
                          <button
                            onClick={() => setSelectedAppt(appt)}
                            title="View Details"
                            style={{
                              padding: '0.35rem 0.65rem',
                              borderRadius: '0.375rem',
                              border: '1px solid #d1d5db',
                              backgroundColor: '#fff',
                              color: '#374151',
                              fontSize: '0.75rem',
                              cursor: 'pointer',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '0.25rem'
                            }}
                          >
                            <Eye size={13} /> View
                          </button>
                          {appt.status !== 'Cancelled' && appt.status !== 'Completed' && (
                            <button
                              onClick={() => handleCancel(appt.id)}
                              title="Cancel Appointment"
                              style={{
                                padding: '0.35rem 0.65rem',
                                borderRadius: '0.375rem',
                                border: '1px solid #fee2e2',
                                backgroundColor: '#fff',
                                color: '#dc2626',
                                fontSize: '0.75rem',
                                cursor: 'pointer',
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: '0.25rem'
                              }}
                            >
                              <Ban size={13} /> Cancel
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Appointment Details Modal */}
      {selectedAppt && (
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
            maxWidth: '560px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)',
            overflow: 'hidden'
          }}>
            <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Calendar size={20} style={{ color: '#0d9488' }} />
                <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', margin: 0 }}>
                  Appointment Details
                </h3>
              </div>
              <button
                onClick={() => setSelectedAppt(null)}
                style={{ background: 'none', border: 'none', color: '#6b7280', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <div style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem', backgroundColor: '#f9fafb', padding: '1rem', borderRadius: '0.5rem' }}>
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Pet</div>
                  <div style={{ fontSize: '1rem', fontWeight: '700', color: '#111827', marginTop: '0.15rem' }}>{selectedAppt.petName}</div>
                </div>
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Pet Owner</div>
                  <div style={{ fontSize: '1rem', fontWeight: '700', color: '#111827', marginTop: '0.15rem' }}>{selectedAppt.ownerName}</div>
                </div>
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Veterinarian</div>
                  <div style={{ fontSize: '0.95rem', fontWeight: '600', color: '#0d9488', marginTop: '0.15rem' }}>{selectedAppt.veterinarianName}</div>
                </div>
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Status</div>
                  <div style={{ marginTop: '0.25rem' }}>
                    <span style={{
                      padding: '0.2rem 0.6rem',
                      borderRadius: '9999px',
                      backgroundColor: getStatusBadge(selectedAppt.status).bg,
                      color: getStatusBadge(selectedAppt.status).text,
                      fontSize: '0.75rem',
                      fontWeight: '700'
                    }}>
                      {selectedAppt.status}
                    </span>
                  </div>
                </div>
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Date & Time</div>
                  <div style={{ fontSize: '0.9rem', color: '#374151', marginTop: '0.15rem' }}>
                    {selectedAppt.date} ({selectedAppt.startTime} – {selectedAppt.endTime})
                  </div>
                </div>
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Quotation Total</div>
                  <div style={{ fontSize: '1rem', fontWeight: '700', color: '#059669', marginTop: '0.15rem' }}>
                    {selectedAppt.quotationTotal != null ? `$${selectedAppt.quotationTotal.toFixed(2)}` : 'N/A'}
                  </div>
                </div>
              </div>

              {selectedAppt.notes && (
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600', marginBottom: '0.25rem' }}>Notes & Instructions</div>
                  <div style={{ padding: '0.75rem', backgroundColor: '#f9fafb', borderRadius: '0.5rem', fontSize: '0.875rem', color: '#4b5563' }}>
                    {selectedAppt.notes}
                  </div>
                </div>
              )}

              {/* Status Transition Actions */}
              <div>
                <div style={{ fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.5rem' }}>
                  Update Appointment Lifecycle Status:
                </div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
                  {selectedAppt.status !== 'Confirmed' && selectedAppt.status !== 'Cancelled' && (
                    <button
                      disabled={statusUpdating}
                      onClick={() => handleStatusChange(selectedAppt.id, 'Confirmed')}
                      style={{
                        padding: '0.45rem 0.85rem',
                        borderRadius: '0.375rem',
                        backgroundColor: '#dcfce7',
                        color: '#15803d',
                        border: '1px solid #bbf7d0',
                        fontSize: '0.8rem',
                        fontWeight: '600',
                        cursor: 'pointer'
                      }}
                    >
                      Confirm Appointment
                    </button>
                  )}
                  {selectedAppt.status !== 'InProgress' && selectedAppt.status !== 'Cancelled' && selectedAppt.status !== 'Completed' && (
                    <button
                      disabled={statusUpdating}
                      onClick={() => handleStatusChange(selectedAppt.id, 'InProgress')}
                      style={{
                        padding: '0.45rem 0.85rem',
                        borderRadius: '0.375rem',
                        backgroundColor: '#fef3c7',
                        color: '#b45309',
                        border: '1px solid #fde68a',
                        fontSize: '0.8rem',
                        fontWeight: '600',
                        cursor: 'pointer'
                      }}
                    >
                      Mark In-Progress
                    </button>
                  )}
                  {selectedAppt.status !== 'Completed' && selectedAppt.status !== 'Cancelled' && (
                    <button
                      disabled={statusUpdating}
                      onClick={() => handleStatusChange(selectedAppt.id, 'Completed')}
                      style={{
                        padding: '0.45rem 0.85rem',
                        borderRadius: '0.375rem',
                        backgroundColor: '#e0e7ff',
                        color: '#4338ca',
                        border: '1px solid #c7d2fe',
                        fontSize: '0.8rem',
                        fontWeight: '600',
                        cursor: 'pointer'
                      }}
                    >
                      Mark Completed
                    </button>
                  )}
                  {selectedAppt.status !== 'Cancelled' && (
                    <button
                      disabled={statusUpdating}
                      onClick={() => handleCancel(selectedAppt.id)}
                      style={{
                        padding: '0.45rem 0.85rem',
                        borderRadius: '0.375rem',
                        backgroundColor: '#fee2e2',
                        color: '#dc2626',
                        border: '1px solid #fecaca',
                        fontSize: '0.8rem',
                        fontWeight: '600',
                        cursor: 'pointer'
                      }}
                    >
                      Cancel Appointment
                    </button>
                  )}
                </div>
              </div>
            </div>

            <div style={{ padding: '1rem 1.5rem', borderTop: '1px solid #e5e7eb', display: 'flex', justifyContent: 'flex-end' }}>
              <button
                onClick={() => setSelectedAppt(null)}
                style={{
                  padding: '0.5rem 1rem',
                  borderRadius: '0.5rem',
                  border: '1px solid #d1d5db',
                  backgroundColor: '#fff',
                  color: '#374151',
                  fontSize: '0.875rem',
                  cursor: 'pointer'
                }}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Book Appointment Modal */}
      {bookModalOpen && (
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
            maxWidth: '540px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)',
            overflow: 'hidden'
          }}>
            <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Calendar size={20} style={{ color: '#0d9488' }} />
                <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', margin: 0 }}>
                  Book Clinic Appointment
                </h3>
              </div>
              <button
                onClick={() => setBookModalOpen(false)}
                style={{ background: 'none', border: 'none', color: '#6b7280', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleCreateAppointmentSubmit} style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              {bookError && (
                <div style={{ padding: '0.75rem', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '0.5rem', color: '#b91c1c', fontSize: '0.85rem' }}>
                  {bookError}
                </div>
              )}

              {/* Veterinarian Selection */}
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                  Select Veterinarian *
                </label>
                <select
                  value={bookForm.veterinarianId}
                  onChange={(e) => handleVetChangeInBooking(e.target.value)}
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
                      {v.name} ({v.specialisation})
                    </option>
                  ))}
                </select>
              </div>

              {/* Date */}
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                  Appointment Date *
                </label>
                <input
                  type="date"
                  value={bookForm.date}
                  onChange={(e) => handleDateChangeInBooking(e.target.value)}
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

              {/* Slot Selection */}
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.375rem' }}>
                  <label style={{ fontSize: '0.8rem', fontWeight: '600', color: '#374151' }}>
                    Available Doctor Slot *
                  </label>
                  {slotsLoading && <span style={{ fontSize: '0.75rem', color: '#0d9488' }}>Loading slots...</span>}
                </div>

                {availableSlots.length === 0 ? (
                  <div style={{ padding: '0.75rem', backgroundColor: '#fffbeb', border: '1px solid #fde68a', borderRadius: '0.5rem', fontSize: '0.85rem', color: '#b45309' }}>
                    No open availability slots found for this doctor on the selected date. Please switch to the "Veterinarian Schedules" tab to define open slots first.
                  </div>
                ) : (
                  <select
                    value={bookForm.appointmentSlotId}
                    onChange={(e) => handleSlotSelect(e.target.value)}
                    style={{
                      width: '100%',
                      padding: '0.55rem 0.75rem',
                      borderRadius: '0.5rem',
                      border: '1px solid #d1d5db',
                      fontSize: '0.875rem'
                    }}
                    required
                  >
                    <option value="">Select Time Slot...</option>
                    {availableSlots.map(s => (
                      <option key={s.id} value={s.id}>
                        {s.startTime} – {s.endTime} ({s.branch || 'Main Clinic'})
                      </option>
                    ))}
                  </select>
                )}
              </div>

              {/* Conflict Check Status */}
              {hasConflict && (
                <div style={{ padding: '0.5rem 0.75rem', backgroundColor: '#fee2e2', border: '1px solid #fecaca', borderRadius: '0.375rem', color: '#dc2626', fontSize: '0.8rem', display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                  <AlertCircle size={14} /> Schedule conflict detected for this veterinarian!
                </div>
              )}

              {/* Notes */}
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                  Clinical / Consultation Notes
                </label>
                <textarea
                  value={bookForm.notes}
                  onChange={(e) => setBookForm({ ...bookForm, notes: e.target.value })}
                  rows={3}
                  placeholder="Reason for visit, symptoms, or special instructions..."
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
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '0.5rem' }}>
                <button
                  type="button"
                  onClick={() => setBookModalOpen(false)}
                  style={{
                    padding: '0.55rem 1rem',
                    borderRadius: '0.5rem',
                    border: '1px solid #d1d5db',
                    backgroundColor: '#fff',
                    color: '#374151',
                    fontSize: '0.875rem',
                    cursor: 'pointer'
                  }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={bookSubmitting || availableSlots.length === 0}
                  style={{
                    padding: '0.55rem 1.25rem',
                    borderRadius: '0.5rem',
                    border: 'none',
                    backgroundColor: '#0d9488',
                    color: '#fff',
                    fontSize: '0.875rem',
                    fontWeight: '600',
                    cursor: bookSubmitting || availableSlots.length === 0 ? 'not-allowed' : 'pointer',
                    opacity: bookSubmitting || availableSlots.length === 0 ? 0.6 : 1
                  }}
                >
                  {bookSubmitting ? 'Booking...' : 'Confirm Appointment'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
