import { useNavigate } from 'react-router-dom';
import { LayoutDashboard, Package, AlertTriangle, Calendar } from 'lucide-react';
import DashboardLayout from '../shared/DashboardLayout';

/**
 * Inventory Officer dashboard — medicine/inventory focused view.
 * Navigation points to the existing Merge_1 InventoryPage.
 */
export function InventoryDashboard() {
  const navigate = useNavigate();

  const navItems = [
    {
      label: 'Inventory Overview',
      icon: <LayoutDashboard size={18} />,
      active: false,
      onClick: () => navigate('/'),
    },
    {
      label: 'Medicine & Inventory',
      icon: <Package size={18} />,
      active: false,
      onClick: () => navigate('/inventory'),
    },
    {
      label: 'Low Stock Alerts',
      icon: <AlertTriangle size={18} />,
      active: false,
      onClick: () => navigate('/inventory'),
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
      pageTitle="Inventory Officer Dashboard"
      pageSubtitle="Pharmaceutical stock tracking, FEFO batch dispensing, and supplier directory"
      navItems={navItems}
    >
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Total Medicines</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>View medicine catalog</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Low Stock Items</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#F59E0B', marginTop: '0.25rem' }}>—</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Items below reorder level</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Expiring Batches</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#DC2626', marginTop: '0.25rem' }}>—</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Batches expiring soon</div>
        </div>
      </div>

      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Inventory Management</h3>
        <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
          Use the Medicine & Inventory link in the sidebar to access the full inventory management interface — medicine catalog, batch tracking, reservations, stock-in, and supplier management.
        </p>
      </div>
    </DashboardLayout>
  );
}

export default InventoryDashboard;
