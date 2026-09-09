import { useState } from 'react';
import { Users, BarChart3, Building2 } from 'lucide-react';
import DashboardLayout from '../../shared/DashboardLayout';
import StaffManagementView from './StaffManagementView';
import useAuthStore from '../../../store/authStore';

export default function ClinicManagerDashboard() {
  const [activeSection, setActiveSection] = useState('staff'); // 'staff' | 'summary'
  const { user } = useAuthStore();

  const navItems = [
    {
      label: 'Staff Management',
      icon: <Users size={18} />,
      active: activeSection === 'staff',
      onClick: () => setActiveSection('staff')
    },
    {
      label: 'Clinic Summary',
      icon: <BarChart3 size={18} />,
      active: activeSection === 'summary',
      onClick: () => setActiveSection('summary')
    },
  ];

  return (
    <DashboardLayout
      pageTitle={activeSection === 'staff' ? 'Staff & Team Management' : 'Clinic Manager Portal'}
      pageSubtitle={
        user?.organization
          ? `Organization: ${user.organization.name} (${user.organization.status})`
          : 'Oversee clinic operations, staff credentials, and organization team'
      }
      navItems={navItems}
    >
      {activeSection === 'staff' && <StaffManagementView />}

      {activeSection === 'summary' && (
        <>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
            <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
              <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Organization Name</div>
              <div style={{ fontSize: '1.25rem', fontWeight: '700', color: '#111827', marginTop: '0.25rem' }}>
                {user?.organization?.name || 'Veterinary Organization'}
              </div>
            </div>
            <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
              <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Account Authority</div>
              <div style={{ fontSize: '1.25rem', fontWeight: '700', color: '#10b981', marginTop: '0.25rem' }}>
                Clinic Manager
              </div>
            </div>
            <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
              <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Organization Isolation</div>
              <div style={{ fontSize: '1.25rem', fontWeight: '700', color: '#6366f1', marginTop: '0.25rem' }}>
                Tenant Secure
              </div>
            </div>
          </div>

          <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
            <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Organization Overview</h3>
            <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
              As a Clinic Manager, you manage operational staff (Veterinarians and Inventory Officers) strictly for your own veterinary organization. Switch to the <strong>Staff Management</strong> tab to add new team members or manage account access.
            </p>
          </div>
        </>
      )}
    </DashboardLayout>
  );
}

