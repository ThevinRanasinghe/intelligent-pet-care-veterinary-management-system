import { useNavigate } from 'react-router-dom';
import { LayoutDashboard, Stethoscope, PawPrint, Calendar, ClipboardCheck } from 'lucide-react';
import DashboardLayout from '../shared/DashboardLayout';

/**
 * Veterinarian dashboard — patient/consultation-oriented view.
 * Navigation points to existing Merge_1 pages.
 */
export function VeterinarianDashboard() {
  const navigate = useNavigate();

  const navItems = [
    {
      label: 'Overview',
      icon: <LayoutDashboard size={18} />,
      active: false,
      onClick: () => navigate('/'),
    },
    {
      label: 'Consultation Requests',
      icon: <PawPrint size={18} />,
      active: false,
      onClick: () => navigate('/consultations'),
    },
    {
      label: 'Diagnosis & Treatment',
      icon: <Stethoscope size={18} />,
      active: false,
      onClick: () => navigate('/treatment'),
    },
    {
      label: 'Examinations',
      icon: <ClipboardCheck size={18} />,
      active: false,
      onClick: () => navigate('/examinations'),
    },
    {
      label: 'Scheduling',
      icon: <Calendar size={18} />,
      active: false,
      onClick: () => navigate('/scheduling'),
    },
  ];

  return (
    <DashboardLayout
      pageTitle="Veterinarian Dashboard"
      pageSubtitle="Patient consultations, examinations, and treatment records"
      navItems={navItems}
    >
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>My Consultations</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>View consultation requests</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Pending Examinations</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Record examinations</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Treatment Records</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Manage treatment plans</div>
        </div>
      </div>

      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Patient Care</h3>
        <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
          Access consultation requests, record examinations, and manage treatment plans through the sidebar navigation.
        </p>
      </div>
    </DashboardLayout>
  );
}

export default VeterinarianDashboard;
