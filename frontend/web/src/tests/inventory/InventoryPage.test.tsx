import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { InventoryPage } from '../../features/inventory/InventoryPage';
import { renderWithAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return {
    ok,
    status,
    text: async () => (body === undefined ? '' : JSON.stringify(body)),
  } as Response;
}

const mockMedicines = {
  items: [
    {
      id: 'med-1',
      name: 'Amoxicillin 250mg',
      category: 'Antibiotics',
      description: 'Broad spectrum',
      dosageForm: 'Tablet',
      strength: '250mg',
      unitPrice: 120,
      manufacturer: 'Zoetis',
      status: 'Active',
      reorderLevel: 20,
      totalQuantity: 100,
      reservedQuantity: 15,
      availableQuantity: 85,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    },
    {
      id: 'med-2',
      name: 'Meloxicam Oral',
      category: 'Anti-inflammatory',
      description: 'NSAID',
      dosageForm: 'Suspension',
      strength: '1.5mg/ml',
      unitPrice: 850,
      manufacturer: 'Boehringer',
      status: 'Active',
      reorderLevel: 10,
      totalQuantity: 5,
      reservedQuantity: 0,
      availableQuantity: 5,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    },
  ],
  total: 2,
  page: 1,
  pageSize: 10,
};

const mockLowStock = [
  {
    id: 'med-2',
    name: 'Meloxicam Oral',
    category: 'Anti-inflammatory',
    dosageForm: 'Suspension',
    strength: '1.5mg/ml',
    availableQuantity: 5,
    reorderLevel: 10,
  },
];

const mockExpiring = [
  {
    id: 'b-1',
    medicineId: 'med-1',
    batchNumber: 'LOT-EXP-1',
    quantity: 25,
    expiryDate: '2026-04-01',
    status: 'Active',
  },
];

const mockSuppliers = [
  {
    id: 'sup-1',
    name: 'VetPharma Lanka',
    contactPerson: 'Perera',
    phone: '0112345678',
    email: 'info@vetpharma.lk',
    address: '123 Galle Rd, Colombo',
    status: 'Active',
  },
];

function setupDefaultMocks(fetchMock: ReturnType<typeof vi.fn>) {
  fetchMock.mockImplementation((url: string, init?: RequestInit) => {
    const urlStr = url.toString();
    const method = init?.method || 'GET';

    if (urlStr.includes('/medicines/low-stock')) {
      return Promise.resolve(jsonResponse(mockLowStock));
    }
    if (urlStr.includes('/medicines/expiring')) {
      return Promise.resolve(jsonResponse(mockExpiring));
    }
    if (urlStr.includes('/suppliers') && method === 'GET') {
      return Promise.resolve(jsonResponse(mockSuppliers));
    }
    if (urlStr.includes('/medicines') && method === 'GET') {
      return Promise.resolve(jsonResponse(mockMedicines));
    }
    if (urlStr.includes('/medicines') && method === 'POST') {
      return Promise.resolve(
        jsonResponse(
          {
            id: 'med-new',
            name: 'New Medicine',
            unitPrice: 50,
          },
          true,
          201
        )
      );
    }
    return Promise.resolve(jsonResponse([]));
  });
}

describe('InventoryPage component', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    setupDefaultMocks(fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders inventory dashboard with KPI cards and medicine table', async () => {
    renderWithAuth(<InventoryPage />, { role: 'InventoryOfficer' });

    await waitFor(() => {
      expect(screen.getByText('Medicine & Stock Control')).toBeInTheDocument();
      expect(screen.getByText('Amoxicillin 250mg')).toBeInTheDocument();
      expect(screen.getByText('Meloxicam Oral')).toBeInTheDocument();
      expect(screen.getByText('Total SKUs')).toBeInTheDocument();
      expect(screen.getByText('Low Stock Alerts')).toBeInTheDocument();
    });
  });

  it('shows Add Medicine and Add Supplier buttons for InventoryOfficer', async () => {
    renderWithAuth(<InventoryPage />, { role: 'InventoryOfficer' });

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /add medicine/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /add supplier/i })).toBeInTheDocument();
    });
  });

  it('switches between tabs: Low Stock, Expiring Batches, and Suppliers', async () => {
    const user = userEvent.setup();
    renderWithAuth(<InventoryPage />, { role: 'InventoryOfficer' });

    await waitFor(() => {
      expect(screen.getByText('Amoxicillin 250mg')).toBeInTheDocument();
    });

    // Click Low Stock tab
    const lowStockTab = screen.getByRole('button', { name: /low stock/i });
    await user.click(lowStockTab);
    expect(screen.getByText('Critical Inventory')).toBeInTheDocument();

    // Click Expiring Batches tab
    const expiringTab = screen.getByRole('button', { name: /expiring batches/i });
    await user.click(expiringTab);
    expect(screen.getByText('FEFO Stock Expiry Monitoring')).toBeInTheDocument();
    expect(screen.getByText('LOT-EXP-1')).toBeInTheDocument();

    // Click Suppliers tab
    const suppliersTab = screen.getByRole('button', { name: /suppliers/i });
    await user.click(suppliersTab);
    expect(screen.getByText('Procurement Directory')).toBeInTheDocument();
    expect(screen.getByText('VetPharma Lanka')).toBeInTheDocument();
  });

  it('opens and closes Add Medicine modal', async () => {
    const user = userEvent.setup();
    renderWithAuth(<InventoryPage />, { role: 'InventoryOfficer' });

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /add medicine/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /add medicine/i }));

    expect(screen.getByRole('heading', { name: 'Add New Medicine' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() => {
      expect(screen.queryByRole('heading', { name: 'Add New Medicine' })).not.toBeInTheDocument();
    });
  });

  it('allows Veterinarian to reserve stock but hides administrative buttons', async () => {
    renderWithAuth(<InventoryPage />, { role: 'Veterinarian' });

    await waitFor(() => {
      expect(screen.getByText('Amoxicillin 250mg')).toBeInTheDocument();
    });

    // Veterinarian cannot add new medicine or add supplier
    expect(screen.queryByRole('button', { name: /add medicine/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add supplier/i })).not.toBeInTheDocument();

    // But can reserve available medicines
    const reserveButtons = screen.getAllByRole('button', { name: /reserve/i });
    expect(reserveButtons.length).toBeGreaterThan(0);
  });
});
