import { Users, DollarSign, CalendarCheck, BarChart3 } from 'lucide-react';
import DashboardLayout from '../../shared/DashboardLayout';

export default function ClinicManagerDashboard() {
  const navItems = [
    { label: 'Executive Summary', icon: <BarChart3 size={18} />, active: true },
    { label: 'Staff Management', icon: <Users size={18} /> },
    { label: 'Billing & Invoices', icon: <DollarSign size={18} /> },
    { label: 'Clinic Schedule', icon: <CalendarCheck size={18} /> },
  ];

  return (
    <DashboardLayout
      pageTitle="Clinic Manager Portal"
      pageSubtitle="Oversee clinic operations, staff scheduling, revenue, and client satisfaction"
      navItems={navItems}
    >
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Active Staff On Duty</div>
          <div style={{ fontSize: '2rem', fontWeight: '700', color: '#111827', marginTop: '0.25rem' }}>12</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Today's Revenue</div>
          <div style={{ fontSize: '2rem', fontWeight: '700', color: '#10b981', marginTop: '0.25rem' }}>$2,450</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Bed Occupancy</div>
          <div style={{ fontSize: '2rem', fontWeight: '700', color: '#6366f1', marginTop: '0.25rem' }}>75%</div>
        </div>
      </div>

      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '1rem' }}>Clinic Operations</h3>
        <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>
          Monitor daily throughput, veterinarian caseloads, and clinic financial summaries.
        </p>
      </div>
    </DashboardLayout>
  );
}
