import { useEffect, useMemo, useState } from 'react';
import { RefreshCw, ShieldCheck } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { getRoles, getUsers, type AdminUser } from '../../services/adminService';
import { messageFrom } from '../../utils/errors';

/** What each platform role is for — mirrors backend authorization scopes. */
const ROLE_DESCRIPTIONS: Record<string, string> = {
  Administrator: 'Platform-level administration: user accounts, organization lifecycle, system configuration.',
  ClinicManager: 'Clinic operations: scheduling, quotations & billing, approvals, reports; read-only clinical and inventory visibility.',
  Veterinarian: 'Clinical work: consultations, examinations, diagnosis & treatment, prescriptions, medical history.',
  InventoryOfficer: 'Pharmacy stock: medicines, batches, suppliers, reservations, low-stock and expiry management.',
  PetOwner: 'Self-service: own pets, consultation requests, care history and prescriptions.',
  Staff: 'General authenticated staff account.',
};

/** Roles & Permissions — the platform role catalog and which users hold each role. */
export function AdminRolesPage() {
  const [roles, setRoles] = useState<string[]>([]);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const [roleList, userList] = await Promise.all([getRoles(), getUsers()]);
      setRoles(roleList);
      setUsers(userList);
    } catch (err) {
      setError(messageFrom(err));
      setRoles([]);
      setUsers([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void load(); }, []);

  const membersByRole = useMemo(() => {
    const map = new Map<string, AdminUser[]>();
    for (const user of users) {
      const list = map.get(user.role) ?? [];
      list.push(user);
      map.set(user.role, list);
    }
    return map;
  }, [users]);

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">ADMINISTRATION</span>
          <h2>Roles &amp; Permissions</h2>
          <p>Platform role catalog and the accounts assigned to each role.</p>
        </div>
        <div className="heading-actions">
          <Button variant="secondary" onClick={() => void load()} icon={<RefreshCw size={14} />}>Refresh</Button>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: '16px' }}><strong>Error:</strong> {error}</div>}

      {loading ? (
        <div className="empty-state"><strong>Loading roles…</strong></div>
      ) : roles.length === 0 ? (
        <div className="empty-state"><ShieldCheck size={28} /><strong>No roles defined.</strong></div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.25rem' }}>
          {roles.map((role) => {
            const members = membersByRole.get(role) ?? [];
            return (
              <div key={role} className="table-wrap" style={{ padding: '1.25rem' }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
                  <h3 style={{ margin: 0, fontSize: '1rem' }}>{role}</h3>
                  <Badge tone="info">{members.length} {members.length === 1 ? 'user' : 'users'}</Badge>
                </div>
                <p className="muted" style={{ fontSize: '0.85rem', marginTop: 0 }}>
                  {ROLE_DESCRIPTIONS[role] ?? 'Custom role defined by the platform.'}
                </p>
                {members.length === 0 ? (
                  <p className="muted" style={{ fontSize: '0.85rem' }}>No accounts hold this role.</p>
                ) : (
                  <ul style={{ margin: 0, paddingLeft: '1.1rem', fontSize: '0.85rem' }}>
                    {members.map((m) => (
                      <li key={m.id}>
                        {m.name || m.email} <span className="muted">({m.email})</span>
                        {!m.active && <Badge tone="neutral">Inactive</Badge>}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default AdminRolesPage;
