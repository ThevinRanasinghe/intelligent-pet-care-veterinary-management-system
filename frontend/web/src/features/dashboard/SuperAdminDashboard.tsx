import { useState } from 'react';
import { Building2, Server, Shield, Users } from 'lucide-react';

const TABS = [
  { key: 'overview', label: 'Platform Overview', icon: <Server size={16} /> },
  { key: 'organizations', label: 'Organizations', icon: <Building2 size={16} /> },
  { key: 'staff', label: 'Staff & Users', icon: <Users size={16} /> },
  { key: 'audit', label: 'Security & Governance', icon: <Shield size={16} /> },
];

/**
 * Super Admin dashboard — platform governance view.
 * Renders inside the shared DashboardLayout shell.
 */
export function SuperAdminDashboard() {
  const [activeSection, setActiveSection] = useState('overview');

  return (
    <>
      {/* Internal section tabs */}
      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1.5rem', flexWrap: 'wrap' }}>
        {TABS.map(tab => (
          <button
            key={tab.key}
            onClick={() => setActiveSection(tab.key)}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '0.5rem',
              padding: '0.5rem 1rem',
              borderRadius: '9999px',
              fontSize: '0.8rem',
              fontWeight: 600,
              border: '1px solid',
              cursor: 'pointer',
              transition: 'all 150ms',
              borderColor: activeSection === tab.key ? 'var(--petcare-black, #111111)' : '#e5e7eb',
              background: activeSection === tab.key ? 'var(--petcare-black, #111111)' : '#ffffff',
              color: activeSection === tab.key ? 'var(--petcare-yellow, #FFBE00)' : '#6b7280',
            }}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </div>

      {activeSection === 'overview' && (
        <>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
            <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
              <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Platform Model</div>
              <div style={{ fontSize: '1.25rem', fontWeight: '700', color: '#111827', marginTop: '0.25rem' }}>Multi-Tenant Veterinary</div>
            </div>
            <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
              <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Data Isolation</div>
              <div style={{ fontSize: '1.25rem', fontWeight: '700', color: '#10b981', marginTop: '0.25rem' }}>Strict Tenant Enforced</div>
            </div>
            <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
              <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Platform Health</div>
              <div style={{ fontSize: '1.25rem', fontWeight: '700', color: '#10b981', marginTop: '0.25rem' }}>100% Operational</div>
            </div>
          </div>

          <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
            <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Multi-Organization Architecture</h3>
            <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
              Beacon Pet Health provides multi-organization data isolation. Each independent veterinary hospital maintains its own clinic manager, veterinarians, and inventory officers.
            </p>
          </div>
        </>
      )}

      {activeSection === 'organizations' && (
        <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
          <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Organization Verification</h3>
          <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
            Approve pending clinic registrations or suspend inactive clinics. Organization management endpoints will be available after the final consolidated migration.
          </p>
        </div>
      )}

      {activeSection === 'staff' && (
        <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
          <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Staff Verification & Team</h3>
          <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
            Verify staff accounts across all organizations. Staff management endpoints will be available after the final consolidated migration.
          </p>
        </div>
      )}

      {activeSection === 'audit' && (
        <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
          <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Auditability & Security Log</h3>
          <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
            All administrative actions — including organization approval, registration rejection (with justification), and clinic suspension — are timestamped and attributed to the executing SuperAdmin user account in PostgreSQL.
          </p>
        </div>
      )}
    </>
  );
}

export default SuperAdminDashboard;
