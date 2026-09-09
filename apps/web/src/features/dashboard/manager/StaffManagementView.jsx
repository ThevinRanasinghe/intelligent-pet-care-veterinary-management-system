import { useState, useEffect, useCallback } from 'react';
import {
  Users, UserPlus, Search, RefreshCw, Shield, AlertCircle, CheckCircle,
  Stethoscope, Package, Mail, Phone, Calendar, Power
} from 'lucide-react';
import {
  getOrganizationStaff, createStaffMember, updateStaffStatus
} from '../services/adminApi';

export default function StaffManagementView() {
  const [staffList, setStaffList] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedRole, setSelectedRole] = useState('');
  const [selectedStatus, setSelectedStatus] = useState('');

  // Add Staff Modal state
  const [addModalOpen, setAddModalOpen] = useState(false);
  const [addForm, setAddForm] = useState({
    firstName: '',
    lastName: '',
    email: '',
    phoneNumber: '',
    role: 'Veterinarian',
    password: '',
    confirmPassword: ''
  });
  const [modalErrors, setModalErrors] = useState({});
  const [actionLoading, setActionLoading] = useState(false);

  const fetchStaff = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getOrganizationStaff({
        search: searchQuery,
        role: selectedRole,
        status: selectedStatus
      });
      setStaffList(data);
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to load staff list.');
    } finally {
      setLoading(false);
    }
  }, [searchQuery, selectedRole, selectedStatus]);

  useEffect(() => {
    fetchStaff();
  }, [fetchStaff]);

  const handleToggleStatus = async (staff) => {
    const isCurrentlyActive = staff.status === 'Active';
    const newStatus = isCurrentlyActive ? 'Disabled' : 'Active';
    const actionName = isCurrentlyActive ? 'disable' : 'activate';

    if (!window.confirm(`Are you sure you want to ${actionName} ${staff.fullName}'s account?`)) {
      return;
    }

    setActionLoading(true);
    setError('');
    setSuccessMsg('');
    try {
      await updateStaffStatus(staff.id, newStatus);
      setSuccessMsg(`${staff.fullName}'s account is now ${newStatus.toLowerCase()}.`);
      await fetchStaff();
    } catch (err) {
      setError(err.response?.data?.message || err.message || `Failed to ${actionName} staff member.`);
    } finally {
      setActionLoading(false);
    }
  };

  const validateModalForm = () => {
    const errs = {};
    if (!addForm.firstName.trim()) errs.firstName = 'First name is required.';
    if (!addForm.lastName.trim()) errs.lastName = 'Last name is required.';
    if (!addForm.email.trim()) {
      errs.email = 'Email is required.';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(addForm.email)) {
      errs.email = 'Valid email is required.';
    }
    if (!addForm.password) {
      errs.password = 'Temporary password is required.';
    } else if (addForm.password.length < 8) {
      errs.password = 'Password must be at least 8 characters.';
    }
    if (addForm.confirmPassword !== addForm.password) {
      errs.confirmPassword = 'Passwords do not match.';
    }
    return errs;
  };

  const handleCreateStaff = async (e) => {
    e.preventDefault();
    const errs = validateModalForm();
    if (Object.keys(errs).length > 0) {
      setModalErrors(errs);
      return;
    }

    setActionLoading(true);
    setModalErrors({});
    setError('');
    setSuccessMsg('');
    try {
      await createStaffMember({
        firstName:   addForm.firstName.trim(),
        lastName:    addForm.lastName.trim(),
        email:       addForm.email.trim(),
        phoneNumber: addForm.phoneNumber.trim() || null,
        role:        addForm.role,
        password:    addForm.password
      });

      setSuccessMsg(`New staff member ${addForm.firstName} ${addForm.lastName} (${addForm.role}) created successfully.`);
      setAddModalOpen(false);
      setAddForm({
        firstName: '',
        lastName: '',
        email: '',
        phoneNumber: '',
        role: 'Veterinarian',
        password: '',
        confirmPassword: ''
      });
      await fetchStaff();
    } catch (err) {
      setModalErrors({ api: err.response?.data?.message || err.message || 'Failed to create staff member.' });
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <div>
      {/* Header and Add Action */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem', marginBottom: '1.5rem' }}>
        <div>
          <h2 style={{ fontSize: '1.25rem', fontWeight: '700', color: '#111827', margin: 0 }}>
            Clinic Staff & Team
          </h2>
          <p style={{ fontSize: '0.85rem', color: '#6B7280', marginTop: '0.25rem' }}>
            Manage veterinarians, inventory officers, credentials, and access for your organization.
          </p>
        </div>

        <button
          onClick={() => { setModalErrors({}); setAddModalOpen(true); }}
          style={{
            padding: '0.6rem 1.25rem',
            backgroundColor: 'var(--petcare-black, #111111)',
            color: '#FFFFFF',
            border: 'none',
            borderRadius: '0.5rem',
            fontWeight: '600',
            fontSize: '0.875rem',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '0.5rem',
            boxShadow: '0 2px 4px rgba(0,0,0,0.1)'
          }}
        >
          <UserPlus size={16} />
          Add Staff Member
        </button>
      </div>

      {/* Notifications */}
      {successMsg && (
        <div className="alert alert-success" style={{ marginBottom: '1rem' }}>
          <CheckCircle size={18} />
          <span>{successMsg}</span>
        </div>
      )}
      {error && (
        <div className="alert alert-error" style={{ marginBottom: '1rem' }}>
          <AlertCircle size={18} />
          <span>{error}</span>
        </div>
      )}

      {/* Search & Filters */}
      <div style={{
        display: 'flex', flexWrap: 'wrap', gap: '0.75rem', alignItems: 'center',
        backgroundColor: '#FFFFFF', padding: '1rem', borderRadius: '0.75rem',
        border: '1px solid #E5E7EB', marginBottom: '1.5rem'
      }}>
        <div style={{ position: 'relative', flex: '1 1 240px' }}>
          <Search size={16} style={{ position: 'absolute', left: '0.75rem', top: '50%', transform: 'translateY(-50%)', color: '#9CA3AF' }} />
          <input
            type="text"
            placeholder="Search by staff name or email..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{
              width: '100%', padding: '0.5rem 0.75rem 0.5rem 2.25rem',
              fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB'
            }}
          />
        </div>

        <select
          value={selectedRole}
          onChange={(e) => setSelectedRole(e.target.value)}
          style={{ padding: '0.5rem 0.75rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB', backgroundColor: '#FFFFFF' }}
        >
          <option value="">All Roles</option>
          <option value="Veterinarian">Veterinarian</option>
          <option value="InventoryOfficer">Inventory Officer</option>
          <option value="ClinicManager">Clinic Manager</option>
        </select>

        <select
          value={selectedStatus}
          onChange={(e) => setSelectedStatus(e.target.value)}
          style={{ padding: '0.5rem 0.75rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB', backgroundColor: '#FFFFFF' }}
        >
          <option value="">All Statuses</option>
          <option value="Active">Active</option>
          <option value="Disabled">Disabled</option>
          <option value="Pending">Pending</option>
        </select>

        <button
          onClick={fetchStaff}
          disabled={loading}
          style={{
            padding: '0.5rem 0.75rem',
            backgroundColor: '#F3F4F6',
            border: '1px solid #D1D5DB',
            borderRadius: '0.375rem',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '0.35rem',
            fontSize: '0.875rem',
            color: '#374151'
          }}
        >
          <RefreshCw size={14} className={loading ? 'animate-spin' : ''} />
          Refresh
        </button>
      </div>

      {/* Staff Table */}
      {loading ? (
        <div style={{ padding: '3rem', textAlign: 'center', color: '#6B7280' }}>
          <RefreshCw size={28} className="animate-spin" style={{ margin: '0 auto 0.75rem auto' }} />
          <p>Loading staff members...</p>
        </div>
      ) : staffList.length === 0 ? (
        <div style={{ backgroundColor: '#FFFFFF', border: '1px dashed #D1D5DB', borderRadius: '0.75rem', padding: '3rem', textAlign: 'center' }}>
          <Users size={36} style={{ color: '#9CA3AF', margin: '0 auto 0.5rem auto' }} />
          <h4 style={{ fontSize: '1rem', fontWeight: '600', color: '#374151' }}>No staff members found</h4>
          <p style={{ fontSize: '0.875rem', color: '#6B7280', marginTop: '0.25rem' }}>
            {searchQuery ? `No staff match "${searchQuery}".` : 'Get started by creating Veterinarian or Inventory Officer accounts.'}
          </p>
        </div>
      ) : (
        <div style={{ backgroundColor: '#FFFFFF', borderRadius: '0.75rem', border: '1px solid #E5E7EB', overflow: 'hidden' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
            <thead style={{ backgroundColor: '#F9FAFB', borderBottom: '1px solid #E5E7EB', color: '#4B5563', fontSize: '0.75rem', textTransform: 'uppercase' }}>
              <tr>
                <th style={{ padding: '0.75rem 1rem' }}>Staff Member</th>
                <th style={{ padding: '0.75rem 1rem' }}>Role</th>
                <th style={{ padding: '0.75rem 1rem' }}>Contact</th>
                <th style={{ padding: '0.75rem 1rem' }}>Status</th>
                <th style={{ padding: '0.75rem 1rem' }}>Joined</th>
                <th style={{ padding: '0.75rem 1rem', textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {staffList.map((member) => {
                const initials = `${member.firstName?.[0] || ''}${member.lastName?.[0] || ''}`.toUpperCase();
                return (
                  <tr key={member.id} style={{ borderBottom: '1px solid #F3F4F6' }}>
                    <td style={{ padding: '0.85rem 1rem' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                        <div style={{
                          width: '36px', height: '36px', borderRadius: '9999px',
                          backgroundColor: '#E0E7FF', color: '#4F46E5', display: 'flex',
                          alignItems: 'center', justifyContent: 'center', fontWeight: '700', fontSize: '0.85rem'
                        }}>
                          {initials}
                        </div>
                        <div>
                          <div style={{ fontWeight: '600', color: '#111827' }}>{member.fullName}</div>
                          <div style={{ fontSize: '0.75rem', color: '#6B7280' }}>ID: {member.id.substring(0, 8)}…</div>
                        </div>
                      </div>
                    </td>
                    <td style={{ padding: '0.85rem 1rem' }}>
                      {member.role === 'Veterinarian' && (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem', backgroundColor: '#EFF6FF', color: '#1D4ED8', padding: '0.2rem 0.5rem', borderRadius: '4px', fontWeight: '600', fontSize: '0.75rem' }}>
                          <Stethoscope size={13} /> Veterinarian
                        </span>
                      )}
                      {member.role === 'InventoryOfficer' && (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem', backgroundColor: '#F5F3FF', color: '#6D28D9', padding: '0.2rem 0.5rem', borderRadius: '4px', fontWeight: '600', fontSize: '0.75rem' }}>
                          <Package size={13} /> Inventory Officer
                        </span>
                      )}
                      {member.role === 'ClinicManager' && (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem', backgroundColor: '#FEF3C7', color: '#92400E', padding: '0.2rem 0.5rem', borderRadius: '4px', fontWeight: '600', fontSize: '0.75rem' }}>
                          <Shield size={13} /> Clinic Manager
                        </span>
                      )}
                    </td>
                    <td style={{ padding: '0.85rem 1rem', color: '#4B5563' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
                        <Mail size={13} style={{ color: '#9CA3AF' }} />
                        {member.email}
                      </div>
                      {member.phoneNumber && (
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', marginTop: '0.2rem', fontSize: '0.8rem' }}>
                          <Phone size={13} style={{ color: '#9CA3AF' }} />
                          {member.phoneNumber}
                        </div>
                      )}
                    </td>
                    <td style={{ padding: '0.85rem 1rem' }}>
                      {member.status === 'Active' ? (
                        <span style={{ backgroundColor: '#D1FAE5', color: '#065F46', padding: '0.2rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: '600' }}>
                          ● Active
                        </span>
                      ) : (
                        <span style={{ backgroundColor: '#FEE2E2', color: '#991B1B', padding: '0.2rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: '600' }}>
                          ○ Disabled
                        </span>
                      )}
                    </td>
                    <td style={{ padding: '0.85rem 1rem', color: '#6B7280', fontSize: '0.8rem' }}>
                      {new Date(member.createdAt).toLocaleDateString()}
                    </td>
                    <td style={{ padding: '0.85rem 1rem', textAlign: 'right' }}>
                      {member.role !== 'ClinicManager' && (
                        <button
                          onClick={() => handleToggleStatus(member)}
                          disabled={actionLoading}
                          title={member.status === 'Active' ? 'Disable Account' : 'Activate Account'}
                          style={{
                            padding: '0.35rem 0.65rem',
                            fontSize: '0.75rem',
                            fontWeight: '600',
                            borderRadius: '0.375rem',
                            border: member.status === 'Active' ? '1px solid #DC2626' : '1px solid #10B981',
                            backgroundColor: member.status === 'Active' ? '#FEF2F2' : '#ECFDF5',
                            color: member.status === 'Active' ? '#DC2626' : '#059669',
                            cursor: 'pointer',
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '0.3rem'
                          }}
                        >
                          <Power size={13} />
                          {member.status === 'Active' ? 'Disable' : 'Activate'}
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {/* Add Staff Modal */}
      {addModalOpen && (
        <div style={{
          position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#FFFFFF', borderRadius: '0.75rem', maxWidth: '500px', width: '100%',
            padding: '1.75rem', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <h3 style={{ fontSize: '1.25rem', fontWeight: '700', color: '#111827', marginBottom: '0.25rem' }}>
              Add Organization Staff Member
            </h3>
            <p style={{ fontSize: '0.85rem', color: '#6B7280', marginBottom: '1.25rem' }}>
              Create an account for a Veterinarian or Inventory Officer in your clinic.
            </p>

            {modalErrors.api && (
              <div className="alert alert-error" style={{ marginBottom: '1rem', fontSize: '0.85rem' }}>
                <AlertCircle size={16} />
                <span>{modalErrors.api}</span>
              </div>
            )}

            <form onSubmit={handleCreateStaff}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginBottom: '0.75rem' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                    First Name *
                  </label>
                  <input
                    type="text"
                    required
                    placeholder="Jane"
                    value={addForm.firstName}
                    onChange={(e) => setAddForm({ ...addForm, firstName: e.target.value })}
                    style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                  />
                  {modalErrors.firstName && <span style={{ color: '#EF4444', fontSize: '0.75rem' }}>{modalErrors.firstName}</span>}
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                    Last Name *
                  </label>
                  <input
                    type="text"
                    required
                    placeholder="Smith"
                    value={addForm.lastName}
                    onChange={(e) => setAddForm({ ...addForm, lastName: e.target.value })}
                    style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                  />
                  {modalErrors.lastName && <span style={{ color: '#EF4444', fontSize: '0.75rem' }}>{modalErrors.lastName}</span>}
                </div>
              </div>

              <div style={{ marginBottom: '0.75rem' }}>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                  Role *
                </label>
                <select
                  value={addForm.role}
                  onChange={(e) => setAddForm({ ...addForm, role: e.target.value })}
                  style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB', backgroundColor: '#FFFFFF' }}
                >
                  <option value="Veterinarian">Veterinarian</option>
                  <option value="InventoryOfficer">Inventory Officer</option>
                </select>
                <span style={{ fontSize: '0.75rem', color: '#6B7280' }}>
                  Restricted to clinic operational roles only.
                </span>
              </div>

              <div style={{ marginBottom: '0.75rem' }}>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                  Login Email *
                </label>
                <input
                  type="email"
                  required
                  placeholder="jane.smith@clinic.com"
                  value={addForm.email}
                  onChange={(e) => setAddForm({ ...addForm, email: e.target.value })}
                  style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                />
                {modalErrors.email && <span style={{ color: '#EF4444', fontSize: '0.75rem' }}>{modalErrors.email}</span>}
              </div>

              <div style={{ marginBottom: '0.75rem' }}>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                  Phone Number (Optional)
                </label>
                <input
                  type="tel"
                  placeholder="+1 555-0182"
                  value={addForm.phoneNumber}
                  onChange={(e) => setAddForm({ ...addForm, phoneNumber: e.target.value })}
                  style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginBottom: '1.25rem' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                    Temporary Password *
                  </label>
                  <input
                    type="password"
                    required
                    placeholder="Min 8 chars, 1 digit, 1 symbol"
                    value={addForm.password}
                    onChange={(e) => setAddForm({ ...addForm, password: e.target.value })}
                    style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                  />
                  {modalErrors.password && <span style={{ color: '#EF4444', fontSize: '0.75rem' }}>{modalErrors.password}</span>}
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                    Confirm Password *
                  </label>
                  <input
                    type="password"
                    required
                    placeholder="Repeat password"
                    value={addForm.confirmPassword}
                    onChange={(e) => setAddForm({ ...addForm, confirmPassword: e.target.value })}
                    style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                  />
                  {modalErrors.confirmPassword && <span style={{ color: '#EF4444', fontSize: '0.75rem' }}>{modalErrors.confirmPassword}</span>}
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
                <button
                  type="button"
                  onClick={() => setAddModalOpen(false)}
                  style={{
                    padding: '0.5rem 1rem',
                    backgroundColor: '#FFFFFF',
                    border: '1px solid #D1D5DB',
                    borderRadius: '0.375rem',
                    fontSize: '0.875rem',
                    cursor: 'pointer'
                  }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={actionLoading}
                  style={{
                    padding: '0.5rem 1.25rem',
                    backgroundColor: 'var(--petcare-black, #111111)',
                    color: '#FFFFFF',
                    border: 'none',
                    borderRadius: '0.375rem',
                    fontWeight: '600',
                    fontSize: '0.875rem',
                    cursor: 'pointer'
                  }}
                >
                  {actionLoading ? 'Creating…' : 'Create Staff Account'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
