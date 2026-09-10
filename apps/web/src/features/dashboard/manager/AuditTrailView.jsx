import { useState, useEffect, useCallback } from 'react';
import {
  Shield, Search, RefreshCw, AlertCircle, Clock,
  Filter, User, FileText, ChevronLeft, ChevronRight
} from 'lucide-react';
import { getClinicAuditLogs } from '../services/adminApi';

export default function AuditTrailView() {
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [search, setSearch] = useState('');
  const [actionFilter, setActionFilter] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getClinicAuditLogs({
        search: search || undefined,
        action: actionFilter || undefined,
        pageNumber: page,
        pageSize
      });
      setLogs(data);
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to load clinic audit trail.');
    } finally {
      setLoading(false);
    }
  }, [search, actionFilter, page]);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  const getActionBadge = (action) => {
    if (action.includes('Approve')) return { bg: '#dcfce7', text: '#15803d' };
    if (action.includes('Reject')) return { bg: '#fee2e2', text: '#b91c1c' };
    if (action.includes('Revision')) return { bg: '#fef3c7', text: '#b45309' };
    if (action.includes('Create')) return { bg: '#e0f2fe', text: '#0369a1' };
    if (action.includes('Update')) return { bg: '#f3e8ff', text: '#7e22ce' };
    return { bg: '#f3f4f6', text: '#4b5563' };
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Search and Filters */}
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
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.875rem', flex: 1, minWidth: '260px' }}>
          <div style={{ position: 'relative', flex: 1 }}>
            <Search size={16} style={{ position: 'absolute', left: '0.75rem', top: '50%', transform: 'translateY(-50%)', color: '#9ca3af' }} />
            <input
              type="text"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              placeholder="Search by action details or user email..."
              style={{
                width: '100%',
                padding: '0.55rem 0.75rem 0.55rem 2.25rem',
                borderRadius: '0.5rem',
                border: '1px solid #d1d5db',
                fontSize: '0.875rem'
              }}
            />
          </div>

          <select
            value={actionFilter}
            onChange={(e) => {
              setActionFilter(e.target.value);
              setPage(1);
            }}
            style={{
              padding: '0.55rem 0.75rem',
              borderRadius: '0.5rem',
              border: '1px solid #d1d5db',
              fontSize: '0.875rem',
              backgroundColor: '#fff'
            }}
          >
            <option value="">All Action Types</option>
            <option value="ApproveProposal">Approve Proposal</option>
            <option value="RejectProposal">Reject Proposal</option>
            <option value="RequestRevision">Request Revision</option>
            <option value="CreateAppointment">Create Appointment</option>
            <option value="CancelAppointment">Cancel Appointment</option>
            <option value="CreateSlot">Create Schedule Slot</option>
            <option value="UpdateStaff">Update Staff</option>
          </select>
        </div>

        <button
          onClick={fetchLogs}
          title="Refresh Logs"
          style={{
            padding: '0.5rem 0.75rem',
            borderRadius: '0.5rem',
            border: '1px solid #d1d5db',
            backgroundColor: '#fff',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '0.375rem',
            fontSize: '0.85rem',
            color: '#374151'
          }}
        >
          <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
        </button>
      </div>

      {error && (
        <div style={{ padding: '0.875rem 1rem', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#991b1b', fontSize: '0.875rem' }}>
          <AlertCircle size={18} />
          <span>{error}</span>
        </div>
      )}

      {/* Table */}
      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.875rem', border: '1px solid #e5e7eb', overflow: 'hidden' }}>
        <div style={{ padding: '1.25rem', borderBottom: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Shield size={18} style={{ color: '#0d9488' }} />
            <h3 style={{ fontSize: '1rem', fontWeight: '600', color: '#111827', margin: 0 }}>
              Organization Audit Trail ({logs.length} entries)
            </h3>
          </div>
          <span style={{ fontSize: '0.8rem', color: '#6b7280' }}>
            Immutable administrative activity record
          </span>
        </div>

        {loading ? (
          <div style={{ padding: '3.5rem', textAlign: 'center', color: '#6b7280' }}>
            <RefreshCw className="animate-spin" size={24} style={{ color: '#0d9488', margin: '0 auto 0.5rem' }} />
            <p style={{ margin: 0, fontSize: '0.875rem' }}>Loading clinic audit logs...</p>
          </div>
        ) : logs.length === 0 ? (
          <div style={{ padding: '3.5rem 2rem', textAlign: 'center', color: '#6b7280' }}>
            <Clock size={36} style={{ color: '#9ca3af', margin: '0 auto 0.75rem' }} />
            <h4 style={{ fontWeight: '600', color: '#374151', margin: '0 0 0.25rem' }}>No audit logs found</h4>
            <p style={{ fontSize: '0.85rem', color: '#9ca3af', margin: 0 }}>
              Actions performed in this organization will appear here automatically.
            </p>
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
              <thead>
                <tr style={{ backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Timestamp</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>User Email</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Action</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Entity</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Details</th>
                </tr>
              </thead>
              <tbody>
                {logs.map((item) => {
                  const badge = getActionBadge(item.action);
                  return (
                    <tr key={item.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                      <td style={{ padding: '1rem 1.25rem', color: '#6b7280', fontSize: '0.8rem', whiteSpace: 'nowrap' }}>
                        {new Date(item.timestamp).toLocaleString()}
                      </td>
                      <td style={{ padding: '1rem 1.25rem', fontWeight: '500', color: '#111827' }}>
                        {item.userEmail || 'System'}
                      </td>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <span style={{
                          padding: '0.2rem 0.55rem',
                          borderRadius: '0.375rem',
                          backgroundColor: badge.bg,
                          color: badge.text,
                          fontSize: '0.75rem',
                          fontWeight: '700'
                        }}>
                          {item.action}
                        </span>
                      </td>
                      <td style={{ padding: '1rem 1.25rem', color: '#4b5563', fontSize: '0.8rem' }}>
                        {item.entityType} ({item.entityId ? item.entityId.slice(0, 8) : 'N/A'})
                      </td>
                      <td style={{ padding: '1rem 1.25rem', color: '#374151' }}>
                        {item.details}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination Bar */}
        <div style={{ padding: '0.875rem 1.25rem', borderTop: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center', backgroundColor: '#f9fafb' }}>
          <span style={{ fontSize: '0.8rem', color: '#6b7280' }}>
            Page {page} • Showing up to {pageSize} items per page
          </span>
          <div style={{ display: 'flex', gap: '0.5rem' }}>
            <button
              disabled={page <= 1}
              onClick={() => setPage(p => Math.max(1, p - 1))}
              style={{
                padding: '0.35rem 0.65rem',
                borderRadius: '0.375rem',
                border: '1px solid #d1d5db',
                backgroundColor: '#fff',
                fontSize: '0.75rem',
                cursor: page <= 1 ? 'not-allowed' : 'pointer',
                opacity: page <= 1 ? 0.5 : 1,
                display: 'flex',
                alignItems: 'center',
                gap: '0.25rem'
              }}
            >
              <ChevronLeft size={14} /> Prev
            </button>
            <button
              disabled={logs.length < pageSize}
              onClick={() => setPage(p => p + 1)}
              style={{
                padding: '0.35rem 0.65rem',
                borderRadius: '0.375rem',
                border: '1px solid #d1d5db',
                backgroundColor: '#fff',
                fontSize: '0.75rem',
                cursor: logs.length < pageSize ? 'not-allowed' : 'pointer',
                opacity: logs.length < pageSize ? 0.5 : 1,
                display: 'flex',
                alignItems: 'center',
                gap: '0.25rem'
              }}
            >
              Next <ChevronRight size={14} />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
