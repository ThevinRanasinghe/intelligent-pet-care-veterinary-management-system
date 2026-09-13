import { useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle,
  Boxes,
  Calendar,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  Clock,
  FileText,
  History,
  Layers,
  Loader2,
  PackagePlus,
  Pill,
  Plus,
  RefreshCw,
  Search,
  Truck,
  UserCheck,
} from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Modal } from '../../components/ui/Modal';
import { useAuth } from '../auth/AuthContext';
import {
  cancelReservation,
  createMedicine,
  createSupplier,
  dispenseReservation,
  getBatches,
  getExpiring,
  getLowStock,
  getMedicines,
  getSuppliers,
  getTransactions,
  receiveStock,
  reserveMedicine,
  type CreateMedicineRequest,
  type CreateSupplierRequest,
  type ReceiveStockRequest,
  type ReserveMedicineRequest,
} from '../../services/inventoryService';
import type {
  InventoryTransaction,
  Medicine,
  MedicineBatch,
  MedicineReservation,
  Supplier,
} from '../../types/domain';
import { messageFrom } from '../../utils/errors';
import { formatDate, formatLkr } from '../../utils/format';

type InventoryTab = 'catalog' | 'lowStock' | 'expiring' | 'suppliers';

export function InventoryPage() {
  const { hasRole, user } = useAuth();

  const isOfficerOrManager =
    hasRole('InventoryOfficer') || hasRole('ClinicManager') || hasRole('Administrator');
  const isVet = hasRole('Veterinarian') || isOfficerOrManager;

  const [activeTab, setActiveTab] = useState<InventoryTab>('catalog');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  // Medicines Catalog state
  const [medicines, setMedicines] = useState<Medicine[]>([]);
  const [totalMedicines, setTotalMedicines] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('');

  // Low Stock & Expiring state
  const [lowStockList, setLowStockList] = useState<Medicine[]>([]);
  const [expiringBatches, setExpiringBatches] = useState<MedicineBatch[]>([]);

  // Suppliers state
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);

  // Modals state
  const [isAddMedicineOpen, setIsAddMedicineOpen] = useState(false);
  const [isStockInOpen, setIsStockInOpen] = useState(false);
  const [isBatchesOpen, setIsBatchesOpen] = useState(false);
  const [isReserveOpen, setIsReserveOpen] = useState(false);
  const [isTransactionsOpen, setIsTransactionsOpen] = useState(false);
  const [isAddSupplierOpen, setIsAddSupplierOpen] = useState(false);

  // Selected item for modals
  const [selectedMedicine, setSelectedMedicine] = useState<Medicine | null>(null);
  const [currentBatches, setCurrentBatches] = useState<MedicineBatch[]>([]);
  const [currentTransactions, setCurrentTransactions] = useState<InventoryTransaction[]>([]);
  const [modalLoading, setModalLoading] = useState(false);

  // Form states
  const [newMedicine, setNewMedicine] = useState<CreateMedicineRequest>({
    name: '',
    category: 'Antibiotics',
    description: '',
    dosageForm: 'Tablet',
    strength: '250mg',
    unitPrice: 100,
    manufacturer: '',
    reorderLevel: 10,
  });

  const [stockInData, setStockInData] = useState<ReceiveStockRequest>({
    supplierId: '',
    batchNumber: '',
    quantity: 10,
    expiryDate: new Date(Date.now() + 180 * 86400000).toISOString().slice(0, 10),
  });

  const [reserveData, setReserveData] = useState<ReserveMedicineRequest>({
    medicineId: '',
    quantity: 1,
    referenceType: 'Consultation',
    referenceId: undefined,
  });

  const [lastCreatedReservation, setLastCreatedReservation] =
    useState<MedicineReservation | null>(null);

  const [newSupplier, setNewSupplier] = useState<CreateSupplierRequest>({
    name: '',
    contactPerson: '',
    phone: '',
    email: '',
    address: '',
  });

  // Auto-dismiss success notification
  useEffect(() => {
    if (!success) return;
    const timer = setTimeout(() => setSuccess(''), 4000);
    return () => clearTimeout(timer);
  }, [success]);

  // Load primary data
  const loadData = async () => {
    setLoading(true);
    setError('');
    try {
      const [pagedRes, lowStockRes, expiringRes, supplierRes] = await Promise.all([
        getMedicines({
          search: searchQuery || undefined,
          category: selectedCategory || undefined,
          page,
          pageSize,
        }),
        getLowStock(),
        getExpiring(30),
        getSuppliers(),
      ]);

      setMedicines(pagedRes.items);
      setTotalMedicines(pagedRes.total);
      setLowStockList(lowStockRes);
      setExpiringBatches(expiringRes);
      setSuppliers(supplierRes);

      // Default supplier for stock-in modal if empty
      if (supplierRes.length > 0 && !stockInData.supplierId) {
        setStockInData((prev) => ({ ...prev, supplierId: supplierRes[0].id }));
      }
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, searchQuery, selectedCategory]);

  // Handlers for Add Medicine
  const handleCreateMedicine = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newMedicine.name.trim()) {
      setError('Medicine name is required.');
      return;
    }
    setModalLoading(true);
    setError('');
    try {
      await createMedicine(newMedicine);
      setSuccess(`Medicine "${newMedicine.name}" created successfully.`);
      setIsAddMedicineOpen(false);
      setNewMedicine({
        name: '',
        category: 'Antibiotics',
        description: '',
        dosageForm: 'Tablet',
        strength: '250mg',
        unitPrice: 100,
        manufacturer: '',
        reorderLevel: 10,
      });
      await loadData();
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setModalLoading(false);
    }
  };

  // Handlers for Receive Stock
  const handleOpenStockIn = (med: Medicine) => {
    setSelectedMedicine(med);
    setStockInData({
      supplierId: suppliers[0]?.id || '',
      batchNumber: `BAT-${Date.now().toString().slice(-5)}`,
      quantity: 50,
      expiryDate: new Date(Date.now() + 180 * 86400000).toISOString().slice(0, 10),
    });
    setIsStockInOpen(true);
  };

  const handleReceiveStock = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedMedicine) return;
    if (!stockInData.supplierId) {
      setError('Please select a supplier.');
      return;
    }
    setModalLoading(true);
    setError('');
    try {
      await receiveStock(selectedMedicine.id, stockInData);
      setSuccess(`Received ${stockInData.quantity} units for "${selectedMedicine.name}".`);
      setIsStockInOpen(false);
      await loadData();
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setModalLoading(false);
    }
  };

  // Handlers for View Batches
  const handleOpenBatches = async (med: Medicine) => {
    setSelectedMedicine(med);
    setIsBatchesOpen(true);
    setModalLoading(true);
    try {
      const batches = await getBatches(med.id);
      setCurrentBatches(batches);
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setModalLoading(false);
    }
  };

  // Handlers for View Transactions
  const handleOpenTransactions = async (med: Medicine) => {
    setSelectedMedicine(med);
    setIsTransactionsOpen(true);
    setModalLoading(true);
    try {
      const txs = await getTransactions(med.id);
      setCurrentTransactions(txs);
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setModalLoading(false);
    }
  };

  // Handlers for Reserve Medicine
  const handleOpenReserve = (med: Medicine) => {
    setSelectedMedicine(med);
    setReserveData({
      medicineId: med.id,
      quantity: 1,
      referenceType: 'Treatment',
      referenceId: undefined,
    });
    setLastCreatedReservation(null);
    setIsReserveOpen(true);
  };

  const handleCreateReservation = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedMedicine) return;
    setModalLoading(true);
    setError('');
    try {
      const res = await reserveMedicine(reserveData);
      setLastCreatedReservation(res);
      setSuccess(`Reserved ${res.quantity} unit(s) of "${selectedMedicine.name}".`);
      await loadData();
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setModalLoading(false);
    }
  };

  const handleDispenseReservation = async (reservationId: string) => {
    setModalLoading(true);
    setError('');
    try {
      await dispenseReservation(reservationId);
      setSuccess('Reservation successfully dispensed via FEFO batch allocation.');
      setIsReserveOpen(false);
      setLastCreatedReservation(null);
      await loadData();
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setModalLoading(false);
    }
  };

  const handleCancelReservation = async (reservationId: string) => {
    setModalLoading(true);
    setError('');
    try {
      await cancelReservation(reservationId);
      setSuccess('Reservation cancelled and stock restored to available pool.');
      setIsReserveOpen(false);
      setLastCreatedReservation(null);
      await loadData();
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setModalLoading(false);
    }
  };

  // Handlers for Add Supplier
  const handleCreateSupplier = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newSupplier.name.trim()) {
      setError('Supplier name is required.');
      return;
    }
    setModalLoading(true);
    setError('');
    try {
      await createSupplier(newSupplier);
      setSuccess(`Supplier "${newSupplier.name}" added successfully.`);
      setIsAddSupplierOpen(false);
      setNewSupplier({
        name: '',
        contactPerson: '',
        phone: '',
        email: '',
        address: '',
      });
      await loadData();
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setModalLoading(false);
    }
  };

  // Category list derived from items
  const categories = useMemo(() => {
    const defaultCats = [
      'All Categories',
      'Antibiotics',
      'Anti-inflammatory',
      'Vaccines',
      'Analgesics',
      'Dermatology',
      'Supplements',
      'Parasiticides',
    ];
    return defaultCats;
  }, []);

  return (
    <div className="page-wrap">
      {/* Top Banner / Heading */}
      <div className="page-heading">
        <div>
          <div className="eyebrow">Inventory Management</div>
          <h2>Medicine & Stock Control</h2>
          <p>
            Track pharmaceutical inventory, multi-batch FEFO dispensing, atomic reserve workflows,
            and supplier consignments with real-time audit logging.
          </p>
        </div>
        <div className="heading-actions">
          <Button variant="secondary" onClick={loadData} icon={<RefreshCw size={14} />}>
            Refresh
          </Button>
          {isOfficerOrManager && (
            <>
              <Button
                variant="secondary"
                onClick={() => setIsAddSupplierOpen(true)}
                icon={<Truck size={14} />}
              >
                Add Supplier
              </Button>
              <Button
                variant="primary"
                onClick={() => setIsAddMedicineOpen(true)}
                icon={<Plus size={14} />}
              >
                Add Medicine
              </Button>
            </>
          )}
        </div>
      </div>

      {/* Alerts */}
      {error && (
        <div className="form-error" style={{ marginBottom: '16px' }}>
          <strong>Error:</strong> {error}
        </div>
      )}
      {success && (
        <div
          className="info-strip"
          style={{
            marginBottom: '16px',
            borderColor: 'var(--success)',
            background: 'var(--success-soft)',
            color: 'var(--success)',
          }}
        >
          <strong>Success:</strong> <span>{success}</span>
        </div>
      )}

      {/* Summary KPI Cards */}
      <div className="stat-grid">
        <Card className="stat-card">
          <div className="stat-top">
            <span>Total SKUs</span>
            <div className="stat-icon">
              <Boxes size={18} />
            </div>
          </div>
          <strong>{totalMedicines}</strong>
          <p>Active pharmaceutical lines in catalog</p>
        </Card>

        <Card className="stat-card">
          <div className="stat-top">
            <span>Low Stock Alerts</span>
            <div
              className="stat-icon"
              style={{
                background: lowStockList.length > 0 ? 'var(--warning-soft)' : '#eff8f5',
                color: lowStockList.length > 0 ? 'var(--warning)' : 'var(--primary)',
              }}
            >
              <AlertTriangle size={18} />
            </div>
          </div>
          <strong style={{ color: lowStockList.length > 0 ? 'var(--warning)' : 'inherit' }}>
            {lowStockList.length}
          </strong>
          <p>Medicines at or below reorder threshold</p>
        </Card>

        <Card className="stat-card">
          <div className="stat-top">
            <span>Expiring Batches</span>
            <div
              className="stat-icon"
              style={{
                background: expiringBatches.length > 0 ? 'var(--danger-soft)' : '#eff8f5',
                color: expiringBatches.length > 0 ? 'var(--danger)' : 'var(--primary)',
              }}
            >
              <Clock size={18} />
            </div>
          </div>
          <strong style={{ color: expiringBatches.length > 0 ? 'var(--danger)' : 'inherit' }}>
            {expiringBatches.length}
          </strong>
          <p>Batches expiring within the next 30 days</p>
        </Card>

        <Card className="stat-card">
          <div className="stat-top">
            <span>Suppliers Active</span>
            <div className="stat-icon">
              <Truck size={18} />
            </div>
          </div>
          <strong>{suppliers.filter((s) => s.status === 'Active').length}</strong>
          <p>Verified pharmaceutical supply partners</p>
        </Card>
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', gap: '8px', marginBottom: '16px', borderBottom: '1px solid var(--line)', paddingBottom: '8px' }}>
        <button
          className={`btn ${activeTab === 'catalog' ? 'btn-primary' : 'btn-ghost'}`}
          onClick={() => setActiveTab('catalog')}
        >
          <Pill size={14} style={{ marginRight: '6px' }} /> Medicine Catalog
        </button>
        <button
          className={`btn ${activeTab === 'lowStock' ? 'btn-primary' : 'btn-ghost'}`}
          onClick={() => setActiveTab('lowStock')}
        >
          <AlertTriangle size={14} style={{ marginRight: '6px' }} /> Low Stock ({lowStockList.length})
        </button>
        <button
          className={`btn ${activeTab === 'expiring' ? 'btn-primary' : 'btn-ghost'}`}
          onClick={() => setActiveTab('expiring')}
        >
          <Clock size={14} style={{ marginRight: '6px' }} /> Expiring Batches ({expiringBatches.length})
        </button>
        <button
          className={`btn ${activeTab === 'suppliers' ? 'btn-primary' : 'btn-ghost'}`}
          onClick={() => setActiveTab('suppliers')}
        >
          <Truck size={14} style={{ marginRight: '6px' }} /> Suppliers ({suppliers.length})
        </button>
      </div>

      {/* TAB 1: MEDICINE CATALOG */}
      {activeTab === 'catalog' && (
        <Card>
          <div className="card-header">
            <div>
              <div className="eyebrow">Inventory Catalog</div>
              <h3>Pharmaceutical Products</h3>
            </div>
            <div className="filter-bar" style={{ margin: 0 }}>
              <div className="search-input">
                <Search size={16} />
                <input
                  type="text"
                  placeholder="Search medicine name, manufacturer..."
                  value={searchQuery}
                  onChange={(e) => {
                    setSearchQuery(e.target.value);
                    setPage(1);
                  }}
                />
              </div>
              <div className="select-input">
                <select
                  value={selectedCategory}
                  onChange={(e) => {
                    setSelectedCategory(e.target.value === 'All Categories' ? '' : e.target.value);
                    setPage(1);
                  }}
                >
                  {categories.map((c) => (
                    <option key={c} value={c === 'All Categories' ? '' : c}>
                      {c}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </div>

          {loading ? (
            <div style={{ display: 'grid', placeItems: 'center', minHeight: '200px' }}>
              <Loader2 className="spin" size={28} style={{ color: 'var(--primary)' }} />
            </div>
          ) : medicines.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '40px', color: 'var(--muted)' }}>
              <Pill size={40} style={{ opacity: 0.3, marginBottom: '12px' }} />
              <p>No medicines found matching the current search criteria.</p>
              {isOfficerOrManager && (
                <Button
                  variant="secondary"
                  onClick={() => setIsAddMedicineOpen(true)}
                  style={{ marginTop: '10px' }}
                >
                  Add First Medicine
                </Button>
              )}
            </div>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Medicine Name</th>
                    <th>Category</th>
                    <th>Dosage & Strength</th>
                    <th>Unit Price</th>
                    <th>Stock Status</th>
                    <th>Available / Total</th>
                    <th style={{ textAlign: 'right' }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {medicines.map((med) => {
                    const isLow = med.availableQuantity <= med.reorderLevel;
                    return (
                      <tr key={med.id}>
                        <td>
                          <strong>{med.name}</strong>
                          <span className="muted">{med.manufacturer}</span>
                        </td>
                        <td>
                          <Badge tone="neutral">{med.category}</Badge>
                        </td>
                        <td>
                          {med.dosageForm} • {med.strength}
                        </td>
                        <td>
                          <strong>{formatLkr(med.unitPrice)}</strong>
                        </td>
                        <td>
                          {med.availableQuantity === 0 ? (
                            <Badge tone="danger">Out of Stock</Badge>
                          ) : isLow ? (
                            <Badge tone="warning">Low Stock ({med.reorderLevel} min)</Badge>
                          ) : (
                            <Badge tone="success">In Stock</Badge>
                          )}
                        </td>
                        <td>
                          <strong>
                            {med.availableQuantity} / {med.totalQuantity}
                          </strong>
                          {med.reservedQuantity > 0 && (
                            <span className="muted" style={{ color: 'var(--info)' }}>
                              ({med.reservedQuantity} reserved)
                            </span>
                          )}
                        </td>
                        <td>
                          <div
                            style={{
                              display: 'flex',
                              gap: '6px',
                              justifyContent: 'flex-end',
                            }}
                          >
                            {isOfficerOrManager && (
                              <Button
                                variant="secondary"
                                onClick={() => handleOpenStockIn(med)}
                                title="Receive Stock / Stock-In"
                              >
                                <PackagePlus size={13} style={{ marginRight: '4px' }} /> Stock-In
                              </Button>
                            )}
                            {isVet && (
                              <Button
                                variant="secondary"
                                onClick={() => handleOpenReserve(med)}
                                disabled={med.availableQuantity === 0}
                                title="Reserve for Treatment/Consultation"
                              >
                                <ClipboardList size={13} style={{ marginRight: '4px' }} /> Reserve
                              </Button>
                            )}
                            <Button
                              variant="ghost"
                              onClick={() => handleOpenBatches(med)}
                              title="View Batches (FEFO)"
                            >
                              <Layers size={14} />
                            </Button>
                            <Button
                              variant="ghost"
                              onClick={() => handleOpenTransactions(med)}
                              title="Audit History"
                            >
                              <History size={14} />
                            </Button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>

              {/* Pagination */}
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  padding: '16px 4px 4px',
                }}
              >
                <span className="muted" style={{ fontSize: '12px' }}>
                  Showing {(page - 1) * pageSize + 1} -{' '}
                  {Math.min(page * pageSize, totalMedicines)} of {totalMedicines} items
                </span>
                <div style={{ display: 'flex', gap: '6px' }}>
                  <Button
                    variant="secondary"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    icon={<ChevronLeft size={14} />}
                  >
                    Previous
                  </Button>
                  <Button
                    variant="secondary"
                    disabled={page * pageSize >= totalMedicines}
                    onClick={() => setPage((p) => p + 1)}
                    icon={<ChevronRight size={14} />}
                  >
                    Next
                  </Button>
                </div>
              </div>
            </div>
          )}
        </Card>
      )}

      {/* TAB 2: LOW STOCK ALERTS */}
      {activeTab === 'lowStock' && (
        <Card>
          <div className="card-header">
            <div>
              <div className="eyebrow">Critical Inventory</div>
              <h3>Low Stock Reorder Alerts</h3>
            </div>
            <span className="muted">Threshold triggered when Available ≤ Reorder Level</span>
          </div>

          {lowStockList.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '40px', color: 'var(--muted)' }}>
              <p>All medicines currently have adequate stock levels. No reorders necessary.</p>
            </div>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Medicine</th>
                    <th>Category</th>
                    <th>Available</th>
                    <th>Reorder Level</th>
                    <th>Recommended Order</th>
                    <th style={{ textAlign: 'right' }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {lowStockList.map((med) => {
                    const deficit = Math.max(0, med.reorderLevel * 2 - med.availableQuantity);
                    return (
                      <tr key={med.id}>
                        <td>
                          <strong>{med.name}</strong>
                          <span className="muted">{med.dosageForm} • {med.strength}</span>
                        </td>
                        <td>
                          <Badge tone="warning">{med.category}</Badge>
                        </td>
                        <td>
                          <strong style={{ color: 'var(--danger)', fontSize: '13px' }}>
                            {med.availableQuantity}
                          </strong>
                        </td>
                        <td>{med.reorderLevel} units</td>
                        <td>
                          <span style={{ color: 'var(--primary)', fontWeight: 700 }}>
                            +{deficit} units
                          </span>
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          {isOfficerOrManager && (
                            <Button
                              variant="primary"
                              onClick={() => handleOpenStockIn(med)}
                            >
                              <PackagePlus size={13} style={{ marginRight: '4px' }} /> Quick Restock
                            </Button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      )}

      {/* TAB 3: EXPIRING BATCHES */}
      {activeTab === 'expiring' && (
        <Card>
          <div className="card-header">
            <div>
              <div className="eyebrow">FEFO Stock Expiry Monitoring</div>
              <h3>Batches Expiring in Next 30 Days</h3>
            </div>
            <span className="muted">Tracked for First-Expiry-First-Out dispensing safety</span>
          </div>

          {expiringBatches.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '40px', color: 'var(--muted)' }}>
              <p>No batches are expiring within the next 30 days. Stock shelf-life is optimal.</p>
            </div>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Batch Number</th>
                    <th>Medicine ID</th>
                    <th>Current Quantity</th>
                    <th>Expiry Date</th>
                    <th>Urgency</th>
                  </tr>
                </thead>
                <tbody>
                  {expiringBatches.map((batch) => {
                    const exp = new Date(batch.expiryDate);
                    const now = new Date();
                    const diffDays = Math.ceil((exp.getTime() - now.getTime()) / (1000 * 3600 * 24));
                    return (
                      <tr key={batch.id}>
                        <td>
                          <strong>{batch.batchNumber}</strong>
                        </td>
                        <td>
                          <span className="muted">{batch.medicineId}</span>
                        </td>
                        <td>
                          <strong>{batch.quantity} units</strong>
                        </td>
                        <td>{batch.expiryDate}</td>
                        <td>
                          {diffDays <= 0 ? (
                            <Badge tone="danger">Expired ({Math.abs(diffDays)}d ago)</Badge>
                          ) : diffDays <= 14 ? (
                            <Badge tone="danger">Expiring in {diffDays} days</Badge>
                          ) : (
                            <Badge tone="warning">Expiring in {diffDays} days</Badge>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      )}

      {/* TAB 4: SUPPLIERS */}
      {activeTab === 'suppliers' && (
        <Card>
          <div className="card-header">
            <div>
              <div className="eyebrow">Procurement Directory</div>
              <h3>Pharmaceutical & Medical Suppliers</h3>
            </div>
            {isOfficerOrManager && (
              <Button
                variant="primary"
                onClick={() => setIsAddSupplierOpen(true)}
                icon={<Plus size={14} />}
              >
                Add Supplier
              </Button>
            )}
          </div>

          {suppliers.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '40px', color: 'var(--muted)' }}>
              <p>No suppliers registered yet.</p>
            </div>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Supplier Name</th>
                    <th>Contact Person</th>
                    <th>Phone</th>
                    <th>Email</th>
                    <th>Address</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {suppliers.map((s) => (
                    <tr key={s.id}>
                      <td>
                        <strong>{s.name}</strong>
                      </td>
                      <td>{s.contactPerson}</td>
                      <td>{s.phone}</td>
                      <td>{s.email}</td>
                      <td>{s.address}</td>
                      <td>
                        <Badge tone={s.status === 'Active' ? 'success' : 'neutral'}>
                          {s.status}
                        </Badge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      )}

      {/* MODAL: ADD MEDICINE */}
      {isAddMedicineOpen && (
        <Modal title="Add New Medicine" onClose={() => setIsAddMedicineOpen(false)}>
          <form onSubmit={handleCreateMedicine}>
            <div className="form-grid">
              <label>
                Medicine Name *
                <input
                  type="text"
                  required
                  value={newMedicine.name}
                  onChange={(e) => setNewMedicine({ ...newMedicine, name: e.target.value })}
                  placeholder="e.g. Amoxicillin Clavulanate"
                />
              </label>
              <label>
                Category *
                <select
                  value={newMedicine.category}
                  onChange={(e) => setNewMedicine({ ...newMedicine, category: e.target.value })}
                >
                  <option value="Antibiotics">Antibiotics</option>
                  <option value="Anti-inflammatory">Anti-inflammatory</option>
                  <option value="Vaccines">Vaccines</option>
                  <option value="Analgesics">Analgesics</option>
                  <option value="Dermatology">Dermatology</option>
                  <option value="Supplements">Supplements</option>
                  <option value="Parasiticides">Parasiticides</option>
                </select>
              </label>
              <label>
                Dosage Form *
                <input
                  type="text"
                  required
                  value={newMedicine.dosageForm}
                  onChange={(e) => setNewMedicine({ ...newMedicine, dosageForm: e.target.value })}
                  placeholder="e.g. Tablet, Syrup, Injection"
                />
              </label>
              <label>
                Strength *
                <input
                  type="text"
                  required
                  value={newMedicine.strength}
                  onChange={(e) => setNewMedicine({ ...newMedicine, strength: e.target.value })}
                  placeholder="e.g. 500mg, 10mg/ml"
                />
              </label>
              <label>
                Unit Price (LKR) *
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  required
                  value={newMedicine.unitPrice}
                  onChange={(e) =>
                    setNewMedicine({ ...newMedicine, unitPrice: parseFloat(e.target.value) || 0 })
                  }
                />
              </label>
              <label>
                Reorder Level *
                <input
                  type="number"
                  min="0"
                  required
                  value={newMedicine.reorderLevel}
                  onChange={(e) =>
                    setNewMedicine({ ...newMedicine, reorderLevel: parseInt(e.target.value) || 0 })
                  }
                />
              </label>
              <label style={{ gridColumn: 'span 2' }}>
                Manufacturer *
                <input
                  type="text"
                  required
                  value={newMedicine.manufacturer}
                  onChange={(e) => setNewMedicine({ ...newMedicine, manufacturer: e.target.value })}
                  placeholder="e.g. Zoetis, Boehringer Ingelheim"
                />
              </label>
              <label style={{ gridColumn: 'span 2' }}>
                Description
                <textarea
                  rows={2}
                  value={newMedicine.description}
                  onChange={(e) => setNewMedicine({ ...newMedicine, description: e.target.value })}
                  placeholder="Clinical usage, indications, contraindications..."
                />
              </label>
            </div>

            <div className="modal-actions">
              <Button variant="secondary" onClick={() => setIsAddMedicineOpen(false)} type="button">
                Cancel
              </Button>
              <Button variant="primary" type="submit" disabled={modalLoading}>
                {modalLoading ? <Loader2 size={14} className="spin" /> : 'Save Medicine'}
              </Button>
            </div>
          </form>
        </Modal>
      )}

      {/* MODAL: STOCK-IN / RECEIVE STOCK */}
      {isStockInOpen && selectedMedicine && (
        <Modal
          title={`Receive Stock: ${selectedMedicine.name}`}
          onClose={() => setIsStockInOpen(false)}
        >
          <form onSubmit={handleReceiveStock}>
            <p className="muted" style={{ marginBottom: '14px' }}>
              Record an incoming consignment batch from a certified supplier. Initialises stock with
              FEFO tracking.
            </p>

            <div className="form-grid">
              <label style={{ gridColumn: 'span 2' }}>
                Supplier *
                <select
                  required
                  value={stockInData.supplierId}
                  onChange={(e) => setStockInData({ ...stockInData, supplierId: e.target.value })}
                >
                  {suppliers.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name} ({s.contactPerson})
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Batch / Lot Number *
                <input
                  type="text"
                  required
                  value={stockInData.batchNumber}
                  onChange={(e) => setStockInData({ ...stockInData, batchNumber: e.target.value })}
                  placeholder="e.g. LOT-2026-A1"
                />
              </label>
              <label>
                Quantity (Units) *
                <input
                  type="number"
                  min="1"
                  required
                  value={stockInData.quantity}
                  onChange={(e) =>
                    setStockInData({ ...stockInData, quantity: parseInt(e.target.value) || 1 })
                  }
                />
              </label>
              <label style={{ gridColumn: 'span 2' }}>
                Expiry Date *
                <input
                  type="date"
                  required
                  value={stockInData.expiryDate}
                  onChange={(e) => setStockInData({ ...stockInData, expiryDate: e.target.value })}
                />
              </label>
            </div>

            <div className="modal-actions">
              <Button variant="secondary" onClick={() => setIsStockInOpen(false)} type="button">
                Cancel
              </Button>
              <Button variant="primary" type="submit" disabled={modalLoading}>
                {modalLoading ? <Loader2 size={14} className="spin" /> : 'Confirm Stock-In'}
              </Button>
            </div>
          </form>
        </Modal>
      )}

      {/* MODAL: BATCHES VIEW */}
      {isBatchesOpen && selectedMedicine && (
        <Modal
          title={`Batches: ${selectedMedicine.name}`}
          onClose={() => setIsBatchesOpen(false)}
        >
          <div style={{ marginBottom: '16px' }}>
            <p className="muted">
              Stock batches sorted by First-Expiry-First-Out (FEFO) dispensing order.
            </p>
          </div>

          {modalLoading ? (
            <div style={{ display: 'grid', placeItems: 'center', height: '120px' }}>
              <Loader2 className="spin" size={24} style={{ color: 'var(--primary)' }} />
            </div>
          ) : currentBatches.length === 0 ? (
            <p className="muted">No batches recorded for this medicine yet.</p>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Batch Number</th>
                    <th>Remaining Qty</th>
                    <th>Expiry Date</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {currentBatches.map((b) => (
                    <tr key={b.id}>
                      <td>
                        <strong>{b.batchNumber}</strong>
                      </td>
                      <td>{b.quantity} units</td>
                      <td>{b.expiryDate}</td>
                      <td>
                        {b.quantity === 0 ? (
                          <Badge tone="neutral">Depleted</Badge>
                        ) : b.isExpired ? (
                          <Badge tone="danger">Expired</Badge>
                        ) : (
                          <Badge tone="success">Active</Badge>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div className="modal-actions">
            <Button variant="secondary" onClick={() => setIsBatchesOpen(false)}>
              Close
            </Button>
          </div>
        </Modal>
      )}

      {/* MODAL: RESERVE MEDICINE & DISPENSE WORKFLOW */}
      {isReserveOpen && selectedMedicine && (
        <Modal
          title={`Reserve / Dispense: ${selectedMedicine.name}`}
          onClose={() => setIsReserveOpen(false)}
        >
          {!lastCreatedReservation ? (
            <form onSubmit={handleCreateReservation}>
              <p className="muted" style={{ marginBottom: '14px' }}>
                Atomic stock hold for clinical treatment. Available units:{' '}
                <strong style={{ color: 'var(--primary)' }}>
                  {selectedMedicine.availableQuantity}
                </strong>
                .
              </p>

              <div className="form-grid">
                <label>
                  Quantity to Reserve *
                  <input
                    type="number"
                    min="1"
                    max={selectedMedicine.availableQuantity}
                    required
                    value={reserveData.quantity}
                    onChange={(e) =>
                      setReserveData({
                        ...reserveData,
                        quantity: parseInt(e.target.value) || 1,
                      })
                    }
                  />
                </label>
                <label>
                  Reference Type
                  <select
                    value={reserveData.referenceType}
                    onChange={(e) =>
                      setReserveData({ ...reserveData, referenceType: e.target.value })
                    }
                  >
                    <option value="Treatment">Treatment</option>
                    <option value="Consultation">Consultation</option>
                    <option value="Surgery">Surgery</option>
                    <option value="Emergency">Emergency</option>
                  </select>
                </label>
              </div>

              <div className="modal-actions">
                <Button variant="secondary" onClick={() => setIsReserveOpen(false)} type="button">
                  Cancel
                </Button>
                <Button variant="primary" type="submit" disabled={modalLoading}>
                  {modalLoading ? <Loader2 size={14} className="spin" /> : 'Create Reservation'}
                </Button>
              </div>
            </form>
          ) : (
            <div>
              <div
                className="info-strip"
                style={{
                  marginBottom: '16px',
                  background: 'var(--info-soft)',
                  borderColor: 'var(--info)',
                }}
              >
                <div>
                  <strong>Reservation Created (#{lastCreatedReservation.id.slice(0, 8)})</strong>
                  <span>
                    Status: <strong>{lastCreatedReservation.status}</strong> | Quantity:{' '}
                    <strong>{lastCreatedReservation.quantity}</strong>
                  </span>
                </div>
              </div>

              <p className="muted" style={{ fontSize: '12px' }}>
                The units are now atomically locked in the reserved pool. You can dispense them
                immediately (FEFO deduction from batches) or cancel the hold.
              </p>

              <div className="modal-actions" style={{ marginTop: '20px' }}>
                <Button
                  variant="danger"
                  onClick={() => handleCancelReservation(lastCreatedReservation.id)}
                  disabled={modalLoading}
                >
                  Cancel Reservation
                </Button>
                {isOfficerOrManager && (
                  <Button
                    variant="primary"
                    onClick={() => handleDispenseReservation(lastCreatedReservation.id)}
                    disabled={modalLoading}
                  >
                    {modalLoading ? <Loader2 size={14} className="spin" /> : 'Dispense Stock Now'}
                  </Button>
                )}
              </div>
            </div>
          )}
        </Modal>
      )}

      {/* MODAL: TRANSACTIONS AUDIT LEDGER */}
      {isTransactionsOpen && selectedMedicine && (
        <Modal
          title={`Audit Ledger: ${selectedMedicine.name}`}
          onClose={() => setIsTransactionsOpen(false)}
        >
          <div style={{ marginBottom: '14px' }}>
            <p className="muted">
              Complete transaction history showing physical stock movements and reservation holds.
            </p>
          </div>

          {modalLoading ? (
            <div style={{ display: 'grid', placeItems: 'center', height: '120px' }}>
              <Loader2 className="spin" size={24} style={{ color: 'var(--primary)' }} />
            </div>
          ) : currentTransactions.length === 0 ? (
            <p className="muted">No transactions recorded for this medicine yet.</p>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Timestamp</th>
                    <th>Type</th>
                    <th>Qty Change</th>
                    <th>User ID</th>
                  </tr>
                </thead>
                <tbody>
                  {currentTransactions.map((tx) => (
                    <tr key={tx.id}>
                      <td>{formatDate(tx.occurredAt)}</td>
                      <td>
                        <Badge
                          tone={
                            tx.type === 'StockIn'
                              ? 'success'
                              : tx.type === 'Dispense'
                              ? 'info'
                              : tx.type === 'Reservation'
                              ? 'warning'
                              : 'neutral'
                          }
                        >
                          {tx.type}
                        </Badge>
                      </td>
                      <td>
                        <strong
                          style={{
                            color: tx.quantityChange > 0 ? 'var(--success)' : 'var(--danger)',
                          }}
                        >
                          {tx.quantityChange > 0 ? `+${tx.quantityChange}` : tx.quantityChange}
                        </strong>
                      </td>
                      <td>
                        <span className="muted" style={{ fontSize: '10px' }}>
                          {tx.performedByUserId}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div className="modal-actions">
            <Button variant="secondary" onClick={() => setIsTransactionsOpen(false)}>
              Close
            </Button>
          </div>
        </Modal>
      )}

      {/* MODAL: ADD SUPPLIER */}
      {isAddSupplierOpen && (
        <Modal title="Register New Supplier" onClose={() => setIsAddSupplierOpen(false)}>
          <form onSubmit={handleCreateSupplier}>
            <div className="form-grid">
              <label style={{ gridColumn: 'span 2' }}>
                Supplier Name *
                <input
                  type="text"
                  required
                  value={newSupplier.name}
                  onChange={(e) => setNewSupplier({ ...newSupplier, name: e.target.value })}
                  placeholder="e.g. MediPharma Sri Lanka"
                />
              </label>
              <label>
                Contact Person *
                <input
                  type="text"
                  required
                  value={newSupplier.contactPerson}
                  onChange={(e) =>
                    setNewSupplier({ ...newSupplier, contactPerson: e.target.value })
                  }
                  placeholder="e.g. Dr. K. Silva"
                />
              </label>
              <label>
                Phone Number *
                <input
                  type="text"
                  required
                  value={newSupplier.phone}
                  onChange={(e) => setNewSupplier({ ...newSupplier, phone: e.target.value })}
                  placeholder="e.g. +94 11 234 5678"
                />
              </label>
              <label style={{ gridColumn: 'span 2' }}>
                Email Address *
                <input
                  type="email"
                  required
                  value={newSupplier.email}
                  onChange={(e) => setNewSupplier({ ...newSupplier, email: e.target.value })}
                  placeholder="e.g. orders@medipharma.lk"
                />
              </label>
              <label style={{ gridColumn: 'span 2' }}>
                Physical Address *
                <input
                  type="text"
                  required
                  value={newSupplier.address}
                  onChange={(e) => setNewSupplier({ ...newSupplier, address: e.target.value })}
                  placeholder="e.g. 142 Galle Road, Colombo 03"
                />
              </label>
            </div>

            <div className="modal-actions">
              <Button variant="secondary" onClick={() => setIsAddSupplierOpen(false)} type="button">
                Cancel
              </Button>
              <Button variant="primary" type="submit" disabled={modalLoading}>
                {modalLoading ? <Loader2 size={14} className="spin" /> : 'Register Supplier'}
              </Button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
