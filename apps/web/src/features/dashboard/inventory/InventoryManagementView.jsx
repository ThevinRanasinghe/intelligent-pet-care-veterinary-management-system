import { useState, useEffect, useCallback } from 'react';
import {
  Package,
  AlertTriangle,
  Clock,
  Truck,
  Search,
  RefreshCw,
  Plus,
  Layers,
  History,
  ClipboardList,
  PackagePlus,
  ChevronLeft,
  ChevronRight,
  X,
  Loader2,
  CheckCircle,
  AlertCircle,
  Pill,
} from 'lucide-react';
import {
  getMedicines,
  createMedicine,
  receiveStock,
  getBatches,
  getTransactions,
  getLowStock,
  getExpiring,
  getExpiredStock,
  reserveMedicine,
  cancelReservation,
  dispenseReservation,
  getSuppliers,
  createSupplier,
} from '../services/inventoryApi';
import useAuthStore from '../../../store/authStore';

export default function InventoryManagementView({ defaultTab = 'catalog' }) {
  const { user } = useAuthStore();

  const isOfficerOrManager =
    user?.role === 'InventoryOfficer' ||
    user?.role === 'ClinicManager' ||
    user?.role === 'SuperAdmin';
  const isVet = user?.role === 'Veterinarian' || isOfficerOrManager;

  const [activeTab, setActiveTab] = useState(defaultTab);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  // Catalog state
  const [medicines, setMedicines] = useState([]);
  const [totalMedicines, setTotalMedicines] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('');

  // Auxiliary data
  const [lowStockList, setLowStockList] = useState([]);
  const [expiringBatches, setExpiringBatches] = useState([]);
  const [expiredBatches, setExpiredBatches] = useState([]);
  const [suppliers, setSuppliers] = useState([]);

  // Modals state
  const [isAddMedOpen, setIsAddMedOpen] = useState(false);
  const [isStockInOpen, setIsStockInOpen] = useState(false);
  const [isBatchesOpen, setIsBatchesOpen] = useState(false);
  const [isReserveOpen, setIsReserveOpen] = useState(false);
  const [isTxOpen, setIsTxOpen] = useState(false);
  const [isAddSupOpen, setIsAddSupOpen] = useState(false);

  // Selected item for modal
  const [selectedMedicine, setSelectedMedicine] = useState(null);
  const [currentBatches, setCurrentBatches] = useState([]);
  const [currentTransactions, setCurrentTransactions] = useState([]);
  const [modalLoading, setModalLoading] = useState(false);
  const [lastReservation, setLastReservation] = useState(null);

  // Form states
  const [newMedicine, setNewMedicine] = useState({
    name: '',
    category: 'Antibiotics',
    description: '',
    dosageForm: 'Tablet',
    strength: '250mg',
    unitPrice: 100,
    manufacturer: '',
    reorderLevel: 10,
  });

  const [stockInData, setStockInData] = useState({
    supplierId: '',
    batchNumber: '',
    quantity: 50,
    expiryDate: new Date(Date.now() + 180 * 86400000).toISOString().slice(0, 10),
  });

  const [reserveData, setReserveData] = useState({
    medicineId: '',
    quantity: 1,
    referenceType: 'Treatment',
    referenceId: undefined,
  });

  const [newSupplier, setNewSupplier] = useState({
    name: '',
    contactPerson: '',
    phone: '',
    email: '',
    address: '',
  });

  // Auto-clear success message
  useEffect(() => {
    if (!success) return;
    const t = setTimeout(() => setSuccess(''), 4000);
    return () => clearTimeout(t);
  }, [success]);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [paged, lowStock, expiring, expired, sups] = await Promise.all([
        getMedicines({
          search: searchQuery || undefined,
          category: selectedCategory || undefined,
          page,
          pageSize,
        }),
        getLowStock(),
        getExpiring(30),
        getExpiredStock(),
        getSuppliers(),
      ]);

      setMedicines(paged.items || []);
      setTotalMedicines(paged.total || 0);
      setLowStockList(lowStock || []);
      setExpiringBatches(expiring || []);
      setExpiredBatches(expired || []);
      setSuppliers(sups || []);

      if (sups?.length > 0 && !stockInData.supplierId) {
        setStockInData((prev) => ({ ...prev, supplierId: sups[0].id }));
      }
    } catch (err) {
      const msg = err.response?.data?.detail || err.response?.data?.message || err.message;
      setError(msg || 'Failed to load inventory records.');
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, searchQuery, selectedCategory, stockInData.supplierId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Handlers
  const handleCreateMedicine = async (e) => {
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
      setIsAddMedOpen(false);
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
      const msg = err.response?.data?.detail || err.response?.data?.message || err.message;
      setError(msg || 'Failed to create medicine.');
    } finally {
      setModalLoading(false);
    }
  };

  const handleOpenStockIn = (med) => {
    setSelectedMedicine(med);
    setStockInData({
      supplierId: suppliers[0]?.id || '',
      batchNumber: `LOT-${Date.now().toString().slice(-6)}`,
      quantity: 50,
      expiryDate: new Date(Date.now() + 180 * 86400000).toISOString().slice(0, 10),
    });
    setIsStockInOpen(true);
  };

  const handleReceiveStock = async (e) => {
    e.preventDefault();
    if (!selectedMedicine) return;
    if (!stockInData.supplierId) {
      setError('Please select a valid supplier.');
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
      const msg = err.response?.data?.detail || err.response?.data?.message || err.message;
      setError(msg || 'Failed to receive stock consignment.');
    } finally {
      setModalLoading(false);
    }
  };

  const handleOpenBatches = async (med) => {
    setSelectedMedicine(med);
    setIsBatchesOpen(true);
    setModalLoading(true);
    try {
      const b = await getBatches(med.id);
      setCurrentBatches(b || []);
    } catch (err) {
      const msg = err.response?.data?.detail || err.response?.data?.message || err.message;
      setError(msg || 'Failed to load batches.');
    } finally {
      setModalLoading(false);
    }
  };

  const handleOpenTransactions = async (med) => {
    setSelectedMedicine(med);
    setIsTxOpen(true);
    setModalLoading(true);
    try {
      const txs = await getTransactions(med.id);
      setCurrentTransactions(txs || []);
    } catch (err) {
      const msg = err.response?.data?.detail || err.response?.data?.message || err.message;
      setError(msg || 'Failed to load transactions.');
    } finally {
      setModalLoading(false);
    }
  };

  const handleOpenReserve = (med) => {
    setSelectedMedicine(med);
    setReserveData({
      medicineId: med.id,
      quantity: 1,
      referenceType: 'Treatment',
      referenceId: undefined,
    });
    setLastReservation(null);
    setIsReserveOpen(true);
  };

  const handleCreateReservation = async (e) => {
    e.preventDefault();
    if (!selectedMedicine) return;
    setModalLoading(true);
    setError('');
    try {
      const res = await reserveMedicine(reserveData);
      setLastReservation(res);
      setSuccess(`Reserved ${res.quantity} unit(s) of "${selectedMedicine.name}".`);
      await loadData();
    } catch (err) {
      const msg = err.response?.data?.detail || err.response?.data?.message || err.message;
      setError(msg || 'Failed to reserve stock.');
    } finally {
      setModalLoading(false);
    }
  };

  const handleDispenseReservation = async (resId) => {
    setModalLoading(true);
    setError('');
    try {
      await dispenseReservation(resId);
      setSuccess('Reservation successfully dispensed via FEFO batch allocation.');
      setIsReserveOpen(false);
      setLastReservation(null);
      await loadData();
    } catch (err) {
      const msg = err.response?.data?.detail || err.response?.data?.message || err.message;
      setError(msg || 'Failed to dispense reservation.');
    } finally {
      setModalLoading(false);
    }
  };

  const handleCancelReservation = async (resId) => {
    setModalLoading(true);
    setError('');
    try {
      await cancelReservation(resId);
      setSuccess('Reservation cancelled and restored to available inventory.');
      setIsReserveOpen(false);
      setLastReservation(null);
      await loadData();
    } catch (err) {
      const msg = err.response?.data?.detail || err.response?.data?.message || err.message;
      setError(msg || 'Failed to cancel reservation.');
    } finally {
      setModalLoading(false);
    }
  };

  const handleCreateSupplier = async (e) => {
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
      setIsAddSupOpen(false);
      setNewSupplier({
        name: '',
        contactPerson: '',
        phone: '',
        email: '',
        address: '',
      });
      await loadData();
    } catch (err) {
      const msg = err.response?.data?.detail || err.response?.data?.message || err.message;
      setError(msg || 'Failed to register supplier.');
    } finally {
      setModalLoading(false);
    }
  };

  const categories = [
    'All Categories',
    'Antibiotics',
    'Anti-inflammatory',
    'Vaccines',
    'Analgesics',
    'Dermatology',
    'Supplements',
    'Parasiticides',
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Top Banner & Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <h2 style={{ fontSize: '1.5rem', fontWeight: '700', color: '#111827', margin: 0 }}>
            Medicine & Inventory Management
          </h2>
          <p style={{ color: '#6b7280', fontSize: '0.875rem', marginTop: '0.25rem' }}>
            Multi-batch FEFO dispensing, atomic stock reservation, and pharmaceutical supplier tracking.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
          <button
            onClick={loadData}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '0.4rem',
              padding: '0.5rem 0.85rem',
              borderRadius: '0.5rem',
              border: '1px solid #d1d5db',
              backgroundColor: '#ffffff',
              color: '#374151',
              fontSize: '0.85rem',
              fontWeight: '600',
              cursor: 'pointer',
            }}
          >
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
          </button>
          {isOfficerOrManager && (
            <>
              <button
                onClick={() => setIsAddSupOpen(true)}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '0.4rem',
                  padding: '0.5rem 0.85rem',
                  borderRadius: '0.5rem',
                  border: '1px solid #d1d5db',
                  backgroundColor: '#ffffff',
                  color: '#374151',
                  fontSize: '0.85rem',
                  fontWeight: '600',
                  cursor: 'pointer',
                }}
              >
                <Truck size={14} /> Add Supplier
              </button>
              <button
                onClick={() => setIsAddMedOpen(true)}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '0.4rem',
                  padding: '0.5rem 0.95rem',
                  borderRadius: '0.5rem',
                  border: 'none',
                  backgroundColor: '#111827',
                  color: '#ffffff',
                  fontSize: '0.85rem',
                  fontWeight: '600',
                  cursor: 'pointer',
                }}
              >
                <Plus size={14} /> Add Medicine
              </button>
            </>
          )}
        </div>
      </div>

      {/* Notifications */}
      {error && (
        <div
          style={{
            padding: '0.75rem 1rem',
            backgroundColor: '#FEF2F2',
            border: '1px solid #F87171',
            borderRadius: '0.5rem',
            color: '#B91C1C',
            fontSize: '0.875rem',
            display: 'flex',
            alignItems: 'center',
            gap: '0.5rem',
          }}
        >
          <AlertCircle size={16} /> <strong>Error:</strong> {error}
        </div>
      )}
      {success && (
        <div
          style={{
            padding: '0.75rem 1rem',
            backgroundColor: '#ECFDF5',
            border: '1px solid #34D399',
            borderRadius: '0.5rem',
            color: '#065F46',
            fontSize: '0.875rem',
            display: 'flex',
            alignItems: 'center',
            gap: '0.5rem',
          }}
        >
          <CheckCircle size={16} /> <strong>Success:</strong> {success}
        </div>
      )}

      {/* KPI Cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem' }}>
        <div style={{ backgroundColor: '#ffffff', padding: '1.25rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb', boxShadow: '0 1px 2px rgba(0,0,0,0.05)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: '0.8rem', color: '#6b7280', fontWeight: '600' }}>Total SKUs</span>
            <div style={{ padding: '0.4rem', borderRadius: '0.5rem', backgroundColor: '#f3f4f6', color: '#374151' }}>
              <Package size={16} />
            </div>
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: '700', color: '#111827', marginTop: '0.5rem' }}>{totalMedicines}</div>
          <span style={{ fontSize: '0.75rem', color: '#9ca3af' }}>Active catalog products</span>
        </div>

        <div style={{ backgroundColor: '#ffffff', padding: '1.25rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb', boxShadow: '0 1px 2px rgba(0,0,0,0.05)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: '0.8rem', color: '#6b7280', fontWeight: '600' }}>Low Stock</span>
            <div style={{ padding: '0.4rem', borderRadius: '0.5rem', backgroundColor: lowStockList.length > 0 ? '#FEF3C7' : '#f3f4f6', color: lowStockList.length > 0 ? '#D97706' : '#6b7280' }}>
              <AlertTriangle size={16} />
            </div>
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: '700', color: lowStockList.length > 0 ? '#D97706' : '#111827', marginTop: '0.5rem' }}>
            {lowStockList.length}
          </div>
          <span style={{ fontSize: '0.75rem', color: '#9ca3af' }}>Below reorder threshold</span>
        </div>

        <div style={{ backgroundColor: '#ffffff', padding: '1.25rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb', boxShadow: '0 1px 2px rgba(0,0,0,0.05)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: '0.8rem', color: '#6b7280', fontWeight: '600' }}>Expiring (30d)</span>
            <div style={{ padding: '0.4rem', borderRadius: '0.5rem', backgroundColor: expiringBatches.length > 0 ? '#FEE2E2' : '#f3f4f6', color: expiringBatches.length > 0 ? '#DC2626' : '#6b7280' }}>
              <Clock size={16} />
            </div>
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: '700', color: expiringBatches.length > 0 ? '#DC2626' : '#111827', marginTop: '0.5rem' }}>
            {expiringBatches.length}
          </div>
          <span style={{ fontSize: '0.75rem', color: '#9ca3af' }}>Batches near expiration</span>
        </div>

        <div style={{ backgroundColor: '#ffffff', padding: '1.25rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb', boxShadow: '0 1px 2px rgba(0,0,0,0.05)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: '0.8rem', color: '#6b7280', fontWeight: '600' }}>Suppliers</span>
            <div style={{ padding: '0.4rem', borderRadius: '0.5rem', backgroundColor: '#f3f4f6', color: '#374151' }}>
              <Truck size={16} />
            </div>
          </div>
          <div style={{ fontSize: '1.75rem', fontWeight: '700', color: '#111827', marginTop: '0.5rem' }}>{suppliers.length}</div>
          <span style={{ fontSize: '0.75rem', color: '#9ca3af' }}>Verified procurement partners</span>
        </div>
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', gap: '0.5rem', borderBottom: '1px solid #e5e7eb', paddingBottom: '0.5rem' }}>
        {[
          { id: 'catalog', label: 'Medicine Catalog', icon: <Pill size={15} /> },
          { id: 'lowStock', label: `Low Stock (${lowStockList.length})`, icon: <AlertTriangle size={15} /> },
          { id: 'expiring', label: `Expiring (${expiringBatches.length + expiredBatches.length})`, icon: <Clock size={15} /> },
          { id: 'suppliers', label: `Suppliers (${suppliers.length})`, icon: <Truck size={15} /> },
        ].map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '0.4rem',
              padding: '0.5rem 0.85rem',
              borderRadius: '0.5rem',
              border: 'none',
              backgroundColor: activeTab === tab.id ? '#111827' : 'transparent',
              color: activeTab === tab.id ? '#ffffff' : '#6b7280',
              fontWeight: '600',
              fontSize: '0.85rem',
              cursor: 'pointer',
            }}
          >
            {tab.icon} {tab.label}
          </button>
        ))}
      </div>

      {/* TAB 1: CATALOG */}
      {activeTab === 'catalog' && (
        <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.25rem', boxShadow: '0 1px 2px rgba(0,0,0,0.05)' }}>
          {/* Filter Bar */}
          <div style={{ display: 'flex', gap: '0.75rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flex: 1, minWidth: '240px', border: '1px solid #d1d5db', borderRadius: '0.5rem', padding: '0 0.75rem', backgroundColor: '#f9fafb' }}>
              <Search size={16} color="#9ca3af" />
              <input
                type="text"
                placeholder="Search medicine name, manufacturer..."
                value={searchQuery}
                onChange={(e) => {
                  setSearchQuery(e.target.value);
                  setPage(1);
                }}
                style={{ border: 'none', background: 'transparent', outline: 'none', width: '100%', height: '38px', fontSize: '0.875rem' }}
              />
            </div>
            <select
              value={selectedCategory}
              onChange={(e) => {
                setSelectedCategory(e.target.value === 'All Categories' ? '' : e.target.value);
                setPage(1);
              }}
              style={{ border: '1px solid #d1d5db', borderRadius: '0.5rem', padding: '0 0.75rem', height: '38px', fontSize: '0.875rem', backgroundColor: '#ffffff' }}
            >
              {categories.map((c) => (
                <option key={c} value={c === 'All Categories' ? '' : c}>{c}</option>
              ))}
            </select>
          </div>

          {/* Table */}
          {loading ? (
            <div style={{ display: 'grid', placeItems: 'center', minHeight: '200px' }}>
              <Loader2 size={28} className="animate-spin" color="#111827" />
            </div>
          ) : medicines.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '3rem', color: '#6b7280' }}>
              <Pill size={40} style={{ opacity: 0.3, margin: '0 auto 0.75rem' }} />
              <p>No medicines found matching the current search criteria.</p>
            </div>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontSize: '0.75rem', textTransform: 'uppercase' }}>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Medicine Name</th>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Category</th>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Dosage & Strength</th>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Unit Price</th>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Stock Status</th>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Available / Total</th>
                    <th style={{ padding: '0.75rem 0.5rem', textAlign: 'right' }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {medicines.map((med) => {
                    const isLow = med.availableQuantity <= med.reorderLevel;
                    return (
                      <tr key={med.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                        <td style={{ padding: '0.75rem 0.5rem' }}>
                          <strong style={{ display: 'block', color: '#111827' }}>{med.name}</strong>
                          <span style={{ fontSize: '0.75rem', color: '#9ca3af' }}>{med.manufacturer}</span>
                        </td>
                        <td style={{ padding: '0.75rem 0.5rem' }}>
                          <span style={{ padding: '0.2rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', backgroundColor: '#f3f4f6', color: '#4b5563', fontWeight: '500' }}>
                            {med.category}
                          </span>
                        </td>
                        <td style={{ padding: '0.75rem 0.5rem', color: '#4b5563' }}>
                          {med.dosageForm} • {med.strength}
                        </td>
                        <td style={{ padding: '0.75rem 0.5rem', fontWeight: '600', color: '#111827' }}>
                          LKR {med.unitPrice?.toFixed(2)}
                        </td>
                        <td style={{ padding: '0.75rem 0.5rem' }}>
                          {med.availableQuantity === 0 ? (
                            <span style={{ padding: '0.2rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', backgroundColor: '#FEE2E2', color: '#B91C1C', fontWeight: '600' }}>
                              Out of Stock
                            </span>
                          ) : isLow ? (
                            <span style={{ padding: '0.2rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', backgroundColor: '#FEF3C7', color: '#B45309', fontWeight: '600' }}>
                              Low Stock ({med.reorderLevel} min)
                            </span>
                          ) : (
                            <span style={{ padding: '0.2rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', backgroundColor: '#D1FAE5', color: '#065F46', fontWeight: '600' }}>
                              In Stock
                            </span>
                          )}
                        </td>
                        <td style={{ padding: '0.75rem 0.5rem' }}>
                          <strong style={{ color: '#111827' }}>{med.availableQuantity}</strong> / {med.totalQuantity}
                          {med.reservedQuantity > 0 && (
                            <span style={{ display: 'block', fontSize: '0.75rem', color: '#2563EB' }}>
                              ({med.reservedQuantity} reserved)
                            </span>
                          )}
                        </td>
                        <td style={{ padding: '0.75rem 0.5rem', textAlign: 'right' }}>
                          <div style={{ display: 'flex', gap: '0.35rem', justifyContent: 'flex-end' }}>
                            {isOfficerOrManager && (
                              <button
                                onClick={() => handleOpenStockIn(med)}
                                title="Stock-In Consignment"
                                style={{ display: 'inline-flex', alignItems: 'center', gap: '0.25rem', padding: '0.35rem 0.6rem', borderRadius: '0.375rem', border: '1px solid #d1d5db', backgroundColor: '#ffffff', fontSize: '0.75rem', fontWeight: '600', cursor: 'pointer' }}
                              >
                                <PackagePlus size={13} /> Stock-In
                              </button>
                            )}
                            {isVet && (
                              <button
                                onClick={() => handleOpenReserve(med)}
                                disabled={med.availableQuantity === 0}
                                title="Reserve for Treatment"
                                style={{ display: 'inline-flex', alignItems: 'center', gap: '0.25rem', padding: '0.35rem 0.6rem', borderRadius: '0.375rem', border: '1px solid #d1d5db', backgroundColor: '#ffffff', fontSize: '0.75rem', fontWeight: '600', cursor: med.availableQuantity === 0 ? 'not-allowed' : 'pointer', opacity: med.availableQuantity === 0 ? 0.5 : 1 }}
                              >
                                <ClipboardList size={13} /> Reserve
                              </button>
                            )}
                            <button
                              onClick={() => handleOpenBatches(med)}
                              title="View FEFO Batches"
                              style={{ padding: '0.35rem', borderRadius: '0.375rem', border: 'none', backgroundColor: '#f3f4f6', cursor: 'pointer' }}
                            >
                              <Layers size={14} color="#4b5563" />
                            </button>
                            <button
                              onClick={() => handleOpenTransactions(med)}
                              title="Audit History"
                              style={{ padding: '0.35rem', borderRadius: '0.375rem', border: 'none', backgroundColor: '#f3f4f6', cursor: 'pointer' }}
                            >
                              <History size={14} color="#4b5563" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>

              {/* Pagination */}
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '1rem' }}>
                <span style={{ fontSize: '0.8rem', color: '#6b7280' }}>
                  Showing {(page - 1) * pageSize + 1} - {Math.min(page * pageSize, totalMedicines)} of {totalMedicines} items
                </span>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    style={{ padding: '0.35rem 0.65rem', borderRadius: '0.375rem', border: '1px solid #d1d5db', backgroundColor: '#ffffff', cursor: page <= 1 ? 'not-allowed' : 'pointer', opacity: page <= 1 ? 0.5 : 1 }}
                  >
                    <ChevronLeft size={14} />
                  </button>
                  <button
                    disabled={page * pageSize >= totalMedicines}
                    onClick={() => setPage((p) => p + 1)}
                    style={{ padding: '0.35rem 0.65rem', borderRadius: '0.375rem', border: '1px solid #d1d5db', backgroundColor: '#ffffff', cursor: page * pageSize >= totalMedicines ? 'not-allowed' : 'pointer', opacity: page * pageSize >= totalMedicines ? 0.5 : 1 }}
                  >
                    <ChevronRight size={14} />
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* TAB 2: LOW STOCK */}
      {activeTab === 'lowStock' && (
        <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.25rem' }}>
          <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.75rem' }}>
            Low Stock Alerts
          </h3>
          {lowStockList.length === 0 ? (
            <p style={{ color: '#6b7280', padding: '2rem', textAlign: 'center' }}>
              All medicines currently meet minimum required stock thresholds.
            </p>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontSize: '0.75rem' }}>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Medicine</th>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Category</th>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Available Units</th>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Reorder Threshold</th>
                    <th style={{ padding: '0.75rem 0.5rem', textAlign: 'right' }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {lowStockList.map((m) => (
                    <tr key={m.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                      <td style={{ padding: '0.75rem 0.5rem' }}>
                        <strong>{m.name}</strong>
                      </td>
                      <td style={{ padding: '0.75rem 0.5rem' }}>{m.category}</td>
                      <td style={{ padding: '0.75rem 0.5rem', color: '#DC2626', fontWeight: '700' }}>
                        {m.availableQuantity}
                      </td>
                      <td style={{ padding: '0.75rem 0.5rem' }}>{m.reorderLevel} units</td>
                      <td style={{ padding: '0.75rem 0.5rem', textAlign: 'right' }}>
                        {isOfficerOrManager && (
                          <button
                            onClick={() => handleOpenStockIn(m)}
                            style={{ display: 'inline-flex', alignItems: 'center', gap: '0.25rem', padding: '0.35rem 0.65rem', borderRadius: '0.375rem', border: 'none', backgroundColor: '#111827', color: '#ffffff', fontSize: '0.75rem', fontWeight: '600', cursor: 'pointer' }}
                          >
                            <PackagePlus size={13} /> Restock Now
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* TAB 3: EXPIRING BATCHES */}
      {activeTab === 'expiring' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {/* Already Expired */}
          <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.25rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <div style={{ padding: '0.3rem', borderRadius: '0.375rem', backgroundColor: '#FEE2E2' }}>
                <AlertCircle size={16} color="#DC2626" />
              </div>
              <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', margin: 0 }}>
                Expired Stocks <span style={{ fontSize: '0.8rem', fontWeight: '400', color: '#DC2626' }}>— {expiredBatches.length} batch{expiredBatches.length !== 1 ? 'es' : ''} with remaining units</span>
              </h3>
            </div>
            {expiredBatches.length === 0 ? (
              <p style={{ color: '#6b7280', padding: '1rem', textAlign: 'center', fontSize: '0.875rem' }}>
                No expired batches with remaining stock found.
              </p>
            ) : (
              <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
                  <thead>
                    <tr style={{ borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontSize: '0.75rem' }}>
                      <th style={{ padding: '0.75rem 0.5rem' }}>Batch Number</th>
                      <th style={{ padding: '0.75rem 0.5rem' }}>Medicine ID</th>
                      <th style={{ padding: '0.75rem 0.5rem' }}>Remaining Units</th>
                      <th style={{ padding: '0.75rem 0.5rem' }}>Expired On</th>
                      <th style={{ padding: '0.75rem 0.5rem' }}>Days Overdue</th>
                    </tr>
                  </thead>
                  <tbody>
                    {expiredBatches.map((b) => {
                      const expiredDaysAgo = Math.floor((new Date() - new Date(b.expiryDate)) / 86400000);
                      return (
                        <tr key={b.id} style={{ borderBottom: '1px solid #f3f4f6', backgroundColor: '#FFF5F5' }}>
                          <td style={{ padding: '0.75rem 0.5rem' }}>
                            <strong style={{ color: '#B91C1C' }}>{b.batchNumber}</strong>
                          </td>
                          <td style={{ padding: '0.75rem 0.5rem', color: '#6b7280', fontSize: '0.75rem' }}>
                            {b.medicineId}
                          </td>
                          <td style={{ padding: '0.75rem 0.5rem', fontWeight: '700', color: '#B91C1C' }}>
                            {b.quantity}
                          </td>
                          <td style={{ padding: '0.75rem 0.5rem', color: '#DC2626', fontWeight: '600' }}>
                            {b.expiryDate}
                          </td>
                          <td style={{ padding: '0.75rem 0.5rem' }}>
                            <span style={{ padding: '0.2rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', backgroundColor: '#FEE2E2', color: '#B91C1C', fontWeight: '700' }}>
                              {expiredDaysAgo}d overdue
                            </span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Expiring Soon */}
          <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.25rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <div style={{ padding: '0.3rem', borderRadius: '0.375rem', backgroundColor: '#FEF3C7' }}>
                <Clock size={16} color="#D97706" />
              </div>
              <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', margin: 0 }}>
                Expiring Within 30 Days <span style={{ fontSize: '0.8rem', fontWeight: '400', color: '#D97706' }}>— {expiringBatches.length} batch{expiringBatches.length !== 1 ? 'es' : ''}</span>
              </h3>
            </div>
            {expiringBatches.length === 0 ? (
              <p style={{ color: '#6b7280', padding: '1rem', textAlign: 'center', fontSize: '0.875rem' }}>
                No active batches expiring within the next 30 days. Stock shelf-life is optimal.
              </p>
            ) : (
              <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
                  <thead>
                    <tr style={{ borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontSize: '0.75rem' }}>
                      <th style={{ padding: '0.75rem 0.5rem' }}>Batch Number</th>
                      <th style={{ padding: '0.75rem 0.5rem' }}>Medicine ID</th>
                      <th style={{ padding: '0.75rem 0.5rem' }}>Remaining Units</th>
                      <th style={{ padding: '0.75rem 0.5rem' }}>Expiry Date</th>
                      <th style={{ padding: '0.75rem 0.5rem' }}>Days Remaining</th>
                    </tr>
                  </thead>
                  <tbody>
                    {expiringBatches.map((b) => {
                      const daysLeft = Math.ceil((new Date(b.expiryDate) - new Date()) / 86400000);
                      return (
                        <tr key={b.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                          <td style={{ padding: '0.75rem 0.5rem' }}>
                            <strong>{b.batchNumber}</strong>
                          </td>
                          <td style={{ padding: '0.75rem 0.5rem', color: '#6b7280', fontSize: '0.75rem' }}>
                            {b.medicineId}
                          </td>
                          <td style={{ padding: '0.75rem 0.5rem', fontWeight: '600' }}>{b.quantity}</td>
                          <td style={{ padding: '0.75rem 0.5rem', color: '#D97706', fontWeight: '600' }}>
                            {b.expiryDate}
                          </td>
                          <td style={{ padding: '0.75rem 0.5rem' }}>
                            <span style={{ padding: '0.2rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', backgroundColor: daysLeft <= 7 ? '#FEE2E2' : '#FEF3C7', color: daysLeft <= 7 ? '#DC2626' : '#92400E', fontWeight: '700' }}>
                              {daysLeft}d left
                            </span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}

      {/* TAB 4: SUPPLIERS */}
      {activeTab === 'suppliers' && (
        <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.25rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
            <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', margin: 0 }}>
              Registered Suppliers
            </h3>
            {isOfficerOrManager && (
              <button
                onClick={() => setIsAddSupOpen(true)}
                style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', padding: '0.4rem 0.8rem', borderRadius: '0.375rem', border: 'none', backgroundColor: '#111827', color: '#ffffff', fontSize: '0.8rem', fontWeight: '600', cursor: 'pointer' }}
              >
                <Plus size={14} /> New Supplier
              </button>
            )}
          </div>
          {suppliers.length === 0 ? (
            <p style={{ color: '#6b7280', padding: '2rem', textAlign: 'center' }}>
              No suppliers registered in the directory yet.
            </p>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontSize: '0.75rem' }}>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Supplier Name</th>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Contact Person</th>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Phone</th>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Email</th>
                    <th style={{ padding: '0.75rem 0.5rem' }}>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {suppliers.map((s) => (
                    <tr key={s.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                      <td style={{ padding: '0.75rem 0.5rem' }}>
                        <strong>{s.name}</strong>
                        <span style={{ display: 'block', fontSize: '0.75rem', color: '#9ca3af' }}>{s.address}</span>
                      </td>
                      <td style={{ padding: '0.75rem 0.5rem' }}>{s.contactPerson}</td>
                      <td style={{ padding: '0.75rem 0.5rem' }}>{s.phone}</td>
                      <td style={{ padding: '0.75rem 0.5rem' }}>{s.email}</td>
                      <td style={{ padding: '0.75rem 0.5rem' }}>
                        <span style={{ padding: '0.2rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', backgroundColor: s.status === 'Active' ? '#D1FAE5' : '#F3F4F6', color: s.status === 'Active' ? '#065F46' : '#6B7280', fontWeight: '600' }}>
                          {s.status}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* MODAL: ADD MEDICINE */}
      {isAddMedOpen && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'grid', placeItems: 'center', zIndex: 50, padding: '1rem' }}>
          <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', maxWidth: '550px', width: '100%', padding: '1.5rem', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
              <h3 style={{ fontSize: '1.25rem', fontWeight: '700', color: '#111827', margin: 0 }}>Add New Medicine</h3>
              <button onClick={() => setIsAddMedOpen(false)} style={{ border: 'none', background: 'transparent', cursor: 'pointer' }}><X size={18} /></button>
            </div>
            <form onSubmit={handleCreateMedicine}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div style={{ gridColumn: 'span 2' }}>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Medicine Name *</label>
                  <input required type="text" value={newMedicine.name} onChange={(e) => setNewMedicine({ ...newMedicine, name: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Category *</label>
                  <select value={newMedicine.category} onChange={(e) => setNewMedicine({ ...newMedicine, category: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }}>
                    <option value="Antibiotics">Antibiotics</option>
                    <option value="Anti-inflammatory">Anti-inflammatory</option>
                    <option value="Vaccines">Vaccines</option>
                    <option value="Analgesics">Analgesics</option>
                    <option value="Dermatology">Dermatology</option>
                    <option value="Supplements">Supplements</option>
                    <option value="Parasiticides">Parasiticides</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Dosage Form *</label>
                  <input required type="text" value={newMedicine.dosageForm} onChange={(e) => setNewMedicine({ ...newMedicine, dosageForm: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Strength *</label>
                  <input required type="text" value={newMedicine.strength} onChange={(e) => setNewMedicine({ ...newMedicine, strength: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Unit Price (LKR) *</label>
                  <input required min="0" step="0.01" type="number" value={newMedicine.unitPrice} onChange={(e) => setNewMedicine({ ...newMedicine, unitPrice: parseFloat(e.target.value) || 0 })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Reorder Level *</label>
                  <input required min="0" type="number" value={newMedicine.reorderLevel} onChange={(e) => setNewMedicine({ ...newMedicine, reorderLevel: parseInt(e.target.value) || 0 })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
                <div style={{ gridColumn: 'span 2' }}>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Manufacturer *</label>
                  <input required type="text" value={newMedicine.manufacturer} onChange={(e) => setNewMedicine({ ...newMedicine, manufacturer: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '1.25rem' }}>
                <button type="button" onClick={() => setIsAddMedOpen(false)} style={{ padding: '0.5rem 0.85rem', borderRadius: '0.375rem', border: '1px solid #d1d5db', backgroundColor: '#ffffff', cursor: 'pointer' }}>Cancel</button>
                <button type="submit" disabled={modalLoading} style={{ padding: '0.5rem 1rem', borderRadius: '0.375rem', border: 'none', backgroundColor: '#111827', color: '#ffffff', fontWeight: '600', cursor: 'pointer' }}>
                  {modalLoading ? 'Saving...' : 'Create Medicine'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: STOCK-IN */}
      {isStockInOpen && selectedMedicine && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'grid', placeItems: 'center', zIndex: 50, padding: '1rem' }}>
          <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', maxWidth: '500px', width: '100%', padding: '1.5rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
              <h3 style={{ fontSize: '1.125rem', fontWeight: '700', color: '#111827', margin: 0 }}>Stock-In: {selectedMedicine.name}</h3>
              <button onClick={() => setIsStockInOpen(false)} style={{ border: 'none', background: 'transparent', cursor: 'pointer' }}><X size={18} /></button>
            </div>
            <form onSubmit={handleReceiveStock}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Supplier *</label>
                  <select required value={stockInData.supplierId} onChange={(e) => setStockInData({ ...stockInData, supplierId: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }}>
                    {suppliers.map((s) => (
                      <option key={s.id} value={s.id}>{s.name}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Batch / Lot Number *</label>
                  <input required type="text" value={stockInData.batchNumber} onChange={(e) => setStockInData({ ...stockInData, batchNumber: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Quantity (Units) *</label>
                  <input required min="1" type="number" value={stockInData.quantity} onChange={(e) => setStockInData({ ...stockInData, quantity: parseInt(e.target.value) || 1 })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Expiry Date *</label>
                  <input required type="date" value={stockInData.expiryDate} onChange={(e) => setStockInData({ ...stockInData, expiryDate: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '1.25rem' }}>
                <button type="button" onClick={() => setIsStockInOpen(false)} style={{ padding: '0.5rem 0.85rem', borderRadius: '0.375rem', border: '1px solid #d1d5db', backgroundColor: '#ffffff', cursor: 'pointer' }}>Cancel</button>
                <button type="submit" disabled={modalLoading} style={{ padding: '0.5rem 1rem', borderRadius: '0.375rem', border: 'none', backgroundColor: '#111827', color: '#ffffff', fontWeight: '600', cursor: 'pointer' }}>
                  {modalLoading ? 'Processing...' : 'Confirm Stock-In'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: BATCHES VIEW */}
      {isBatchesOpen && selectedMedicine && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'grid', placeItems: 'center', zIndex: 50, padding: '1rem' }}>
          <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', maxWidth: '600px', width: '100%', padding: '1.5rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
              <h3 style={{ fontSize: '1.125rem', fontWeight: '700', color: '#111827', margin: 0 }}>Batches: {selectedMedicine.name}</h3>
              <button onClick={() => setIsBatchesOpen(false)} style={{ border: 'none', background: 'transparent', cursor: 'pointer' }}><X size={18} /></button>
            </div>
            {modalLoading ? (
              <div style={{ textAlign: 'center', padding: '2rem' }}><Loader2 className="animate-spin" size={24} /></div>
            ) : currentBatches.length === 0 ? (
              <p style={{ color: '#6b7280', padding: '1.5rem', textAlign: 'center' }}>No batches found.</p>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid #e5e7eb', color: '#6b7280' }}>
                    <th style={{ padding: '0.5rem' }}>Batch Lot</th>
                    <th style={{ padding: '0.5rem' }}>Remaining Units</th>
                    <th style={{ padding: '0.5rem' }}>Expiry Date</th>
                  </tr>
                </thead>
                <tbody>
                  {currentBatches.map((b) => (
                    <tr key={b.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                      <td style={{ padding: '0.5rem' }}><strong>{b.batchNumber}</strong></td>
                      <td style={{ padding: '0.5rem' }}>{b.quantity}</td>
                      <td style={{ padding: '0.5rem' }}>{b.expiryDate}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '1rem' }}>
              <button onClick={() => setIsBatchesOpen(false)} style={{ padding: '0.5rem 1rem', borderRadius: '0.375rem', border: '1px solid #d1d5db', backgroundColor: '#ffffff', cursor: 'pointer' }}>Close</button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL: RESERVE / DISPENSE */}
      {isReserveOpen && selectedMedicine && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'grid', placeItems: 'center', zIndex: 50, padding: '1rem' }}>
          <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', maxWidth: '500px', width: '100%', padding: '1.5rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
              <h3 style={{ fontSize: '1.125rem', fontWeight: '700', color: '#111827', margin: 0 }}>Reserve / Dispense: {selectedMedicine.name}</h3>
              <button onClick={() => setIsReserveOpen(false)} style={{ border: 'none', background: 'transparent', cursor: 'pointer' }}><X size={18} /></button>
            </div>
            {!lastReservation ? (
              <form onSubmit={handleCreateReservation}>
                <p style={{ fontSize: '0.85rem', color: '#6b7280', marginBottom: '1rem' }}>
                  Available units to hold: <strong style={{ color: '#059669' }}>{selectedMedicine.availableQuantity}</strong>
                </p>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Quantity *</label>
                    <input required min="1" max={selectedMedicine.availableQuantity} type="number" value={reserveData.quantity} onChange={(e) => setReserveData({ ...reserveData, quantity: parseInt(e.target.value) || 1 })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Reference Type</label>
                    <select value={reserveData.referenceType} onChange={(e) => setReserveData({ ...reserveData, referenceType: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }}>
                      <option value="Treatment">Treatment</option>
                      <option value="Consultation">Consultation</option>
                      <option value="Surgery">Surgery</option>
                    </select>
                  </div>
                </div>
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '1.25rem' }}>
                  <button type="button" onClick={() => setIsReserveOpen(false)} style={{ padding: '0.5rem 0.85rem', borderRadius: '0.375rem', border: '1px solid #d1d5db', backgroundColor: '#ffffff', cursor: 'pointer' }}>Cancel</button>
                  <button type="submit" disabled={modalLoading} style={{ padding: '0.5rem 1rem', borderRadius: '0.375rem', border: 'none', backgroundColor: '#111827', color: '#ffffff', fontWeight: '600', cursor: 'pointer' }}>
                    {modalLoading ? 'Holding...' : 'Create Reservation'}
                  </button>
                </div>
              </form>
            ) : (
              <div>
                <div style={{ padding: '0.75rem', backgroundColor: '#EFF6FF', borderRadius: '0.5rem', border: '1px solid #BFDBFE', marginBottom: '1rem' }}>
                  <div style={{ fontSize: '0.85rem', color: '#1E40AF', fontWeight: '600' }}>Reservation Active (#{lastReservation.id.slice(0, 8)})</div>
                  <div style={{ fontSize: '0.8rem', color: '#1E3A8A' }}>Reserved: {lastReservation.quantity} units</div>
                </div>
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
                  <button onClick={() => handleCancelReservation(lastReservation.id)} style={{ padding: '0.5rem 0.85rem', borderRadius: '0.375rem', border: '1px solid #DC2626', color: '#DC2626', backgroundColor: '#ffffff', fontWeight: '600', cursor: 'pointer' }}>
                    Cancel Hold
                  </button>
                  {isOfficerOrManager && (
                    <button onClick={() => handleDispenseReservation(lastReservation.id)} style={{ padding: '0.5rem 1rem', borderRadius: '0.375rem', border: 'none', backgroundColor: '#059669', color: '#ffffff', fontWeight: '600', cursor: 'pointer' }}>
                      Dispense Stock Now
                    </button>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* MODAL: TRANSACTIONS / AUDIT LEDGER */}
      {isTxOpen && selectedMedicine && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'grid', placeItems: 'center', zIndex: 50, padding: '1rem' }}>
          <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', maxWidth: '650px', width: '100%', padding: '1.5rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
              <h3 style={{ fontSize: '1.125rem', fontWeight: '700', color: '#111827', margin: 0 }}>Audit Ledger: {selectedMedicine.name}</h3>
              <button onClick={() => setIsTxOpen(false)} style={{ border: 'none', background: 'transparent', cursor: 'pointer' }}><X size={18} /></button>
            </div>
            {modalLoading ? (
              <div style={{ textAlign: 'center', padding: '2rem' }}><Loader2 className="animate-spin" size={24} /></div>
            ) : currentTransactions.length === 0 ? (
              <p style={{ color: '#6b7280', padding: '1.5rem', textAlign: 'center' }}>No transaction history recorded.</p>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid #e5e7eb', color: '#6b7280' }}>
                    <th style={{ padding: '0.5rem' }}>Timestamp</th>
                    <th style={{ padding: '0.5rem' }}>Action Type</th>
                    <th style={{ padding: '0.5rem' }}>Quantity Change</th>
                  </tr>
                </thead>
                <tbody>
                  {currentTransactions.map((t) => (
                    <tr key={t.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                      <td style={{ padding: '0.5rem' }}>{new Date(t.occurredAt).toLocaleString()}</td>
                      <td style={{ padding: '0.5rem' }}><strong>{t.type}</strong></td>
                      <td style={{ padding: '0.5rem', color: t.quantityChange > 0 ? '#059669' : '#DC2626', fontWeight: '700' }}>
                        {t.quantityChange > 0 ? `+${t.quantityChange}` : t.quantityChange}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '1rem' }}>
              <button onClick={() => setIsTxOpen(false)} style={{ padding: '0.5rem 1rem', borderRadius: '0.375rem', border: '1px solid #d1d5db', backgroundColor: '#ffffff', cursor: 'pointer' }}>Close</button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL: ADD SUPPLIER */}
      {isAddSupOpen && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'grid', placeItems: 'center', zIndex: 50, padding: '1rem' }}>
          <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', maxWidth: '500px', width: '100%', padding: '1.5rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
              <h3 style={{ fontSize: '1.125rem', fontWeight: '700', color: '#111827', margin: 0 }}>Register New Supplier</h3>
              <button onClick={() => setIsAddSupOpen(false)} style={{ border: 'none', background: 'transparent', cursor: 'pointer' }}><X size={18} /></button>
            </div>
            <form onSubmit={handleCreateSupplier}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Supplier Name *</label>
                  <input required type="text" value={newSupplier.name} onChange={(e) => setNewSupplier({ ...newSupplier, name: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Contact Person *</label>
                  <input required type="text" value={newSupplier.contactPerson} onChange={(e) => setNewSupplier({ ...newSupplier, contactPerson: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Phone *</label>
                  <input required type="text" value={newSupplier.phone} onChange={(e) => setNewSupplier({ ...newSupplier, phone: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Email *</label>
                  <input required type="email" value={newSupplier.email} onChange={(e) => setNewSupplier({ ...newSupplier, email: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', marginBottom: '0.25rem' }}>Address *</label>
                  <input required type="text" value={newSupplier.address} onChange={(e) => setNewSupplier({ ...newSupplier, address: e.target.value })} style={{ width: '100%', height: '36px', border: '1px solid #d1d5db', borderRadius: '0.375rem', padding: '0 0.5rem' }} />
                </div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '1.25rem' }}>
                <button type="button" onClick={() => setIsAddSupOpen(false)} style={{ padding: '0.5rem 0.85rem', borderRadius: '0.375rem', border: '1px solid #d1d5db', backgroundColor: '#ffffff', cursor: 'pointer' }}>Cancel</button>
                <button type="submit" disabled={modalLoading} style={{ padding: '0.5rem 1rem', borderRadius: '0.375rem', border: 'none', backgroundColor: '#111827', color: '#ffffff', fontWeight: '600', cursor: 'pointer' }}>
                  {modalLoading ? 'Saving...' : 'Register'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
