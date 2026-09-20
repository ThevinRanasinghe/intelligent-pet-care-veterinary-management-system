import { useNavigate } from 'react-router-dom';
import { LayoutDashboard, PawPrint, Calendar, Stethoscope } from 'lucide-react';
import DashboardLayout from '../shared/DashboardLayout';

/**
 * Pet Owner landing/dashboard — pet owner's view of their pets and consultations.
 * Navigation points to existing Merge_1 pages.
 */
export function PetOwnerDashboard() {
  const navigate = useNavigate();

  const navItems = [
    {
      label: 'My Pets',
      icon: <PawPrint size={18} />,
      active: false,
      onClick: () => navigate('/pets'),
    },
    {
      label: 'Consultation Requests',
      icon: <LayoutDashboard size={18} />,
      active: false,
      onClick: () => navigate('/consultations'),
    },
    {
      label: 'Treatment Records',
      icon: <Stethoscope size={18} />,
      active: false,
      onClick: () => navigate('/treatment'),
    },
    {
      label: 'Appointments',
      icon: <Calendar size={18} />,
      active: false,
      onClick: () => navigate('/scheduling'),
    },
  ];

  return (
    <DashboardLayout
      pageTitle="My Pet Care"
      pageSubtitle="Manage your pets, consultation requests, and treatment history"
      navItems={navItems}
    >
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>My Pets</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>View and manage your pets</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Active Consultations</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Track consultation status</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Treatment History</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>View past treatments</div>
        </div>
      </div>

      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Welcome to Beacon Pet Health</h3>
        <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
          Manage your pets, request consultations, and review treatment records through the sidebar navigation. Your veterinary care team is here to help.
        </p>
      </div>
    </DashboardLayout>
  );
}

export default PetOwnerDashboard;
