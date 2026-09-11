import { useState, useEffect, useCallback } from 'react';
import {
  Users, UserPlus, Search, RefreshCw, Shield, AlertCircle, CheckCircle,
  Stethoscope, Package, Mail, Phone, Calendar, Power, Key, Edit2, Eye, X, Sparkles
} from 'lucide-react';
import {
  getOrganizationStaff, createStaffMember, updateStaffMember,
  updateStaffStatus, resetStaffPassword, verifyStaffMember
} from '../services/adminApi';

function generateRandomPassword() {
  const letters = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz';
  const digits = '23456789';
  const symbols = '!@#$%^&*';
  let pass = '';
  for (let i = 0; i < 6; i++) pass += letters.charAt(Math.floor(Math.random() * letters.length));
  for (let i = 0; i < 2; i++) pass += digits.charAt(Math.floor(Math.random() * digits.length));
  for (let i = 0; i < 2; i++) pass += symbols.charAt(Math.floor(Math.random() * symbols.length));
  return pass;
}

export default function StaffManagementView({ isSuperAdmin = false, defaultTab = isSuperAdmin ? 'PENDING' : 'ALL' }) {
  const [staffList, setStaffList] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedRole, setSelectedRole] = useState('');
  const [selectedStatus, setSelectedStatus] = useState(defaultTab === 'PENDING' ? 'Pending' : '');
  const [activeTab, setActiveTab] = useState(defaultTab);

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
  const [addErrors, setAddErrors] = useState({});

  // View Details Modal state
  const [viewModalOpen, setViewModalOpen] = useState(false);
  const [selectedStaff, setSelectedStaff] = useState(null);

  // Edit Staff Modal state
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [editForm, setEditForm] = useState({ id: '', firstName: '', lastName: '', phoneNumber: '', email: '', role: '' });
  const [editErrors, setEditErrors] = useState({});

  // Reset Password Modal state
  const [resetModalOpen, setResetModalOpen] = useState(false);
  const [resetStaffTarget, setResetStaffTarget] = useState(null);
  const [resetPasswordValue, setResetPasswordValue] = useState('');
  const [resetErrors, setResetErrors] = useState({});

  const [actionLoading, setActionLoading] = useState(false);

  // Fetch staff list from backend
  const fetchStaff = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      let roleFilter = selectedRole;
      let statusFilter = selectedStatus;

      if (activeTab === 'VET') roleFilter = 'Veterinarian';
      else if (activeTab === 'INVENTORY') roleFilter = 'InventoryOfficer';
      else if (activeTab === 'PENDING') statusFilter = 'Pending';
      else if (activeTab === 'DISABLED') statusFilter = 'Disabled';

      const data = await getOrganizationStaff({
        search: searchQuery,
        role: roleFilter,
        status: statusFilter
      });
      setStaffList(data);
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to load staff list.');
    } finally {
      setLoading(false);
    }
  }, [searchQuery, selectedRole, selectedStatus, activeTab]);

  useEffect(() => {
    fetchStaff();
  }, [fetchStaff]);

  // Tab click handler
  const handleTabChange = (tab) => {
    setActiveTab(tab);
    if (tab === 'ALL') {
      setSelectedRole('');
      setSelectedStatus('');
    } else if (tab === 'VET') {
      setSelectedRole('Veterinarian');
      setSelectedStatus('');
    } else if (tab === 'INVENTORY') {
      setSelectedRole('InventoryOfficer');
      setSelectedStatus('');
    } else if (tab === 'PENDING') {
      setSelectedRole('');
      setSelectedStatus('Pending');
    } else if (tab === 'DISABLED') {
      setSelectedRole('');
      setSelectedStatus('Disabled');
    }
  };

  // Verify staff account
  const handleVerifyStaff = async (staff) => {
    setActionLoading(true);
    setError('');
    setSuccessMsg('');
    try {
      await verifyStaffMember(staff.id);
      setSuccessMsg(`Staff account for ${staff.fullName} verified and activated successfully.`);
      await fetchStaff();
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to verify staff member.');
    } finally {
      setActionLoading(false);
    }
  };

  // Toggle staff status
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

  // Validate Add form
  const validateAddForm = () => {
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

  // Create Staff
  const handleCreateStaff = async (e) => {
    e.preventDefault();
    const errs = validateAddForm();
    if (Object.keys(errs).length > 0) {
      setAddErrors(errs);
      return;
    }

    setActionLoading(true);
    setAddErrors({});
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

      setSuccessMsg(`New staff member ${addForm.firstName} ${addForm.lastName} (${addForm.role}) created successfully with temporary password.`);
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
      setAddErrors({ api: err.response?.data?.message || err.message || 'Failed to create staff member.' });
    } finally {
      setActionLoading(false);
    }
  };

  // Edit Staff
  const openEditModal = (staff) => {
    setEditForm({
      id: staff.id,
      firstName: staff.firstName,
      lastName: staff.lastName,
      phoneNumber: staff.phoneNumber || '',
      email: staff.email,
      role: staff.role
    });
    setEditErrors({});
    setEditModalOpen(true);
  };

  const handleUpdateStaff = async (e) => {
    e.preventDefault();
    if (!editForm.firstName.trim() || !editForm.lastName.trim()) {
      setEditErrors({ name: 'First name and last name are required.' });
      return;
    }

    setActionLoading(true);
    setEditErrors({});
    try {
      await updateStaffMember(editForm.id, {
        firstName: editForm.firstName.trim(),
        lastName: editForm.lastName.trim(),
        phoneNumber: editForm.phoneNumber.trim() || null
      });

      setSuccessMsg(`Updated profile for ${editForm.firstName} ${editForm.lastName}.`);
      setEditModalOpen(false);
      await fetchStaff();
    } catch (err) {
      setEditErrors({ api: err.response?.data?.message || err.message || 'Failed to update staff member.' });
    } finally {
      setActionLoading(false);
    }
  };

  // Reset Password
  const openResetModal = (staff) => {
    setResetStaffTarget(staff);
    setResetPasswordValue(generateRandomPassword());
    setResetErrors({});
    setResetModalOpen(true);
  };

  const handleResetPassword = async (e) => {
    e.preventDefault();
    if (!resetPasswordValue || resetPasswordValue.length < 8) {
      setResetErrors({ password: 'Password must be at least 8 characters long.' });
      return;
    }

    setActionLoading(true);
    setResetErrors({});
    try {
      await resetStaffPassword(resetStaffTarget.id, resetPasswordValue);
      setSuccessMsg(`Temporary password reset for ${resetStaffTarget.fullName}. MustChangePassword flag set to true.`);
      setResetModalOpen(false);
      await fetchStaff();
    } catch (err) {
      setResetErrors({ api: err.response?.data?.message || err.message || 'Failed to reset password.' });
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
            {isSuperAdmin ? 'Staff Verification & Governance' : 'Clinic Staff & Team'}
          </h2>
          <p style={{ fontSize: '0.85rem', color: '#6B7280', marginTop: '0.25rem' }}>
            {isSuperAdmin
              ? 'Review pending registrations, verify veterinarians and inventory officers across organizations, and govern platform staff.'
              : 'Manage veterinarians, inventory officers, credentials, and organization-scoped permissions.'}
          </p>
        </div>

        {!isSuperAdmin && (
          <button
            onClick={() => {
              const pass = generateRandomPassword();
              setAddForm({
                firstName: '',
                lastName: '',
                email: '',
                phoneNumber: '',
                role: 'Veterinarian',
                password: pass,
                confirmPassword: pass
              });
              setAddErrors({});
              setAddModalOpen(true);
            }}
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
        )}
      </div>

      {/* Quick Navigation Tabs */}
      <div style={{ display: 'flex', gap: '0.5rem', borderBottom: '1px solid #E5E7EB', marginBottom: '1.25rem' }}>
        {isSuperAdmin ? (
          <>
            <button
              type="button"
              onClick={() => handleTabChange('PENDING')}
              style={{
                padding: '0.6rem 1.1rem', fontSize: '0.875rem', fontWeight: '600',
                border: 'none', borderBottom: activeTab === 'PENDING' ? '2px solid #D97706' : '2px solid transparent',
                background: 'transparent', color: activeTab === 'PENDING' ? '#D97706' : '#6B7280', cursor: 'pointer',
                display: 'inline-flex', alignItems: 'center', gap: '0.35rem'
              }}
            >
              <AlertCircle size={15} /> Pending Verification
            </button>
            <button
              type="button"
              onClick={() => handleTabChange('ALL')}
              style={{
                padding: '0.6rem 1.1rem', fontSize: '0.875rem', fontWeight: '600',
                border: 'none', borderBottom: activeTab === 'ALL' ? '2px solid var(--petcare-black, #111111)' : '2px solid transparent',
                background: 'transparent', color: activeTab === 'ALL' ? '#111827' : '#6B7280', cursor: 'pointer'
              }}
            >
              All Staff
            </button>
            <button
              type="button"
              onClick={() => handleTabChange('VET')}
              style={{
                padding: '0.6rem 1.1rem', fontSize: '0.875rem', fontWeight: '600',
                border: 'none', borderBottom: activeTab === 'VET' ? '2px solid var(--petcare-black, #111111)' : '2px solid transparent',
                background: 'transparent', color: activeTab === 'VET' ? '#111827' : '#6B7280', cursor: 'pointer',
                display: 'inline-flex', alignItems: 'center', gap: '0.35rem'
              }}
            >
              <Stethoscope size={15} /> Veterinarians
            </button>
            <button
              type="button"
              onClick={() => handleTabChange('INVENTORY')}
              style={{
                padding: '0.6rem 1.1rem', fontSize: '0.875rem', fontWeight: '600',
                border: 'none', borderBottom: activeTab === 'INVENTORY' ? '2px solid var(--petcare-black, #111111)' : '2px solid transparent',
                background: 'transparent', color: activeTab === 'INVENTORY' ? '#111827' : '#6B7280', cursor: 'pointer',
                display: 'inline-flex', alignItems: 'center', gap: '0.35rem'
              }}
            >
              <Package size={15} /> Inventory Officers
            </button>
            <button
              type="button"
              onClick={() => handleTabChange('DISABLED')}
              style={{
                padding: '0.6rem 1.1rem', fontSize: '0.875rem', fontWeight: '600',
                border: 'none', borderBottom: activeTab === 'DISABLED' ? '2px solid #DC2626' : '2px solid transparent',
                background: 'transparent', color: activeTab === 'DISABLED' ? '#DC2626' : '#6B7280', cursor: 'pointer'
              }}
            >
              Disabled Staff
            </button>
          </>
        ) : (
          <>
            <button
              type="button"
              onClick={() => handleTabChange('ALL')}
              style={{
                padding: '0.6rem 1.1rem', fontSize: '0.875rem', fontWeight: '600',
                border: 'none', borderBottom: activeTab === 'ALL' ? '2px solid var(--petcare-black, #111111)' : '2px solid transparent',
                background: 'transparent', color: activeTab === 'ALL' ? '#111827' : '#6B7280', cursor: 'pointer'
              }}
            >
              All Staff
            </button>
            <button
              type="button"
              onClick={() => handleTabChange('VET')}
              style={{
                padding: '0.6rem 1.1rem', fontSize: '0.875rem', fontWeight: '600',
                border: 'none', borderBottom: activeTab === 'VET' ? '2px solid var(--petcare-black, #111111)' : '2px solid transparent',
                background: 'transparent', color: activeTab === 'VET' ? '#111827' : '#6B7280', cursor: 'pointer',
                display: 'inline-flex', alignItems: 'center', gap: '0.35rem'
              }}
            >
              <Stethoscope size={15} /> Veterinarians
            </button>
            <button
              type="button"
              onClick={() => handleTabChange('INVENTORY')}
              style={{
                padding: '0.6rem 1.1rem', fontSize: '0.875rem', fontWeight: '600',
                border: 'none', borderBottom: activeTab === 'INVENTORY' ? '2px solid var(--petcare-black, #111111)' : '2px solid transparent',
                background: 'transparent', color: activeTab === 'INVENTORY' ? '#111827' : '#6B7280', cursor: 'pointer',
                display: 'inline-flex', alignItems: 'center', gap: '0.35rem'
              }}
            >
              <Package size={15} /> Inventory Officers
            </button>
            <button
              type="button"
              onClick={() => handleTabChange('PENDING')}
              style={{
                padding: '0.6rem 1.1rem', fontSize: '0.875rem', fontWeight: '600',
                border: 'none', borderBottom: activeTab === 'PENDING' ? '2px solid #D97706' : '2px solid transparent',
                background: 'transparent', color: activeTab === 'PENDING' ? '#D97706' : '#6B7280', cursor: 'pointer',
                display: 'inline-flex', alignItems: 'center', gap: '0.35rem'
              }}
            >
              <AlertCircle size={15} /> Pending Verification
            </button>
            <button
              type="button"
              onClick={() => handleTabChange('DISABLED')}
              style={{
                padding: '0.6rem 1.1rem', fontSize: '0.875rem', fontWeight: '600',
                border: 'none', borderBottom: activeTab === 'DISABLED' ? '2px solid #DC2626' : '2px solid transparent',
                background: 'transparent', color: activeTab === 'DISABLED' ? '#DC2626' : '#6B7280', cursor: 'pointer'
              }}
            >
              Disabled Staff
            </button>
          </>
        )}
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

      {/* Search & Secondary Filters */}
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
          onChange={(e) => { setSelectedRole(e.target.value); setActiveTab('ALL'); }}
          style={{ padding: '0.5rem 0.75rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB', backgroundColor: '#FFFFFF' }}
        >
          <option value="">All Roles</option>
          <option value="Veterinarian">Veterinarian</option>
          <option value="InventoryOfficer">Inventory Officer</option>
          <option value="ClinicManager">Clinic Manager</option>
        </select>

        <select
          value={selectedStatus}
          onChange={(e) => { setSelectedStatus(e.target.value); setActiveTab(e.target.value === 'Pending' ? 'PENDING' : 'ALL'); }}
          style={{ padding: '0.5rem 0.75rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB', backgroundColor: '#FFFFFF' }}
        >
          <option value="">All Statuses</option>
          <option value="Pending">Pending Verification</option>
          <option value="Active">Active</option>
          <option value="Disabled">Disabled</option>
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
          <h4 style={{ fontSize: '1rem', fontWeight: '600', color: '#374151' }}>
            {activeTab === 'PENDING' ? 'No pending staff accounts' : 'No staff members found'}
          </h4>
          <p style={{ fontSize: '0.875rem', color: '#6B7280', marginTop: '0.25rem' }}>
            {searchQuery
              ? `No staff match "${searchQuery}".`
              : activeTab === 'PENDING'
              ? 'There are currently no staff accounts awaiting verification.'
              : 'Get started by creating Veterinarian or Inventory Officer accounts.'}
          </p>
        </div>
      ) : (
        <div style={{ backgroundColor: '#FFFFFF', borderRadius: '0.75rem', border: '1px solid #E5E7EB', overflow: 'hidden' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
            <thead style={{ backgroundColor: '#F9FAFB', borderBottom: '1px solid #E5E7EB', color: '#4B5563', fontSize: '0.75rem', textTransform: 'uppercase' }}>
              <tr>
                <th style={{ padding: '0.75rem 1rem' }}>Staff Member</th>
                {isSuperAdmin && <th style={{ padding: '0.75rem 1rem' }}>Organization</th>}
                <th style={{ padding: '0.75rem 1rem' }}>Role</th>
                <th style={{ padding: '0.75rem 1rem' }}>Contact</th>
                <th style={{ padding: '0.75rem 1rem' }}>Status & Security</th>
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
                          backgroundColor: member.role === 'Veterinarian' ? '#EFF6FF' : '#F5F3FF',
                          color: member.role === 'Veterinarian' ? '#1D4ED8' : '#6D28D9',
                          display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: '700', fontSize: '0.85rem'
                        }}>
                          {initials}
                        </div>
                        <div>
                          <div style={{ fontWeight: '600', color: '#111827' }}>{member.fullName}</div>
                          <div style={{ fontSize: '0.75rem', color: '#6B7280' }}>ID: {member.id.substring(0, 8)}…</div>
                        </div>
                      </div>
                    </td>
                    {isSuperAdmin && (
                      <td style={{ padding: '0.85rem 1rem' }}>
                        <span style={{ fontWeight: '600', color: '#374151' }}>
                          {member.organizationName || 'Global Platform'}
                        </span>
                      </td>
                    )}
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
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.3rem', alignItems: 'flex-start' }}>
                        {member.status === 'Active' && (
                          <span style={{ backgroundColor: '#D1FAE5', color: '#065F46', padding: '0.15rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: '600' }}>
                            ● Active
                          </span>
                        )}
                        {member.status === 'Pending' && (
                          <span style={{ backgroundColor: '#FEF3C7', color: '#92400E', padding: '0.15rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: '600' }}>
                            ⏳ Pending
                          </span>
                        )}
                        {member.status === 'Disabled' && (
                          <span style={{ backgroundColor: '#FEE2E2', color: '#991B1B', padding: '0.15rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: '600' }}>
                            ○ Disabled
                          </span>
                        )}
                        {member.mustChangePassword && (
                          <span style={{
                            backgroundColor: '#FEF3C7',
                            color: '#92400E',
                            padding: '0.1rem 0.4rem',
                            borderRadius: '4px',
                            fontSize: '0.7rem',
                            fontWeight: '600',
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '0.25rem'
                          }}>
                            <Key size={11} /> Password Change Pending
                          </span>
                        )}
                      </div>
                    </td>
                    <td style={{ padding: '0.85rem 1rem', color: '#6B7280', fontSize: '0.8rem' }}>
                      {new Date(member.createdAt).toLocaleDateString()}
                    </td>
                    <td style={{ padding: '0.85rem 1rem', textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem' }}>
                        {/* Verify & Activate Button (if Pending) */}
                        {member.status === 'Pending' && (
                          <button
                            onClick={() => handleVerifyStaff(member)}
                            disabled={actionLoading}
                            title="Verify and Activate Staff Account"
                            style={{
                              padding: '0.35rem 0.65rem',
                              fontSize: '0.75rem',
                              fontWeight: '600',
                              borderRadius: '0.375rem',
                              border: '1px solid #059669',
                              backgroundColor: '#10B981',
                              color: '#FFFFFF',
                              cursor: 'pointer',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '0.3rem',
                              boxShadow: '0 1px 2px rgba(0,0,0,0.1)'
                            }}
                          >
                            <CheckCircle size={13} />
                            Verify Account
                          </button>
                        )}

                        {/* View Button */}
                        <button
                          onClick={() => { setSelectedStaff(member); setViewModalOpen(true); }}
                          title="View Staff Details"
                          style={{
                            padding: '0.35rem 0.5rem',
                            backgroundColor: '#F3F4F6',
                            border: '1px solid #D1D5DB',
                            borderRadius: '0.375rem',
                            color: '#374151',
                            cursor: 'pointer'
                          }}
                        >
                          <Eye size={14} />
                        </button>

                        {/* Edit Button (allowed for non-manager staff) */}
                        {member.role !== 'ClinicManager' && (
                          <button
                            onClick={() => openEditModal(member)}
                            title="Edit Staff Information"
                            style={{
                              padding: '0.35rem 0.5rem',
                              backgroundColor: '#F3F4F6',
                              border: '1px solid #D1D5DB',
                              borderRadius: '0.375rem',
                              color: '#374151',
                              cursor: 'pointer'
                            }}
                          >
                            <Edit2 size={14} />
                          </button>
                        )}

                        {/* Reset Password Button */}
                        {member.role !== 'ClinicManager' && (
                          <button
                            onClick={() => openResetModal(member)}
                            title="Reset Temporary Password"
                            style={{
                              padding: '0.35rem 0.5rem',
                              backgroundColor: '#EFF6FF',
                              border: '1px solid #BFDBFE',
                              borderRadius: '0.375rem',
                              color: '#1D4ED8',
                              cursor: 'pointer'
                            }}
                          >
                            <Key size={14} />
                          </button>
                        )}

                        {/* Disable/Reactivate Button */}
                        {member.role !== 'ClinicManager' && member.status !== 'Pending' && (
                          <button
                            onClick={() => handleToggleStatus(member)}
                            disabled={actionLoading}
                            title={member.status === 'Active' ? 'Disable Account' : 'Reactivate Account'}
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
                              gap: '0.25rem'
                            }}
                          >
                            <Power size={13} />
                            {member.status === 'Active' ? 'Disable' : 'Reactivate'}
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

      {/* ── MODAL 1: ADD STAFF ── */}
      {addModalOpen && (
        <div style={{
          position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#FFFFFF', borderRadius: '0.75rem', maxWidth: '520px', width: '100%',
            padding: '1.75rem', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.15)', maxHeight: '90vh', overflowY: 'auto'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
              <h3 style={{ fontSize: '1.25rem', fontWeight: '700', color: '#111827', margin: 0 }}>
                Add Organization Staff Member
              </h3>
              <button
                onClick={() => setAddModalOpen(false)}
                style={{ background: 'none', border: 'none', color: '#9CA3AF', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>
            <p style={{ fontSize: '0.85rem', color: '#6B7280', marginBottom: '1.25rem' }}>
              Create an account for a Veterinarian or Inventory Officer in your clinic. The staff member will be required to change their temporary password upon their first login.
            </p>

            {addErrors.api && (
              <div className="alert alert-error" style={{ marginBottom: '1rem', fontSize: '0.85rem' }}>
                <AlertCircle size={16} />
                <span>{addErrors.api}</span>
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
                    placeholder="Emma"
                    value={addForm.firstName}
                    onChange={(e) => setAddForm({ ...addForm, firstName: e.target.value })}
                    style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                  />
                  {addErrors.firstName && <span style={{ color: '#EF4444', fontSize: '0.75rem' }}>{addErrors.firstName}</span>}
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                    Last Name *
                  </label>
                  <input
                    type="text"
                    required
                    placeholder="Reed"
                    value={addForm.lastName}
                    onChange={(e) => setAddForm({ ...addForm, lastName: e.target.value })}
                    style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                  />
                  {addErrors.lastName && <span style={{ color: '#EF4444', fontSize: '0.75rem' }}>{addErrors.lastName}</span>}
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
                  Staff will have access scoped strictly to your veterinary organization.
                </span>
              </div>

              <div style={{ marginBottom: '0.75rem' }}>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                  Login Email *
                </label>
                <input
                  type="email"
                  required
                  placeholder="emma@clinic.com"
                  value={addForm.email}
                  onChange={(e) => setAddForm({ ...addForm, email: e.target.value })}
                  style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                />
                {addErrors.email && <span style={{ color: '#EF4444', fontSize: '0.75rem' }}>{addErrors.email}</span>}
              </div>

              <div style={{ marginBottom: '0.75rem' }}>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                  Phone Number (Optional)
                </label>
                <input
                  type="tel"
                  placeholder="+1 555-0199"
                  value={addForm.phoneNumber}
                  onChange={(e) => setAddForm({ ...addForm, phoneNumber: e.target.value })}
                  style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                />
              </div>

              {/* Temporary Password & Generator */}
              <div style={{
                backgroundColor: '#F9FAFB',
                padding: '0.85rem',
                borderRadius: '0.5rem',
                border: '1px solid #E5E7EB',
                marginBottom: '1.25rem'
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
                  <label style={{ fontSize: '0.8rem', fontWeight: '600', color: '#374151' }}>
                    Temporary Password *
                  </label>
                  <button
                    type="button"
                    onClick={() => {
                      const newPass = generateRandomPassword();
                      setAddForm({ ...addForm, password: newPass, confirmPassword: newPass });
                    }}
                    style={{
                      background: 'none', border: 'none', color: '#2563EB', fontSize: '0.75rem',
                      fontWeight: '600', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '0.25rem'
                    }}
                  >
                    <Sparkles size={12} /> Generate Password
                  </button>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                  <div>
                    <input
                      type="text"
                      required
                      placeholder="Temporary password"
                      value={addForm.password}
                      onChange={(e) => setAddForm({ ...addForm, password: e.target.value })}
                      style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB', backgroundColor: '#FFFFFF' }}
                    />
                    {addErrors.password && <span style={{ color: '#EF4444', fontSize: '0.75rem' }}>{addErrors.password}</span>}
                  </div>
                  <div>
                    <input
                      type="text"
                      required
                      placeholder="Confirm temporary password"
                      value={addForm.confirmPassword}
                      onChange={(e) => setAddForm({ ...addForm, confirmPassword: e.target.value })}
                      style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB', backgroundColor: '#FFFFFF' }}
                    />
                    {addErrors.confirmPassword && <span style={{ color: '#EF4444', fontSize: '0.75rem' }}>{addErrors.confirmPassword}</span>}
                  </div>
                </div>
                <div style={{ fontSize: '0.75rem', color: '#6B7280', marginTop: '0.4rem' }}>
                  The staff member must change this temporary password on their first login.
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
                <button
                  type="button"
                  onClick={() => setAddModalOpen(false)}
                  style={{ padding: '0.5rem 1rem', backgroundColor: '#FFFFFF', border: '1px solid #D1D5DB', borderRadius: '0.375rem', fontSize: '0.875rem', cursor: 'pointer' }}
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

      {/* ── MODAL 2: EDIT STAFF DETAILS ── */}
      {editModalOpen && (
        <div style={{
          position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#FFFFFF', borderRadius: '0.75rem', maxWidth: '480px', width: '100%',
            padding: '1.75rem', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
              <h3 style={{ fontSize: '1.25rem', fontWeight: '700', color: '#111827', margin: 0 }}>
                Edit Staff Details
              </h3>
              <button
                onClick={() => setEditModalOpen(false)}
                style={{ background: 'none', border: 'none', color: '#9CA3AF', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>
            <p style={{ fontSize: '0.85rem', color: '#6B7280', marginBottom: '1.25rem' }}>
              Update staff name and contact number. Role and organization assignment are managed securely by policy.
            </p>

            {editErrors.api && (
              <div className="alert alert-error" style={{ marginBottom: '1rem', fontSize: '0.85rem' }}>
                <AlertCircle size={16} />
                <span>{editErrors.api}</span>
              </div>
            )}

            <form onSubmit={handleUpdateStaff}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginBottom: '0.75rem' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                    First Name
                  </label>
                  <input
                    type="text"
                    required
                    value={editForm.firstName}
                    onChange={(e) => setEditForm({ ...editForm, firstName: e.target.value })}
                    style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                    Last Name
                  </label>
                  <input
                    type="text"
                    required
                    value={editForm.lastName}
                    onChange={(e) => setEditForm({ ...editForm, lastName: e.target.value })}
                    style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                  />
                </div>
              </div>

              <div style={{ marginBottom: '0.75rem' }}>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                  Email (Read-only)
                </label>
                <input
                  type="email"
                  disabled
                  value={editForm.email}
                  style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #E5E7EB', backgroundColor: '#F3F4F6', color: '#6B7280' }}
                />
              </div>

              <div style={{ marginBottom: '1.25rem' }}>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                  Phone Number
                </label>
                <input
                  type="tel"
                  placeholder="+1 555-0100"
                  value={editForm.phoneNumber}
                  onChange={(e) => setEditForm({ ...editForm, phoneNumber: e.target.value })}
                  style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
                <button
                  type="button"
                  onClick={() => setEditModalOpen(false)}
                  style={{ padding: '0.5rem 1rem', backgroundColor: '#FFFFFF', border: '1px solid #D1D5DB', borderRadius: '0.375rem', fontSize: '0.875rem', cursor: 'pointer' }}
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
                  {actionLoading ? 'Saving…' : 'Save Changes'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── MODAL 3: RESET TEMPORARY PASSWORD ── */}
      {resetModalOpen && (
        <div style={{
          position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#FFFFFF', borderRadius: '0.75rem', maxWidth: '480px', width: '100%',
            padding: '1.75rem', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
              <h3 style={{ fontSize: '1.25rem', fontWeight: '700', color: '#111827', margin: 0 }}>
                Reset Temporary Password
              </h3>
              <button
                onClick={() => setResetModalOpen(false)}
                style={{ background: 'none', border: 'none', color: '#9CA3AF', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>
            <p style={{ fontSize: '0.85rem', color: '#6B7280', marginBottom: '1rem' }}>
              Set a temporary password for <strong>{resetStaffTarget?.fullName}</strong> ({resetStaffTarget?.email}).
            </p>

            <div style={{
              backgroundColor: '#FEF3C7', border: '1px solid #FDE68A', borderRadius: '0.5rem',
              padding: '0.75rem', marginBottom: '1.25rem', fontSize: '0.8rem', color: '#92400E'
            }}>
              <strong>Security Notice:</strong> The user will be required to change this temporary password immediately upon their next login.
            </div>

            {resetErrors.api && (
              <div className="alert alert-error" style={{ marginBottom: '1rem', fontSize: '0.85rem' }}>
                <AlertCircle size={16} />
                <span>{resetErrors.api}</span>
              </div>
            )}

            <form onSubmit={handleResetPassword}>
              <div style={{ marginBottom: '1.25rem' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.25rem' }}>
                  <label style={{ fontSize: '0.8rem', fontWeight: '600', color: '#374151' }}>
                    New Temporary Password *
                  </label>
                  <button
                    type="button"
                    onClick={() => setResetPasswordValue(generateRandomPassword())}
                    style={{
                      background: 'none', border: 'none', color: '#2563EB', fontSize: '0.75rem',
                      fontWeight: '600', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '0.25rem'
                    }}
                  >
                    <Sparkles size={12} /> Regenerate
                  </button>
                </div>
                <input
                  type="text"
                  required
                  value={resetPasswordValue}
                  onChange={(e) => setResetPasswordValue(e.target.value)}
                  style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                />
                {resetErrors.password && <span style={{ color: '#EF4444', fontSize: '0.75rem' }}>{resetErrors.password}</span>}
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
                <button
                  type="button"
                  onClick={() => setResetModalOpen(false)}
                  style={{ padding: '0.5rem 1rem', backgroundColor: '#FFFFFF', border: '1px solid #D1D5DB', borderRadius: '0.375rem', fontSize: '0.875rem', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={actionLoading}
                  style={{
                    padding: '0.5rem 1.25rem',
                    backgroundColor: '#1D4ED8',
                    color: '#FFFFFF',
                    border: 'none',
                    borderRadius: '0.375rem',
                    fontWeight: '600',
                    fontSize: '0.875rem',
                    cursor: 'pointer'
                  }}
                >
                  {actionLoading ? 'Updating…' : 'Set Temporary Password'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── MODAL 4: VIEW STAFF DETAILS ── */}
      {viewModalOpen && selectedStaff && (
        <div style={{
          position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#FFFFFF', borderRadius: '0.75rem', maxWidth: '480px', width: '100%',
            padding: '1.75rem', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
              <h3 style={{ fontSize: '1.25rem', fontWeight: '700', color: '#111827', margin: 0 }}>
                Staff Member Details
              </h3>
              <button
                onClick={() => setViewModalOpen(false)}
                style={{ background: 'none', border: 'none', color: '#9CA3AF', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem', fontSize: '0.875rem' }}>
              {selectedStaff.organizationName && (
                <div style={{ padding: '0.75rem', backgroundColor: '#EFF6FF', borderRadius: '0.5rem', border: '1px solid #DBEAFE' }}>
                  <div style={{ fontSize: '0.75rem', color: '#1D4ED8', textTransform: 'uppercase', fontWeight: '600' }}>Affiliated Clinic / Organization</div>
                  <div style={{ fontWeight: '700', color: '#1E3A8A', fontSize: '0.95rem', marginTop: '0.15rem' }}>{selectedStaff.organizationName}</div>
                </div>
              )}

              <div style={{ padding: '0.75rem', backgroundColor: '#F9FAFB', borderRadius: '0.5rem', border: '1px solid #F3F4F6' }}>
                <div style={{ fontSize: '0.75rem', color: '#6B7280', textTransform: 'uppercase' }}>Full Name</div>
                <div style={{ fontWeight: '600', color: '#111827', fontSize: '1rem', marginTop: '0.15rem' }}>{selectedStaff.fullName}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div style={{ padding: '0.75rem', backgroundColor: '#F9FAFB', borderRadius: '0.5rem', border: '1px solid #F3F4F6' }}>
                  <div style={{ fontSize: '0.75rem', color: '#6B7280', textTransform: 'uppercase' }}>Role</div>
                  <div style={{ fontWeight: '600', color: '#111827', marginTop: '0.15rem' }}>{selectedStaff.role}</div>
                </div>
                <div style={{ padding: '0.75rem', backgroundColor: '#F9FAFB', borderRadius: '0.5rem', border: '1px solid #F3F4F6' }}>
                  <div style={{ fontSize: '0.75rem', color: '#6B7280', textTransform: 'uppercase' }}>Account Status</div>
                  <div style={{ fontWeight: '600', color: selectedStaff.status === 'Active' ? '#059669' : '#DC2626', marginTop: '0.15rem' }}>
                    {selectedStaff.status}
                  </div>
                </div>
              </div>

              <div style={{ padding: '0.75rem', backgroundColor: '#F9FAFB', borderRadius: '0.5rem', border: '1px solid #F3F4F6' }}>
                <div style={{ fontSize: '0.75rem', color: '#6B7280', textTransform: 'uppercase' }}>Login Email</div>
                <div style={{ fontWeight: '500', color: '#111827', marginTop: '0.15rem' }}>{selectedStaff.email}</div>
              </div>

              <div style={{ padding: '0.75rem', backgroundColor: '#F9FAFB', borderRadius: '0.5rem', border: '1px solid #F3F4F6' }}>
                <div style={{ fontSize: '0.75rem', color: '#6B7280', textTransform: 'uppercase' }}>Phone Number</div>
                <div style={{ fontWeight: '500', color: '#111827', marginTop: '0.15rem' }}>{selectedStaff.phoneNumber || 'None provided'}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div style={{ padding: '0.75rem', backgroundColor: '#F9FAFB', borderRadius: '0.5rem', border: '1px solid #F3F4F6' }}>
                  <div style={{ fontSize: '0.75rem', color: '#6B7280', textTransform: 'uppercase' }}>Created On</div>
                  <div style={{ fontWeight: '500', color: '#111827', marginTop: '0.15rem' }}>
                    {new Date(selectedStaff.createdAt).toLocaleDateString()}
                  </div>
                </div>
                <div style={{ padding: '0.75rem', backgroundColor: '#F9FAFB', borderRadius: '0.5rem', border: '1px solid #F3F4F6' }}>
                  <div style={{ fontSize: '0.75rem', color: '#6B7280', textTransform: 'uppercase' }}>Password Change Pending</div>
                  <div style={{ fontWeight: '600', color: selectedStaff.mustChangePassword ? '#B45309' : '#059669', marginTop: '0.15rem' }}>
                    {selectedStaff.mustChangePassword ? 'Yes (Temporary)' : 'No (Permanent)'}
                  </div>
                </div>
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
              <button
                type="button"
                onClick={() => setViewModalOpen(false)}
                style={{ padding: '0.5rem 1.25rem', backgroundColor: '#111827', color: '#FFFFFF', border: 'none', borderRadius: '0.375rem', fontSize: '0.875rem', fontWeight: '600', cursor: 'pointer' }}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
