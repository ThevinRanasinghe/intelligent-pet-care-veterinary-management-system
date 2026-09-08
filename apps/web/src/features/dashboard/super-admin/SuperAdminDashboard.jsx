import { Shield, Users, Server, Settings } from 'lucide-react';
import DashboardLayout from '../../shared/DashboardLayout';

export default function SuperAdminDashboard() {
  const navItems = [
    { label: 'System Overview', icon: <Server size={18} />, active: true },
    { label: 'User Roles & Access', icon: <Users size={18} /> },
    { label: 'Security & Audit Logs', icon: <Shield size={18} /> },
    { label: 'System Config', icon: <Settings size={18} /> },
  ];

  return (
    <DashboardLayout
      pageTitle="SuperAdmin Console"
      pageSubtitle="Full system administration, tenant security, role governance, and audit trails"
      navItems={navItems}
    >
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Registered Users</div>
          <div style={{ fontSize: '2rem', fontWeight: '700', color: '#111827', marginTop: '0.25rem' }}>1,280</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Active Clinics</div>
          <div style={{ fontSize: '2rem', fontWeight: '700', color: '#10b981', marginTop: '0.25rem' }}>6</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>System Health</div>
          <div style={{ fontSize: '2rem', fontWeight: '700', color: '#10b981', marginTop: '0.25rem' }}>100%</div>
        </div>
      </div>

      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '1rem' }}>Governance & Audit Logs</h3>
        <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>
          Identity role modifications, permission changes, and authentication events are logged for security auditing.
        </p>
      </div>
    </DashboardLayout>
  );
}
