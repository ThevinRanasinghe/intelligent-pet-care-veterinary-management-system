import { useNavigate } from 'react-router-dom';
import { LayoutDashboard, Calendar, ClipboardCheck, FileText, Package, Stethoscope, PawPrint, Sparkles } from 'lucide-react';
import DashboardLayout from '../shared/DashboardLayout';

/**
 * Clinic Manager dashboard — operational command center.
 * Navigation points to existing Merge_1 pages (scheduling, billing, approvals, inventory, etc.).
 */
export function ClinicManagerDashboard() {
  const navigate = useNavigate();

  const navItems = [
    {
      label: 'Clinic Overview',
      icon: <LayoutDashboard size={18} />,
      active: false,
      onClick: () => navigate('/'),
    },
    {
      label: 'Approval Center',
      icon: <ClipboardCheck size={18} />,
      active: false,
      onClick: () => navigate('/approvals'),
    },
    {
      label: 'Scheduling',
      icon: <Calendar size={18} />,
      active: false,
      onClick: () => navigate('/scheduling'),
    },
    {
      label: 'Quotations & Billing',
      icon: <FileText size={18} />,
      active: false,
      onClick: () => navigate('/billing'),
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
      label: 'Medicine & Inventory',
      icon: <Package size={18} />,
      active: false,
      onClick: () => navigate('/inventory'),
    },
    {
      label: 'AI Workflows',
      icon: <Sparkles size={18} />,
      active: false,
      onClick: () => navigate('/ai-workflows'),
    },
  ];

  return (
    <DashboardLayout
      pageTitle="Clinic Manager Overview"
      pageSubtitle="Live operational command center for your clinic"
      navItems={navItems}
    >
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Today's Appointments</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Loading from scheduling API</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Pending Approvals</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Use Approval Center to review</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Active Quotations</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>View in Billing section</div>
        </div>
      </div>

      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Clinic Operations</h3>
        <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
          Use the sidebar navigation to access scheduling, billing, approvals, consultations, treatment records, and inventory management. All existing Merge_1 functionality is available through these links.
        </p>
      </div>
    </DashboardLayout>
  );
}

export default ClinicManagerDashboard;
