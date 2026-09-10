import { useState, useEffect, useCallback } from 'react';
import {
  Building2, Mail, Phone, MapPin, ShieldCheck, CheckCircle2,
  AlertCircle, Edit2, Save, X, RefreshCw
} from 'lucide-react';
import { getMyOrganization, updateMyOrganization } from '../services/adminApi';

export default function OrganizationProfileView() {
  const [org, setOrg] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  const [isEditing, setIsEditing] = useState(false);
  const [form, setForm] = useState({
    name: '',
    registrationNumber: '',
    email: '',
    phone: '',
    address: '',
    city: '',
    country: ''
  });
  const [formErrors, setFormErrors] = useState({});
  const [saving, setSaving] = useState(false);

  const fetchOrg = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getMyOrganization();
      setOrg(data);
      setForm({
        name: data.name || '',
        registrationNumber: data.registrationNumber || '',
        email: data.email || '',
        phone: data.phone || '',
        address: data.address || '',
        city: data.city || '',
        country: data.country || ''
      });
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to load organization workspace details.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchOrg();
  }, [fetchOrg]);

  const validate = () => {
    const errs = {};
    if (!form.name.trim()) errs.name = 'Organization name is required.';
    if (!form.email.trim()) errs.email = 'Email address is required.';
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) errs.email = 'Valid email required.';
    if (!form.phone.trim()) errs.phone = 'Phone number is required.';
    if (!form.address.trim()) errs.address = 'Street address is required.';
    if (!form.city.trim()) errs.city = 'City is required.';
    if (!form.country.trim()) errs.country = 'Country is required.';
    return errs;
  };

  const handleSave = async (e) => {
    e.preventDefault();
    const errs = validate();
    if (Object.keys(errs).length > 0) {
      setFormErrors(errs);
      return;
    }

    setSaving(true);
    setFormErrors({});
    try {
      const updated = await updateMyOrganization(form);
      setOrg(updated);
      setIsEditing(false);
      setSuccessMsg('Organization profile updated successfully.');
      setTimeout(() => setSuccessMsg(''), 4000);
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to update organization profile.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div style={{ padding: '4rem', textAlign: 'center', color: '#6b7280' }}>
        <RefreshCw className="animate-spin" size={28} style={{ color: '#0d9488', margin: '0 auto 0.75rem' }} />
        <p style={{ margin: 0 }}>Loading organization profile...</p>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem', maxWidth: '900px' }}>
      {/* Alert Banners */}
      {successMsg && (
        <div style={{ padding: '0.875rem 1rem', backgroundColor: '#ecfdf5', border: '1px solid #a7f3d0', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#065f46', fontSize: '0.875rem' }}>
          <CheckCircle2 size={18} />
          <span>{successMsg}</span>
        </div>
      )}
      {error && (
        <div style={{ padding: '0.875rem 1rem', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#991b1b', fontSize: '0.875rem' }}>
          <AlertCircle size={18} />
          <span>{error}</span>
        </div>
      )}

      {/* Main Profile Card */}
      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.875rem', border: '1px solid #e5e7eb', overflow: 'hidden' }}>
        {/* Header */}
        <div style={{ padding: '1.5rem', borderBottom: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.875rem' }}>
            <div style={{ width: 44, height: 44, borderRadius: '0.625rem', backgroundColor: '#f0fdfa', border: '1px solid #99f6e4', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Building2 size={24} style={{ color: '#0d9488' }} />
            </div>
            <div>
              <h2 style={{ fontSize: '1.25rem', fontWeight: '700', color: '#111827', margin: 0 }}>
                {org?.name || 'Veterinary Organization'}
              </h2>
              <span style={{ fontSize: '0.8rem', color: '#6b7280' }}>
                Registration ID: {org?.registrationNumber || 'N/A'} • Multi-Tenant ID: {org?.id}
              </span>
            </div>
          </div>

          {!isEditing ? (
            <button
              onClick={() => setIsEditing(true)}
              style={{
                padding: '0.5rem 1rem',
                backgroundColor: '#0d9488',
                color: '#fff',
                borderRadius: '0.5rem',
                border: 'none',
                fontWeight: '600',
                fontSize: '0.875rem',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '0.375rem'
              }}
            >
              <Edit2 size={15} /> Edit Workspace Details
            </button>
          ) : (
            <button
              onClick={() => {
                setIsEditing(false);
                setFormErrors({});
              }}
              style={{
                padding: '0.5rem 1rem',
                backgroundColor: '#fff',
                color: '#374151',
                borderRadius: '0.5rem',
                border: '1px solid #d1d5db',
                fontWeight: '500',
                fontSize: '0.875rem',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '0.375rem'
              }}
            >
              <X size={15} /> Cancel
            </button>
          )}
        </div>

        {/* Form / Read View */}
        <form onSubmit={handleSave} style={{ padding: '1.75rem', display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
          {/* Status & Compliance Bar */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem', backgroundColor: '#f9fafb', padding: '1rem', borderRadius: '0.75rem' }}>
            <div>
              <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Platform Status</div>
              <div style={{ marginTop: '0.25rem' }}>
                <span style={{
                  padding: '0.2rem 0.6rem',
                  borderRadius: '9999px',
                  backgroundColor: org?.status === 'Active' ? '#dcfce7' : '#fef3c7',
                  color: org?.status === 'Active' ? '#15803d' : '#b45309',
                  fontSize: '0.75rem',
                  fontWeight: '700'
                }}>
                  {org?.status || 'Active'}
                </span>
              </div>
            </div>

            <div>
              <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Tenant Isolation</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', marginTop: '0.25rem', color: '#10b981', fontWeight: '600', fontSize: '0.875rem' }}>
                <ShieldCheck size={16} /> Strict Backend Scoped
              </div>
            </div>

            <div>
              <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Registered Staff Count</div>
              <div style={{ fontSize: '1rem', fontWeight: '700', color: '#111827', marginTop: '0.15rem' }}>
                {org?.staffCount ?? 0} Staff Accounts
              </div>
            </div>
          </div>

          {/* Form Fields */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1.25rem' }}>
            <div>
              <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                Organization Name *
              </label>
              {isEditing ? (
                <>
                  <input
                    type="text"
                    value={form.name}
                    onChange={e => setForm({ ...form, name: e.target.value })}
                    style={{ width: '100%', padding: '0.6rem 0.75rem', borderRadius: '0.5rem', border: '1px solid #d1d5db', fontSize: '0.875rem' }}
                  />
                  {formErrors.name && <span style={{ color: '#dc2626', fontSize: '0.75rem' }}>{formErrors.name}</span>}
                </>
              ) : (
                <div style={{ fontSize: '0.95rem', fontWeight: '500', color: '#111827' }}>{org?.name}</div>
              )}
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                Registration / License Number
              </label>
              {isEditing ? (
                <input
                  type="text"
                  value={form.registrationNumber}
                  onChange={e => setForm({ ...form, registrationNumber: e.target.value })}
                  style={{ width: '100%', padding: '0.6rem 0.75rem', borderRadius: '0.5rem', border: '1px solid #d1d5db', fontSize: '0.875rem' }}
                />
              ) : (
                <div style={{ fontSize: '0.95rem', color: '#374151' }}>{org?.registrationNumber || 'Not specified'}</div>
              )}
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                Clinic Email Address *
              </label>
              {isEditing ? (
                <>
                  <input
                    type="email"
                    value={form.email}
                    onChange={e => setForm({ ...form, email: e.target.value })}
                    style={{ width: '100%', padding: '0.6rem 0.75rem', borderRadius: '0.5rem', border: '1px solid #d1d5db', fontSize: '0.875rem' }}
                  />
                  {formErrors.email && <span style={{ color: '#dc2626', fontSize: '0.75rem' }}>{formErrors.email}</span>}
                </>
              ) : (
                <div style={{ fontSize: '0.95rem', color: '#374151' }}>{org?.email}</div>
              )}
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                Primary Phone Number *
              </label>
              {isEditing ? (
                <>
                  <input
                    type="text"
                    value={form.phone}
                    onChange={e => setForm({ ...form, phone: e.target.value })}
                    style={{ width: '100%', padding: '0.6rem 0.75rem', borderRadius: '0.5rem', border: '1px solid #d1d5db', fontSize: '0.875rem' }}
                  />
                  {formErrors.phone && <span style={{ color: '#dc2626', fontSize: '0.75rem' }}>{formErrors.phone}</span>}
                </>
              ) : (
                <div style={{ fontSize: '0.95rem', color: '#374151' }}>{org?.phone}</div>
              )}
            </div>

            <div style={{ gridColumn: '1 / -1' }}>
              <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                Street Address *
              </label>
              {isEditing ? (
                <>
                  <input
                    type="text"
                    value={form.address}
                    onChange={e => setForm({ ...form, address: e.target.value })}
                    style={{ width: '100%', padding: '0.6rem 0.75rem', borderRadius: '0.5rem', border: '1px solid #d1d5db', fontSize: '0.875rem' }}
                  />
                  {formErrors.address && <span style={{ color: '#dc2626', fontSize: '0.75rem' }}>{formErrors.address}</span>}
                </>
              ) : (
                <div style={{ fontSize: '0.95rem', color: '#374151' }}>{org?.address}</div>
              )}
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                City *
              </label>
              {isEditing ? (
                <>
                  <input
                    type="text"
                    value={form.city}
                    onChange={e => setForm({ ...form, city: e.target.value })}
                    style={{ width: '100%', padding: '0.6rem 0.75rem', borderRadius: '0.5rem', border: '1px solid #d1d5db', fontSize: '0.875rem' }}
                  />
                  {formErrors.city && <span style={{ color: '#dc2626', fontSize: '0.75rem' }}>{formErrors.city}</span>}
                </>
              ) : (
                <div style={{ fontSize: '0.95rem', color: '#374151' }}>{org?.city}</div>
              )}
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                Country *
              </label>
              {isEditing ? (
                <>
                  <input
                    type="text"
                    value={form.country}
                    onChange={e => setForm({ ...form, country: e.target.value })}
                    style={{ width: '100%', padding: '0.6rem 0.75rem', borderRadius: '0.5rem', border: '1px solid #d1d5db', fontSize: '0.875rem' }}
                  />
                  {formErrors.country && <span style={{ color: '#dc2626', fontSize: '0.75rem' }}>{formErrors.country}</span>}
                </>
              ) : (
                <div style={{ fontSize: '0.95rem', color: '#374151' }}>{org?.country}</div>
              )}
            </div>
          </div>

          {isEditing && (
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '0.5rem' }}>
              <button
                type="button"
                onClick={() => setIsEditing(false)}
                style={{
                  padding: '0.55rem 1rem',
                  borderRadius: '0.5rem',
                  border: '1px solid #d1d5db',
                  backgroundColor: '#fff',
                  color: '#374151',
                  fontSize: '0.875rem',
                  cursor: 'pointer'
                }}
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={saving}
                style={{
                  padding: '0.55rem 1.25rem',
                  borderRadius: '0.5rem',
                  border: 'none',
                  backgroundColor: '#0d9488',
                  color: '#fff',
                  fontSize: '0.875rem',
                  fontWeight: '600',
                  cursor: saving ? 'not-allowed' : 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.375rem'
                }}
              >
                <Save size={16} /> {saving ? 'Saving...' : 'Save Changes'}
              </button>
            </div>
          )}
        </form>
      </div>
    </div>
  );
}
