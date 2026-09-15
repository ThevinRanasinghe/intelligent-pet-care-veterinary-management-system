import { useState } from 'react';
import { Stethoscope, Calendar, Users, Activity, Package } from 'lucide-react';
import DashboardLayout from '../../shared/DashboardLayout';
import InventoryManagementView from '../inventory/InventoryManagementView';

export default function VeterinarianDashboard() {
  const [activeTab, setActiveTab] = useState('overview');

  const navItems = [
    {
      label: 'Overview',
      icon: <Activity size={18} />,
      active: activeTab === 'overview',
      onClick: () => setActiveTab('overview'),
    },
    {
      label: 'Prescriptions & Stock',
      icon: <Package size={18} />,
      active: activeTab === 'inventory',
      onClick: () => setActiveTab('inventory'),
    },
    {
      label: 'Appointments',
      icon: <Calendar size={18} />,
      active: activeTab === 'appointments',
      onClick: () => setActiveTab('appointments'),
    },
    {
      label: 'Patient Records',
      icon: <Users size={18} />,
      active: activeTab === 'patients',
      onClick: () => setActiveTab('patients'),
    },
    {
      label: 'Clinical Notes',
      icon: <Stethoscope size={18} />,
      active: activeTab === 'notes',
      onClick: () => setActiveTab('notes'),
    },
  ];

  return (
    <DashboardLayout
      pageTitle={activeTab === 'inventory' ? 'Prescriptions & Medicine Inventory' : 'Veterinarian Portal'}
      pageSubtitle={
        activeTab === 'inventory'
          ? 'Check real-time pharmaceutical availability and reserve treatment medications'
          : 'Manage clinical appointments, medical logs, and patient records'
      }
      navItems={navItems}
    >
      {activeTab === 'inventory' ? (
        <InventoryManagementView defaultTab="catalog" />
      ) : (
        <>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
            <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
              <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Today's Consultations</div>
              <div style={{ fontSize: '2rem', fontWeight: '700', color: '#111827', marginTop: '0.25rem' }}>8</div>
            </div>
            <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
              <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Pending Lab Results</div>
              <div style={{ fontSize: '2rem', fontWeight: '700', color: '#f59e0b', marginTop: '0.25rem' }}>3</div>
            </div>
            <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
              <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Emergency Queue</div>
              <div style={{ fontSize: '2rem', fontWeight: '700', color: '#10b981', marginTop: '0.25rem' }}>0</div>
            </div>
          </div>

          <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
            <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '1rem' }}>Upcoming Appointments</h3>
            <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>
              Clinical consultations will load dynamically when veterinary appointments are scheduled.
            </p>
          </div>
        </>
      )}
    </DashboardLayout>
  );
}
