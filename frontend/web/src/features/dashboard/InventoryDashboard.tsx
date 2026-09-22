import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  getExpiring,
  getLowStock,
  getMedicines,
  getReservations,
  getSuppliers,
} from '../../services/inventoryService';
import type { Medicine, MedicineBatch, MedicineReservation, Supplier } from '../../types/domain';

const cardStyle = {
  backgroundColor: '#ffffff',
  padding: '1.5rem',
  borderRadius: '0.75rem',
  border: '1px solid #e5e7eb',
} as const;

/**
 * Inventory Officer dashboard — stock levels, alerts, and reservations
 * built from real inventory API data. Renders inside DashboardLayout.
 */
export function InventoryDashboard() {
  const [medicines, setMedicines] = useState<Medicine[]>([]);
  const [lowStock, setLowStock] = useState<Medicine[]>([]);
  const [expiring, setExpiring] = useState<MedicineBatch[]>([]);
  const [reservations, setReservations] = useState<MedicineReservation[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        const [paged, low, exp, res, sup] = await Promise.all([
          getMedicines({ page: 1, pageSize: 500 }),
          getLowStock(),
          getExpiring(30),
          getReservations(),
          getSuppliers(),
        ]);
        if (!active) return;
        setMedicines(paged.items);
        setLowStock(low);
        setExpiring(exp);
        setReservations(res);
        setSuppliers(sup);
      } catch {
        if (active) setError('Could not load inventory data. Please try again shortly.');
      } finally {
        if (active) setLoading(false);
      }
    }

    void load();
    return () => { active = false; };
  }, []);

  const totalUnits = medicines.reduce((sum, m) => sum + m.availableQuantity, 0);
  const reservedUnits = medicines.reduce((sum, m) => sum + m.reservedQuantity, 0);
  const pendingReservations = reservations.filter((r) => r.status === 'Reserved');
  const activeSuppliers = suppliers.filter((s) => s.status === 'Active');

  return (
    <>
      {error && <div className="form-error" role="alert" style={{ marginBottom: '1rem' }}><strong>Error:</strong> {error}</div>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Medicines in Catalog</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>{loading ? '…' : medicines.length}</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>{loading ? '' : `${totalUnits} units available · ${reservedUnits} reserved`}</div>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Low Stock Medicines</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: lowStock.length > 0 ? '#F59E0B' : '#111827', marginTop: '0.25rem' }}>{loading ? '…' : lowStock.length}</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>At or below reorder level</div>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Batches Expiring ≤ 30 Days</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: expiring.length > 0 ? '#DC2626' : '#111827', marginTop: '0.25rem' }}>{loading ? '…' : expiring.length}</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Review for FEFO dispensing</div>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Pending Reservations</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>{loading ? '…' : pendingReservations.length}</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Awaiting dispense or cancel</div>
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Active Suppliers</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>{loading ? '…' : activeSuppliers.length}</div>
          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Verified supply partners</div>
        </div>
      </div>

      <div style={{ ...cardStyle, padding: '1.75rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Low Stock Alerts</h3>
        {loading ? (
          <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>Loading…</p>
        ) : lowStock.length === 0 ? (
          <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
            All medicines are above their reorder levels.
          </p>
        ) : (
          <div style={{ display: 'grid', gap: '0.6rem' }}>
            {lowStock.slice(0, 5).map((med) => (
              <div key={med.id} className="info-strip" style={{ justifyContent: 'space-between', gap: '0.75rem', flexWrap: 'wrap' }}>
                <div>
                  <strong>{med.name}</strong>
                  <span className="muted"> · {med.category}</span>
                </div>
                <span className="muted" style={{ fontSize: '0.8rem' }}>
                  {med.availableQuantity} available · reorder at {med.reorderLevel}
                </span>
              </div>
            ))}
            <Link to="/inventory/alerts" style={{ fontSize: '0.85rem' }}>Review low stock &amp; expiry →</Link>
          </div>
        )}
      </div>
    </>
  );
}

export default InventoryDashboard;
