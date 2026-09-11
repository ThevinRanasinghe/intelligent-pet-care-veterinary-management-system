import { useState, useEffect, useCallback } from 'react';
import {
  Building2, Search, CheckCircle, XCircle, AlertTriangle, Clock,
  RefreshCw, Shield, MapPin, Mail, Phone, User, Calendar
} from 'lucide-react';
import {
  getOrganizations, approveOrganization, rejectOrganization,
  suspendOrganization, activateOrganization
} from '../services/adminApi';

export default function OrganizationManagementView() {
  const [organizations, setOrganizations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const [selectedTab, setSelectedTab] = useState('All'); // 'All', 'Pending', 'Active', 'Suspended', 'Rejected'
  const [searchQuery, setSearchQuery] = useState('');

  // Reject Modal state
  const [rejectModalOpen, setRejectModalOpen] = useState(false);
  const [selectedOrgForReject, setSelectedOrgForReject] = useState(null);
  const [rejectionReason, setRejectionReason] = useState('');
  const [actionLoading, setActionLoading] = useState(false);

  const fetchOrgs = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getOrganizations({
        search: searchQuery,
        status: selectedTab === 'All' ? '' : selectedTab,
      });
      setOrganizations(data);
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to load organizations.');
    } finally {
      setLoading(false);
    }
  }, [selectedTab, searchQuery]);

  useEffect(() => {
    fetchOrgs();
  }, [fetchOrgs]);

  const handleApprove = async (org) => {
    if (!window.confirm(`Are you sure you want to approve "${org.name}"? This will activate the clinic and its primary manager account.`)) {
      return;
    }
    setActionLoading(true);
    setError('');
    setSuccessMsg('');
    try {
      await approveOrganization(org.id);
      setSuccessMsg(`"${org.name}" has been approved and activated!`);
      await fetchOrgs();
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to approve organization.');
    } finally {
      setActionLoading(false);
    }
  };

  const openRejectModal = (org) => {
    setSelectedOrgForReject(org);
    setRejectionReason('');
    setRejectModalOpen(true);
  };

  const handleConfirmReject = async (e) => {
    e.preventDefault();
    if (!rejectionReason.trim()) {
      setError('Please provide a reason for the rejection.');
      return;
    }
    setActionLoading(true);
    setError('');
    setSuccessMsg('');
    try {
      await rejectOrganization(selectedOrgForReject.id, rejectionReason.trim());
      setSuccessMsg(`Registration for "${selectedOrgForReject.name}" was rejected.`);
      setRejectModalOpen(false);
      setSelectedOrgForReject(null);
      await fetchOrgs();
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to reject organization.');
    } finally {
      setActionLoading(false);
    }
  };

  const handleSuspend = async (org) => {
    if (!window.confirm(`Suspend "${org.name}"? Clinic staff will lose access until reactivated.`)) {
      return;
    }
    setActionLoading(true);
    try {
      await suspendOrganization(org.id);
      setSuccessMsg(`"${org.name}" has been suspended.`);
      await fetchOrgs();
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to suspend organization.');
    } finally {
      setActionLoading(false);
    }
  };

  const handleActivate = async (org) => {
    setActionLoading(true);
    try {
      await activateOrganization(org.id);
      setSuccessMsg(`"${org.name}" has been reactivated.`);
      await fetchOrgs();
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to reactivate organization.');
    } finally {
      setActionLoading(false);
    }
  };

  const getStatusBadge = (status) => {
    switch (status) {
      case 'Pending':
        return <span style={{ backgroundColor: '#FEF3C7', color: '#92400E', padding: '0.25rem 0.65rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: '600' }}>⏳ Pending Review</span>;
      case 'Active':
        return <span style={{ backgroundColor: '#D1FAE5', color: '#065F46', padding: '0.25rem 0.65rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: '600' }}>✓ Active</span>;
      case 'Suspended':
        return <span style={{ backgroundColor: '#FEE2E2', color: '#991B1B', padding: '0.25rem 0.65rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: '600' }}>⛔ Suspended</span>;
      case 'Rejected':
        return <span style={{ backgroundColor: '#F3F4F6', color: '#4B5563', padding: '0.25rem 0.65rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: '600' }}>✕ Rejected</span>;
      default:
        return <span style={{ backgroundColor: '#F3F4F6', color: '#4B5563', padding: '0.25rem 0.65rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: '600' }}>{status}</span>;
    }
  };

  const tabs = ['All', 'Pending', 'Active', 'Suspended', 'Rejected'];

  return (
    <div>
      {/* Top action/filter bar */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem', marginBottom: '1.5rem' }}>
        {/* Tab pills */}
        <div style={{ display: 'flex', gap: '0.5rem', backgroundColor: '#F3F4F6', padding: '0.35rem', borderRadius: '0.5rem' }}>
          {tabs.map((tab) => (
            <button
              key={tab}
              onClick={() => setSelectedTab(tab)}
              style={{
                padding: '0.5rem 1rem',
                fontSize: '0.875rem',
                fontWeight: '600',
                border: 'none',
                borderRadius: '0.375rem',
                cursor: 'pointer',
                backgroundColor: selectedTab === tab ? '#FFFFFF' : 'transparent',
                color: selectedTab === tab ? '#111827' : '#6B7280',
                boxShadow: selectedTab === tab ? '0 1px 3px rgba(0,0,0,0.1)' : 'none',
                transition: 'all 0.15s ease'
              }}
            >
              {tab}
            </button>
          ))}
        </div>

        {/* Search & Refresh */}
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
          <div style={{ position: 'relative', minWidth: '240px' }}>
            <Search size={16} style={{ position: 'absolute', left: '0.75rem', top: '50%', transform: 'translateY(-50%)', color: '#9CA3AF' }} />
            <input
              type="text"
              placeholder="Search clinics..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              style={{
                width: '100%',
                padding: '0.5rem 0.75rem 0.5rem 2.25rem',
                fontSize: '0.875rem',
                borderRadius: '0.375rem',
                border: '1px solid #D1D5DB'
              }}
            />
          </div>
          <button
            onClick={fetchOrgs}
            title="Refresh organizations"
            disabled={loading}
            style={{
              padding: '0.5rem 0.75rem',
              backgroundColor: '#FFFFFF',
              border: '1px solid #D1D5DB',
              borderRadius: '0.375rem',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '0.25rem',
              fontSize: '0.875rem'
            }}
          >
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} />
            Refresh
          </button>
        </div>
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
          <AlertTriangle size={18} />
          <span>{error}</span>
        </div>
      )}

      {/* Organizations List / Cards */}
      {loading ? (
        <div style={{ padding: '3rem', textAlign: 'center', color: '#6B7280' }}>
          <RefreshCw size={28} className="animate-spin" style={{ margin: '0 auto 0.75rem auto' }} />
          <p>Loading organizations...</p>
        </div>
      ) : organizations.length === 0 ? (
        <div style={{ backgroundColor: '#FFFFFF', border: '1px dashed #D1D5DB', borderRadius: '0.75rem', padding: '3rem', textAlign: 'center' }}>
          <Building2 size={36} style={{ color: '#9CA3AF', margin: '0 auto 0.5rem auto' }} />
          <h4 style={{ fontSize: '1rem', fontWeight: '600', color: '#374151' }}>No organizations found</h4>
          <p style={{ fontSize: '0.875rem', color: '#6B7280', marginTop: '0.25rem' }}>
            {searchQuery ? `No clinics match "${searchQuery}" in ${selectedTab}.` : `There are currently no ${selectedTab.toLowerCase()} organizations.`}
          </p>
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {organizations.map((org) => (
            <div
              key={org.id}
              style={{
                backgroundColor: '#FFFFFF',
                borderRadius: '0.75rem',
                border: '1px solid #E5E7EB',
                padding: '1.25rem',
                boxShadow: '0 1px 2px rgba(0,0,0,0.03)',
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'flex-start',
                flexWrap: 'wrap',
                gap: '1rem'
              }}
            >
              {/* Left Column: Clinic Details */}
              <div style={{ flex: '1 1 400px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '0.5rem' }}>
                  <h3 style={{ fontSize: '1.125rem', fontWeight: '700', color: '#111827', margin: 0 }}>{org.name}</h3>
                  {getStatusBadge(org.status)}
                  {org.registrationNumber && (
                    <span style={{ fontSize: '0.75rem', color: '#6B7280', backgroundColor: '#F3F4F6', padding: '0.15rem 0.45rem', borderRadius: '4px' }}>
                      {org.registrationNumber}
                    </span>
                  )}
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '0.5rem', fontSize: '0.85rem', color: '#4B5563', marginTop: '0.75rem' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
                    <MapPin size={14} style={{ color: '#9CA3AF' }} />
                    <span>{org.address}, {org.city}, {org.country}</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
                    <Mail size={14} style={{ color: '#9CA3AF' }} />
                    <span>{org.email}</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
                    <Phone size={14} style={{ color: '#9CA3AF' }} />
                    <span>{org.phone}</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
                    <Calendar size={14} style={{ color: '#9CA3AF' }} />
                    <span>Registered: {new Date(org.createdAt).toLocaleDateString()}</span>
                  </div>
                </div>

                {/* Initial Clinic Manager Callout */}
                <div style={{ marginTop: '0.75rem', padding: '0.5rem 0.75rem', backgroundColor: '#F9FAFB', borderRadius: '0.5rem', border: '1px solid #F3F4F6', display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.85rem' }}>
                  <User size={15} style={{ color: '#6366F1' }} />
                  <span style={{ color: '#4B5563' }}>
                    <strong>Clinic Manager:</strong> {org.initialManagerName || 'Pending'} ({org.initialManagerEmail || '—'})
                  </span>
                  <span style={{ marginLeft: 'auto', color: '#6B7280', fontSize: '0.8rem' }}>
                    Staff: {org.staffCount}
                  </span>
                </div>

                {/* If Rejected: Display reason */}
                {org.status === 'Rejected' && org.rejectionReason && (
                  <div style={{ marginTop: '0.5rem', padding: '0.5rem 0.75rem', backgroundColor: '#FEF2F2', borderRadius: '0.5rem', border: '1px solid #FCA5A5', fontSize: '0.825rem', color: '#991B1B' }}>
                    <strong>Rejection Reason:</strong> {org.rejectionReason}
                  </div>
                )}
              </div>

              {/* Right Column: SuperAdmin Actions */}
              <div style={{ display: 'flex', gap: '0.5rem', alignSelf: 'center', flexWrap: 'wrap' }}>
                {org.status === 'Pending' && (
                  <>
                    <button
                      onClick={() => handleApprove(org)}
                      disabled={actionLoading}
                      style={{
                        padding: '0.5rem 1rem',
                        backgroundColor: '#10B981',
                        color: '#FFFFFF',
                        border: 'none',
                        borderRadius: '0.375rem',
                        fontWeight: '600',
                        fontSize: '0.875rem',
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.35rem'
                      }}
                    >
                      <CheckCircle size={16} />
                      Approve
                    </button>
                    <button
                      onClick={() => openRejectModal(org)}
                      disabled={actionLoading}
                      style={{
                        padding: '0.5rem 1rem',
                        backgroundColor: '#EF4444',
                        color: '#FFFFFF',
                        border: 'none',
                        borderRadius: '0.375rem',
                        fontWeight: '600',
                        fontSize: '0.875rem',
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.35rem'
                      }}
                    >
                      <XCircle size={16} />
                      Reject
                    </button>
                  </>
                )}

                {org.status === 'Active' && (
                  <button
                    onClick={() => handleSuspend(org)}
                    disabled={actionLoading}
                    style={{
                      padding: '0.5rem 1rem',
                      backgroundColor: '#FFFFFF',
                      color: '#DC2626',
                      border: '1px solid #DC2626',
                      borderRadius: '0.375rem',
                      fontWeight: '600',
                      fontSize: '0.875rem',
                      cursor: 'pointer'
                    }}
                  >
                    Suspend Organization
                  </button>
                )}

                {org.status === 'Suspended' && (
                  <button
                    onClick={() => handleActivate(org)}
                    disabled={actionLoading}
                    style={{
                      padding: '0.5rem 1rem',
                      backgroundColor: '#10B981',
                      color: '#FFFFFF',
                      border: 'none',
                      borderRadius: '0.375rem',
                      fontWeight: '600',
                      fontSize: '0.875rem',
                      cursor: 'pointer'
                    }}
                  >
                    Activate Organization
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Reject Modal */}
      {rejectModalOpen && selectedOrgForReject && (
        <div style={{
          position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#FFFFFF', borderRadius: '0.75rem', maxWidth: '480px', width: '100%',
            padding: '1.5rem', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <h3 style={{ fontSize: '1.25rem', fontWeight: '700', color: '#111827', marginBottom: '0.5rem' }}>
              Reject Organization Registration
            </h3>
            <p style={{ fontSize: '0.875rem', color: '#6B7280', marginBottom: '1rem' }}>
              You are rejecting the registration application for <strong>{selectedOrgForReject.name}</strong>. Please provide an explicit reason.
            </p>

            <form onSubmit={handleConfirmReject}>
              <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: '600', color: '#374151', marginBottom: '0.35rem' }}>
                Rejection Reason *
              </label>
              <textarea
                rows={4}
                required
                value={rejectionReason}
                onChange={(e) => setRejectionReason(e.target.value)}
                placeholder="e.g. Could not verify state veterinary license number. Contact details could not be reached."
                style={{
                  width: '100%',
                  padding: '0.65rem',
                  fontSize: '0.875rem',
                  borderRadius: '0.375rem',
                  border: '1px solid #D1D5DB',
                  marginBottom: '1rem'
                }}
              />

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
                <button
                  type="button"
                  onClick={() => setRejectModalOpen(false)}
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
                    backgroundColor: '#EF4444',
                    color: '#FFFFFF',
                    border: 'none',
                    borderRadius: '0.375rem',
                    fontWeight: '600',
                    fontSize: '0.875rem',
                    cursor: 'pointer'
                  }}
                >
                  {actionLoading ? 'Rejecting…' : 'Confirm Rejection'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
