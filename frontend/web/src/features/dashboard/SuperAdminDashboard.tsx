import { useEffect, useMemo, useState } from 'react';
import type { CSSProperties } from 'react';
import { Link } from 'react-router-dom';
import { Building2, RefreshCw, Users } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import {
  getOrganizations,
  getSystemInfo,
  type AdminOrganization,
  type AdminSystemInfo,
} from '../../services/adminService';
import { messageFrom } from '../../utils/errors';
import { formatDate } from '../../utils/format';

const statusTone: Record<string, 'success' | 'warning' | 'neutral' | 'danger' | 'info'> = {
  Pending: 'warning',
  Active: 'success',
  Rejected: 'danger',
  Suspended: 'danger',
  Inactive: 'neutral',
};

const cardStyle: CSSProperties = {
  backgroundColor: '#ffffff',
  padding: '1.5rem',
  borderRadius: '0.75rem',
  border: '1px solid #e5e7eb',
};

/**
 * Administrator dashboard — platform-level view built from real
 * user/organization data served by /api/admin endpoints.
 */
export function SuperAdminDashboard() {
  const [info, setInfo] = useState<AdminSystemInfo | null>(null);
  const [organizations, setOrganizations] = useState<AdminOrganization[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    const [infoResult, orgsResult] = await Promise.allSettled([getSystemInfo(), getOrganizations()]);
    if (infoResult.status === 'fulfilled') setInfo(infoResult.value);
    if (orgsResult.status === 'fulfilled') setOrganizations(orgsResult.value);
    const failure = [infoResult, orgsResult].find((r) => r.status === 'rejected');
    if (failure && failure.status === 'rejected') setError(messageFrom(failure.reason));
    setLoading(false);
  };

  useEffect(() => { void load(); }, []);

  const pendingOrgs = useMemo(
    () => organizations.filter((o) => o.status === 'Pending'),
    [organizations],
  );

  if (loading) {
    return <div className="empty-state"><strong>Loading platform overview…</strong></div>;
  }

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">ADMINISTRATION</span>
          <h2>Platform Overview</h2>
          <p>Accounts, organizations, and verification workload across the platform.</p>
        </div>
        <div className="heading-actions">
          <Button variant="secondary" onClick={() => void load()} icon={<RefreshCw size={14} />}>Refresh</Button>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: '16px' }}><strong>Error:</strong> {error}</div>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1.25rem', marginBottom: '1.5rem' }}>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: 500 }}>Total Users</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#111827', marginTop: '0.25rem' }}>{info?.totalUsers ?? '—'}</div>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: 500 }}>Active Users</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#10b981', marginTop: '0.25rem' }}>{info?.activeUsers ?? '—'}</div>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: 500 }}>Inactive Users</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: (info?.inactiveUsers ?? 0) > 0 ? '#b45309' : '#111827', marginTop: '0.25rem' }}>{info?.inactiveUsers ?? '—'}</div>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: 500 }}>Organizations</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#111827', marginTop: '0.25rem' }}>{info?.totalOrganizations ?? '—'}</div>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: 500 }}>Pending Approvals</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: (info?.pendingOrganizations ?? 0) > 0 ? '#b45309' : '#111827', marginTop: '0.25rem' }}>{info?.pendingOrganizations ?? '—'}</div>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.25rem' }}>
        {/* Users by role */}
        <div style={cardStyle}>
          <h3 style={{ fontSize: '1.05rem', fontWeight: 600, color: '#111827', marginTop: 0, display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Users size={16} /> Users by Role
          </h3>
          {!info || Object.keys(info.usersByRole).length === 0 ? (
            <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>No user data available.</p>
          ) : (
            <ul style={{ margin: 0, padding: 0, listStyle: 'none', fontSize: '0.9rem' }}>
              {Object.entries(info.usersByRole).sort(([a], [b]) => a.localeCompare(b)).map(([role, count]) => (
                <li key={role} style={{ display: 'flex', justifyContent: 'space-between', padding: '0.4rem 0', borderBottom: '1px solid #f3f4f6' }}>
                  <span>{role}</span>
                  <Badge tone="info">{count}</Badge>
                </li>
              ))}
            </ul>
          )}
          <div style={{ marginTop: '0.75rem' }}>
            <Link to="/admin/roles" style={{ fontSize: '0.85rem' }}>Manage roles &amp; permissions →</Link>
          </div>
        </div>

        {/* Pending organization approvals */}
        <div style={cardStyle}>
          <h3 style={{ fontSize: '1.05rem', fontWeight: 600, color: '#111827', marginTop: 0, display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Building2 size={16} /> Organizations Awaiting Verification
          </h3>
          {pendingOrgs.length === 0 ? (
            <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>No pending organization registrations.</p>
          ) : (
            <ul style={{ margin: 0, padding: 0, listStyle: 'none', fontSize: '0.9rem' }}>
              {pendingOrgs.map((org) => (
                <li key={org.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '0.5rem', padding: '0.4rem 0', borderBottom: '1px solid #f3f4f6' }}>
                  <span style={{ minWidth: 0 }}>
                    <strong>{org.name}</strong>
                    <span style={{ color: '#6b7280' }}> — {[org.city, org.country].filter(Boolean).join(', ') || org.email}</span>
                    <div style={{ color: '#6b7280', fontSize: '0.78rem' }}>Registered {org.createdAt ? formatDate(org.createdAt.slice(0, 10)) : '—'}</div>
                  </span>
                  <Badge tone={statusTone[org.status] ?? 'neutral'}>{org.status}</Badge>
                </li>
              ))}
            </ul>
          )}
          <div style={{ marginTop: '0.75rem' }}>
            <Link to="/admin/organizations" style={{ fontSize: '0.85rem' }}>Review organizations →</Link>
          </div>
        </div>
      </div>
    </div>
  );
}

export default SuperAdminDashboard;
