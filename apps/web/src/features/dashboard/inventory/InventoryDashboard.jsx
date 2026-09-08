import { Package, AlertTriangle, Truck, Archive } from 'lucide-react';
import DashboardLayout from '../../shared/DashboardLayout';

export default function InventoryDashboard() {
  const navItems = [
    { label: 'Stock Overview', icon: <Package size={18} />, active: true },
    { label: 'Low Stock Alerts', icon: <AlertTriangle size={18} /> },
    { label: 'Shipments', icon: <Truck size={18} /> },
    { label: 'Suppliers', icon: <Archive size={18} /> },
  ];

  return (
    <DashboardLayout
      pageTitle="Inventory Portal"
      pageSubtitle="Track medical supplies, medications, batch expirations, and stock orders"
      navItems={navItems}
    >
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Total SKUs in Stock</div>
          <div style={{ fontSize: '2rem', fontWeight: '700', color: '#111827', marginTop: '0.25rem' }}>142</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Low Stock Items</div>
          <div style={{ fontSize: '2rem', fontWeight: '700', color: '#ef4444', marginTop: '0.25rem' }}>4</div>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Pending Orders</div>
          <div style={{ fontSize: '2rem', fontWeight: '700', color: '#3b82f6', marginTop: '0.25rem' }}>2</div>
        </div>
      </div>

      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '1rem' }}>Inventory Tracking</h3>
        <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>
          Real-time pharmacy and supply stocks can be managed through this console.
        </p>
      </div>
    </DashboardLayout>
  );
}
