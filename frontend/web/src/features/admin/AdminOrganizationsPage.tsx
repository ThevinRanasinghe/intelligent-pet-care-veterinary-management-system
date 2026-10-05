import { useEffect, useMemo, useState } from 'react';
import { Building2, RefreshCw } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import {
  getOrganizations,
  setOrganizationStatus,
  type AdminOrganization,
  type OrganizationStatus,
} from '../../services/adminService';
import { messageFrom } from '../../utils/errors';
import { formatDate } from '../../utils/format';

const statusTone: Record<OrganizationStatus, 'success' | 'warning' | 'neutral' | 'danger' | 'info'> = {
  Pending: 'warning',
  Active: 'success',
  Rejected: 'danger',
  Suspended: 'danger',
  Inactive: 'neutral',
};

/** Organization Management — verify, suspend, or reactivate clinics. */
export function AdminOrganizationsPage() {
  const [organizations, setOrganizations] = useState<AdminOrganization[]>([]);
  const [statusFilter, setStatusFilter] = useState('All');
  const [loading, setLoading] = useState(true);
  const [actioningId, setActioningId] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      setOrganizations(await getOrganizations());
    } catch (err) {
      setError(messageFrom(err));
      setOrganizations([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void load(); }, []);

  useEffect(() => {
    if (!success) return;
    const timer = setTimeout(() => setSuccess(''), 4000);
    return () => clearTimeout(timer);
  }, [success]);

  const visibleOrgs = useMemo(
    () => (statusFilter === 'All' ? organizations : organizations.filter((o) => o.status === statusFilter)),
    [organizations, statusFilter],
  );

  const transition = async (org: AdminOrganization, status: Exclude<OrganizationStatus, 'Pending'>, needsReason: boolean) => {
    let reason: string | undefined;
    if (needsReason) {
      const input = window.prompt(`Reason for marking "${org.name}" as ${status}:`);
      if (input === null) return; // cancelled
      if (!input.trim()) {
        setError('A reason is required for this action.');
        return;
      }
      reason = input.trim();
    }
    setActioningId(org.id);
    setError('');
    try {
      await setOrganizationStatus(org.id, status, reason);
      setSuccess(`"${org.name}" is now ${status}.`);
      await load();
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setActioningId(null);
    }
  };

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">ADMINISTRATION</span>
          <h2>Organization Management</h2>
          <p>Verify new clinic registrations and manage organization lifecycle states.</p>
        </div>
        <div className="heading-actions" style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} aria-label="Filter by status">
            {['All', 'Pending', 'Active', 'Rejected', 'Suspended', 'Inactive'].map((s) => (
              <option key={s} value={s}>{s === 'All' ? 'All statuses' : s}</option>
            ))}
          </select>
          <Button variant="secondary" onClick={() => void load()} icon={<RefreshCw size={14} />}>Refresh</Button>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: '16px' }}><strong>Error:</strong> {error}</div>}
      {success && (
        <div className="info-strip" style={{ marginBottom: '16px', borderColor: 'var(--success)', background: 'var(--success-soft)', color: 'var(--success)' }}>
          <strong>Success:</strong> <span>{success}</span>
        </div>
      )}

      {loading ? (
        <div className="empty-state"><strong>Loading organizations…</strong></div>
      ) : visibleOrgs.length === 0 ? (
        <div className="empty-state"><Building2 size={28} /><strong>No organizations found.</strong></div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Organization</th>
                <th>Contact</th>
                <th>Location</th>
                <th>Users</th>
                <th>Status</th>
                <th>Registered</th>
                <th style={{ textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {visibleOrgs.map((org) => (
                <tr key={org.id}>
                  <td>
                    <strong>{org.name}</strong>
                    {org.registrationNumber && (
                      <div className="muted" style={{ fontSize: '0.78rem' }}>Reg. {org.registrationNumber}</div>
                    )}
                    {org.rejectionReason && (
                      <div className="muted" style={{ fontSize: '0.78rem' }}>Reason: {org.rejectionReason}</div>
                    )}
                  </td>
                  <td>
                    {org.email}
                    <div className="muted" style={{ fontSize: '0.78rem' }}>{org.phone}</div>
                  </td>
                  <td>{[org.city, org.country].filter(Boolean).join(', ') || '—'}</td>
                  <td>{org.userCount}</td>
                  <td><Badge tone={statusTone[org.status] ?? 'neutral'}>{org.status}</Badge></td>
                  <td>{org.createdAt ? formatDate(org.createdAt.slice(0, 10)) : '—'}</td>
                  <td style={{ textAlign: 'right' }}>
                    <div style={{ display: 'inline-flex', gap: '0.4rem', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                      {org.status === 'Pending' && (
                        <>
                          <Button variant="primary" disabled={actioningId === org.id}
                            onClick={() => void transition(org, 'Active', false)}>Approve</Button>
                          <Button variant="secondary" disabled={actioningId === org.id}
                            onClick={() => void transition(org, 'Rejected', true)}>Reject</Button>
                        </>
                      )}
                      {org.status === 'Active' && (
                        <Button variant="secondary" disabled={actioningId === org.id}
                          onClick={() => void transition(org, 'Suspended', true)}>Suspend</Button>
                      )}
                      {(org.status === 'Suspended' || org.status === 'Rejected' || org.status === 'Inactive') && (
                        <Button variant="secondary" disabled={actioningId === org.id}
                          onClick={() => void transition(org, 'Active', false)}>Reactivate</Button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default AdminOrganizationsPage;
