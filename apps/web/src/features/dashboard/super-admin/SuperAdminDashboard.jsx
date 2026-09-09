import { useState } from 'react';
import { Building2, Server, Shield, Users } from 'lucide-react';
import DashboardLayout from '../../shared/DashboardLayout';
import OrganizationManagementView from './OrganizationManagementView';

export default function SuperAdminDashboard() {
  const [activeSection, setActiveSection] = useState('organizations'); // 'organizations' | 'overview' | 'audit'

  const navItems = [
    {
      label: 'Organizations & Review',
      icon: <Building2 size={18} />,
      active: activeSection === 'organizations',
      onClick: () => setActiveSection('organizations')
    },
    {
      label: 'System Overview',
      icon: <Server size={18} />,
      active: activeSection === 'overview',
      onClick: () => setActiveSection('overview')
    },
    {
      label: 'Security & Governance',
      icon: <Shield size={18} />,
      active: activeSection === 'audit',
      onClick: () => setActiveSection('audit')
    }
  ];

  return (
    <DashboardLayout
      pageTitle={
        activeSection === 'organizations'
          ? 'Veterinary Organization Governance'
          : activeSection === 'overview'
          ? 'Platform Overview'
          : 'Security & Governance'
      }
      pageSubtitle="Review registrations, verify clinics, manage lifecycle states, and monitor platform multi-tenancy"
      navItems={navItems}
    >
      {activeSection === 'organizations' && <OrganizationManagementView />}

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
              Beacon Pet Health provides multi-organization data isolation. Each independent veterinary hospital maintains its own clinic manager, veterinarians, and inventory officers. Switch to the <strong>Organizations & Review</strong> tab to approve pending clinic registrations or suspend inactive clinics.
            </p>
          </div>
        </>
      )}

      {activeSection === 'audit' && (
        <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
          <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Auditability & Security Log</h3>
          <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
            All administrative actions — including organization approval, registration rejection (with justification), and clinic suspension — are timestamped and attributed to the executing SuperAdmin user account in PostgreSQL.
          </p>
        </div>
      )}
    </DashboardLayout>
  );
}

