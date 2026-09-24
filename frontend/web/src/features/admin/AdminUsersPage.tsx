import { useEffect, useMemo, useState } from 'react';
import { RefreshCw, UserPlus, Users } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Modal } from '../../components/ui/Modal';
import {
  createInventoryOfficer,
  createVeterinarian,
  getOrganizations,
  getUsers,
  setUserActive,
  type AdminOrganization,
  type AdminUser,
  type CreatedStaffUser,
} from '../../services/adminService';
import { messageFrom } from '../../utils/errors';
import { formatDate } from '../../utils/format';

const roleTone: Record<string, 'success' | 'warning' | 'neutral' | 'danger' | 'info'> = {
  Administrator: 'danger',
  ClinicManager: 'info',
  Veterinarian: 'success',
  InventoryOfficer: 'warning',
  PetOwner: 'neutral',
};

/** Users & Settings — administrator view of every platform account. */
export function AdminUsersPage() {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [roleFilter, setRoleFilter] = useState('All');
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [actioningId, setActioningId] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const [createRole, setCreateRole] = useState<'Veterinarian' | 'InventoryOfficer' | null>(null);
  const [activeOrgs, setActiveOrgs] = useState<AdminOrganization[]>([]);
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', phoneNumber: '', organizationId: '' });
  const [creating, setCreating] = useState(false);
  const [created, setCreated] = useState<CreatedStaffUser | null>(null);

  const openCreateModal = async (role: 'Veterinarian' | 'InventoryOfficer') => {
    setCreateRole(role);
    setCreated(null);
    setError('');
    setForm({ firstName: '', lastName: '', email: '', phoneNumber: '', organizationId: '' });
    try {
      const orgs = await getOrganizations();
      setActiveOrgs(orgs.filter((o) => o.status === 'Active'));
    } catch (err) {
      setError(messageFrom(err));
    }
  };

  const submitCreate = async () => {
    if (!createRole) return;
    setCreating(true);
    setError('');
    try {
      const request = {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        phoneNumber: form.phoneNumber.trim() || undefined,
        organizationId: form.organizationId,
      };
      const result = createRole === 'Veterinarian'
        ? await createVeterinarian(request)
        : await createInventoryOfficer(request);
      setCreated(result);
      await load();
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setCreating(false);
    }
  };

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      setUsers(await getUsers());
    } catch (err) {
      setError(messageFrom(err));
      setUsers([]);
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

  const roles = useMemo(() => Array.from(new Set(users.map((u) => u.role))).sort(), [users]);

  const visibleUsers = useMemo(() => {
    const term = search.trim().toLowerCase();
    return users.filter((u) =>
      (roleFilter === 'All' || u.role === roleFilter)
      && (!term || u.name.toLowerCase().includes(term) || u.email.toLowerCase().includes(term)));
  }, [users, roleFilter, search]);

  const toggleActive = async (user: AdminUser) => {
    setActioningId(user.id);
    setError('');
    try {
      await setUserActive(user.id, !user.active);
      setSuccess(`${user.name} ${user.active ? 'deactivated' : 'activated'}.`);
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
          <h2>Users &amp; Settings</h2>
          <p>Every platform account — activate or deactivate access here.</p>
        </div>
        <div className="heading-actions" style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <input
            type="search"
            placeholder="Search name or email…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Search users"
          />
          <select value={roleFilter} onChange={(e) => setRoleFilter(e.target.value)} aria-label="Filter by role">
            <option value="All">All roles</option>
            {roles.map((r) => <option key={r} value={r}>{r}</option>)}
          </select>
          <Button variant="secondary" onClick={() => void load()} icon={<RefreshCw size={14} />}>Refresh</Button>
          <Button variant="secondary" onClick={() => void openCreateModal('Veterinarian')} icon={<UserPlus size={14} />}>Create Veterinarian</Button>
          <Button variant="secondary" onClick={() => void openCreateModal('InventoryOfficer')} icon={<UserPlus size={14} />}>Create Inventory Officer</Button>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: '16px' }}><strong>Error:</strong> {error}</div>}
      {success && (
        <div className="info-strip" style={{ marginBottom: '16px', borderColor: 'var(--success)', background: 'var(--success-soft)', color: 'var(--success)' }}>
          <strong>Success:</strong> <span>{success}</span>
        </div>
      )}

      {loading ? (
        <div className="empty-state"><strong>Loading users…</strong></div>
      ) : visibleUsers.length === 0 ? (
        <div className="empty-state"><Users size={28} /><strong>No users found.</strong></div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>User</th>
                <th>Role</th>
                <th>Organization</th>
                <th>Status</th>
                <th>Joined</th>
                <th style={{ textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {visibleUsers.map((user) => (
                <tr key={user.id}>
                  <td>
                    <strong>{user.name || `${user.firstName} ${user.lastName}`.trim() || '—'}</strong>
                    <div className="muted" style={{ fontSize: '0.78rem' }}>{user.email}</div>
                  </td>
                  <td><Badge tone={roleTone[user.role] ?? 'neutral'}>{user.role}</Badge></td>
                  <td>{user.organizationName ?? '—'}</td>
                  <td>
                    <Badge tone={user.active ? 'success' : 'neutral'}>{user.active ? 'Active' : 'Inactive'}</Badge>
                    {user.mustChangePassword && (
                      <Badge tone="warning">Must change password</Badge>
                    )}
                  </td>
                  <td>{user.createdAt ? formatDate(user.createdAt.slice(0, 10)) : '—'}</td>
                  <td style={{ textAlign: 'right' }}>
                    <Button
                      variant="secondary"
                      disabled={actioningId === user.id}
                      onClick={() => void toggleActive(user)}
                    >
                      {user.active ? 'Deactivate' : 'Activate'}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {createRole && (
        <Modal title={`Create ${createRole === 'Veterinarian' ? 'Veterinarian' : 'Inventory Officer'}`} onClose={() => setCreateRole(null)}>
          {created ? (
            <div>
              <div className="info-strip" style={{ borderColor: 'var(--success)', background: 'var(--success-soft)', color: 'var(--success)' }}>
                <strong>Account created.</strong> <span>{created.email} — {created.role}</span>
              </div>
              <p style={{ marginTop: '12px' }}>
                Temporary password (shown once — give it to the staff member; they must change it on first login):
              </p>
              <code style={{ display: 'block', padding: '10px', marginTop: '8px', background: 'var(--surface-muted, #f4f4f4)', borderRadius: '6px', fontSize: '1rem', userSelect: 'all' }}>
                {created.temporaryPassword}
              </code>
              <div style={{ marginTop: '16px', textAlign: 'right' }}>
                <Button variant="secondary" onClick={() => setCreateRole(null)}>Done</Button>
              </div>
            </div>
          ) : (
            <form
              onSubmit={(e) => { e.preventDefault(); void submitCreate(); }}
              style={{ display: 'grid', gap: '10px', minWidth: '320px' }}
            >
              <input required placeholder="First name" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} aria-label="First name" />
              <input required placeholder="Last name" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} aria-label="Last name" />
              <input required type="email" placeholder="Email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} aria-label="Email" />
              <input placeholder="Phone number (optional)" value={form.phoneNumber} onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })} aria-label="Phone number" />
              <select required value={form.organizationId} onChange={(e) => setForm({ ...form, organizationId: e.target.value })} aria-label="Organization">
                <option value="" disabled>Select organization…</option>
                {activeOrgs.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
              </select>
              {activeOrgs.length === 0 && (
                <p className="muted" style={{ fontSize: '0.8rem' }}>No active organizations — approve one under Organizations first.</p>
              )}
              {error && <div className="form-error"><strong>Error:</strong> {error}</div>}
              <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end', marginTop: '6px' }}>
                <Button variant="ghost" type="button" onClick={() => setCreateRole(null)}>Cancel</Button>
                <Button type="submit" disabled={creating || !form.organizationId}>
                  {creating ? 'Creating…' : 'Create account'}
                </Button>
              </div>
            </form>
          )}
        </Modal>
      )}
    </div>
  );
}

export default AdminUsersPage;
