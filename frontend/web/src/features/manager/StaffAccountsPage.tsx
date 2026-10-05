import { useState } from 'react';
import { UserPlus } from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { Modal } from '../../components/ui/Modal';
import {
  createInventoryOfficer,
  createVeterinarian,
  type CreatedStaffAccount,
} from '../../services/managerService';
import { messageFrom } from '../../utils/errors';

const cardStyle = {
  backgroundColor: '#ffffff',
  padding: '1.75rem',
  borderRadius: '0.75rem',
  border: '1px solid #e5e7eb',
} as const;

/**
 * ClinicManager staff-account creation. The organization is bound
 * server-side from the manager's authenticated identity — the form
 * deliberately has no organization selector.
 */
export function StaffAccountsPage() {
  const [createRole, setCreateRole] = useState<'Veterinarian' | 'InventoryOfficer' | null>(null);
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', phoneNumber: '' });
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState('');
  const [created, setCreated] = useState<CreatedStaffAccount | null>(null);

  const openCreateModal = (role: 'Veterinarian' | 'InventoryOfficer') => {
    setCreateRole(role);
    setCreated(null);
    setCreateError('');
    setForm({ firstName: '', lastName: '', email: '', phoneNumber: '' });
  };

  const submitCreate = async () => {
    if (!createRole) return;
    setCreating(true);
    setCreateError('');
    try {
      const request = {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        phoneNumber: form.phoneNumber.trim() || undefined,
      };
      const result = createRole === 'Veterinarian'
        ? await createVeterinarian(request)
        : await createInventoryOfficer(request);
      setCreated(result);
    } catch (err) {
      setCreateError(messageFrom(err));
    } finally {
      setCreating(false);
    }
  };

  return (
    <div style={cardStyle}>
      <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Staff Account Management</h3>
      <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6', marginBottom: '1rem' }}>
        Create staff accounts for your organization. New accounts receive a temporary password and must change it on first login.
      </p>
      <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
        <Button variant="secondary" onClick={() => openCreateModal('Veterinarian')} icon={<UserPlus size={14} />}>Create Veterinarian</Button>
        <Button variant="secondary" onClick={() => openCreateModal('InventoryOfficer')} icon={<UserPlus size={14} />}>Create Inventory Officer</Button>
      </div>

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
              <p className="muted" style={{ fontSize: '0.8rem' }}>Account will be created for your organization.</p>
              {createError && <div className="form-error"><strong>Error:</strong> {createError}</div>}
              <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end', marginTop: '6px' }}>
                <Button variant="ghost" type="button" onClick={() => setCreateRole(null)}>Cancel</Button>
                <Button type="submit" disabled={creating}>
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

export default StaffAccountsPage;
