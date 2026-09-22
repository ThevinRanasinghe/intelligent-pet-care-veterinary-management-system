import { useEffect, useState } from 'react';
import { RefreshCw, ServerCog } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { getSystemInfo, type AdminSystemInfo } from '../../services/adminService';
import { messageFrom } from '../../utils/errors';
import { formatDate } from '../../utils/format';

/** System Settings — platform runtime information and aggregate statistics. */
export function AdminSystemPage() {
  const [info, setInfo] = useState<AdminSystemInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      setInfo(await getSystemInfo());
    } catch (err) {
      setError(messageFrom(err));
      setInfo(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void load(); }, []);

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">ADMINISTRATION</span>
          <h2>System Settings</h2>
          <p>Platform runtime status and account statistics.</p>
        </div>
        <div className="heading-actions">
          <Button variant="secondary" onClick={() => void load()} icon={<RefreshCw size={14} />}>Refresh</Button>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: '16px' }}><strong>Error:</strong> {error}</div>}

      {loading ? (
        <div className="empty-state"><strong>Loading system information…</strong></div>
      ) : !info ? (
        <div className="empty-state"><ServerCog size={28} /><strong>System information unavailable.</strong></div>
      ) : (
        <>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1.25rem', marginBottom: '1.5rem' }}>
            <div className="table-wrap" style={{ padding: '1.25rem' }}>
              <div className="muted" style={{ fontSize: '0.8rem' }}>Environment</div>
              <div style={{ fontSize: '1.2rem', fontWeight: 700 }}>{info.environment}</div>
            </div>
            <div className="table-wrap" style={{ padding: '1.25rem' }}>
              <div className="muted" style={{ fontSize: '0.8rem' }}>Database</div>
              <div style={{ fontSize: '1.2rem', fontWeight: 700 }}>{info.databaseProvider}</div>
            </div>
            <div className="table-wrap" style={{ padding: '1.25rem' }}>
              <div className="muted" style={{ fontSize: '0.8rem' }}>Server Time (UTC)</div>
              <div style={{ fontSize: '1.2rem', fontWeight: 700 }}>{formatDate(info.serverTimeUtc.slice(0, 10))} {info.serverTimeUtc.slice(11, 19)}</div>
            </div>
          </div>

          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Metric</th>
                  <th style={{ textAlign: 'right' }}>Value</th>
                </tr>
              </thead>
              <tbody>
                <tr><td>Total users</td><td style={{ textAlign: 'right' }}>{info.totalUsers}</td></tr>
                <tr><td>Active users</td><td style={{ textAlign: 'right' }}><Badge tone="success">{info.activeUsers}</Badge></td></tr>
                <tr><td>Inactive users</td><td style={{ textAlign: 'right' }}><Badge tone={info.inactiveUsers > 0 ? 'warning' : 'neutral'}>{info.inactiveUsers}</Badge></td></tr>
                <tr><td>Organizations</td><td style={{ textAlign: 'right' }}>{info.totalOrganizations}</td></tr>
                <tr><td>Active organizations</td><td style={{ textAlign: 'right' }}><Badge tone="success">{info.activeOrganizations}</Badge></td></tr>
                <tr><td>Pending organization approvals</td><td style={{ textAlign: 'right' }}><Badge tone={info.pendingOrganizations > 0 ? 'warning' : 'neutral'}>{info.pendingOrganizations}</Badge></td></tr>
              </tbody>
            </table>
          </div>

          {Object.keys(info.usersByRole).length > 0 && (
            <div className="table-wrap" style={{ marginTop: '1.5rem' }}>
              <table>
                <thead>
                  <tr>
                    <th>Role</th>
                    <th style={{ textAlign: 'right' }}>Users</th>
                  </tr>
                </thead>
                <tbody>
                  {Object.entries(info.usersByRole).sort(([a], [b]) => a.localeCompare(b)).map(([role, count]) => (
                    <tr key={role}><td>{role}</td><td style={{ textAlign: 'right' }}>{count}</td></tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  );
}

export default AdminSystemPage;
