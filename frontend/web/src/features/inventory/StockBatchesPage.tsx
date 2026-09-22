import { useEffect, useMemo, useState } from 'react';
import { Layers, PawPrint, Search } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { getBatches, getMedicines, getSuppliers } from '../../services/inventoryService';
import type { Medicine, MedicineBatch, Supplier } from '../../types/domain';
import { formatDate } from '../../utils/format';

type BatchRow = MedicineBatch & { medicineName: string; supplierName: string };

const EXPIRING_SOON_DAYS = 30;

function batchTone(batch: MedicineBatch): 'danger' | 'warning' | 'neutral' | 'success' {
  if (batch.isExpired) return 'danger';
  if (batch.quantity === 0) return 'neutral';
  const days = (new Date(batch.expiryDate).getTime() - Date.now()) / 86400000;
  return days <= EXPIRING_SOON_DAYS ? 'warning' : 'success';
}

function batchLabel(batch: MedicineBatch): string {
  if (batch.isExpired) return 'Expired';
  if (batch.quantity === 0) return 'Depleted';
  const days = (new Date(batch.expiryDate).getTime() - Date.now()) / 86400000;
  return days <= EXPIRING_SOON_DAYS ? 'Expiring Soon' : 'Active';
}

/** Stock &amp; Batches — every medicine batch with quantities and expiry. */
export function StockBatchesPage() {
  const [rows, setRows] = useState<BatchRow[]>([]);
  const [medicines, setMedicines] = useState<Medicine[]>([]);
  const [medicineFilter, setMedicineFilter] = useState('All');
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState('');

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setMessage('');
        const [paged, supplierList] = await Promise.all([
          getMedicines({ page: 1, pageSize: 500 }),
          getSuppliers().catch(() => [] as Supplier[]),
        ]);
        if (!active) return;

        setMedicines(paged.items);
        const supplierById = new Map(supplierList.map((s) => [s.id, s.name]));

        const batchGroups = await Promise.all(
          paged.items.map((med) => getBatches(med.id).catch(() => [] as MedicineBatch[])),
        );
        if (!active) return;

        setRows(
          paged.items.flatMap((med, index) =>
            batchGroups[index].map((batch) => ({
              ...batch,
              medicineName: med.name,
              supplierName: supplierById.get(batch.supplierId) ?? '—',
            })),
          ),
        );
      } catch {
        if (active) {
          setRows([]);
          setMessage('Could not load stock batches. Please try again shortly.');
        }
      } finally {
        if (active) setLoading(false);
      }
    }

    void load();
    return () => { active = false; };
  }, []);

  const visibleRows = useMemo(() => {
    const normalized = query.trim().toLowerCase();
    return rows
      .filter((row) => medicineFilter === 'All' || row.medicineId === medicineFilter)
      .filter((row) =>
        !normalized ||
        [row.medicineName, row.batchNumber, row.supplierName, batchLabel(row)]
          .join(' ')
          .toLowerCase()
          .includes(normalized),
      );
  }, [rows, medicineFilter, query]);

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">INVENTORY</span>
          <h2>Stock &amp; Batches</h2>
          <p>Every received batch with quantities, received dates, and expiry status.</p>
        </div>
        <div className="heading-actions" style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <div style={{ position: 'relative' }}>
            <Search size={15} style={{ position: 'absolute', left: '0.7rem', top: '50%', transform: 'translateY(-50%)', opacity: 0.5 }} />
            <input
              type="search"
              placeholder="Search batches…"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              style={{ paddingLeft: '2rem', minWidth: 'min(200px, 100%)' }}
            />
          </div>
          <select value={medicineFilter} onChange={(event) => setMedicineFilter(event.target.value)} aria-label="Filter by medicine">
            <option value="All">All medicines</option>
            {medicines.map((med) => (
              <option key={med.id} value={med.id}>{med.name}</option>
            ))}
          </select>
        </div>
      </div>

      {loading ? (
        <div className="empty-state"><strong>Loading batches…</strong></div>
      ) : message ? (
        <div className="empty-state"><PawPrint size={28} /><strong>{message}</strong></div>
      ) : visibleRows.length === 0 ? (
        <div className="empty-state"><Layers size={28} /><strong>No stock batches found.</strong></div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Medicine</th>
                <th>Batch #</th>
                <th>Supplier</th>
                <th>Quantity</th>
                <th>Received</th>
                <th>Expiry</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {visibleRows.map((row) => (
                <tr key={row.id}>
                  <td><strong>{row.medicineName}</strong></td>
                  <td>{row.batchNumber}</td>
                  <td>{row.supplierName}</td>
                  <td>{row.quantity}</td>
                  <td>{row.receivedDate ? formatDate(row.receivedDate.slice(0, 10)) : '—'}</td>
                  <td>{formatDate(row.expiryDate.slice(0, 10))}</td>
                  <td><Badge tone={batchTone(row)}>{batchLabel(row)}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default StockBatchesPage;
