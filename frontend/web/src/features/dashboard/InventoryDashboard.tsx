/**
 * Inventory Officer dashboard — medicine/inventory focused view.
 * Renders inside the shared DashboardLayout shell.
 */
export function InventoryDashboard() {
  return (
    <>
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
    </>
  );
}

export default InventoryDashboard;
