import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { BillingPage } from '../../features/billing/BillingPage';
import { validateQuotationInput } from '../../services/billingService';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return {
    ok,
    status,
    text: async () => (body === undefined ? '' : JSON.stringify(body)),
  } as Response;
}

const baseQuotation = {
  id: 'quo-1',
  appointmentId: 'appt-1',
  budget: 10000,
  subtotal: 4500,
  total: 4500,
  isWithinBudget: true,
  status: 'Draft',
  items: [
    { id: 'li-1', category: 'Consultation', description: 'General checkup', quantity: 1, unitPrice: 3500, totalPrice: 3500 },
  ],
  createdAt: '2026-01-01T00:00:00',
  updatedAt: '2026-01-01T00:00:00',
};

function setupFetchMock() {
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    if (!init || init.method === undefined) {
      return Promise.resolve(jsonResponse([baseQuotation]));
    }
    if (init.method === 'PUT') {
      return Promise.resolve(jsonResponse({ ...baseQuotation }));
    }
    return Promise.resolve(jsonResponse(baseQuotation));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('validateQuotationInput (pure function)', () => {
  it('rejects a negative budget', () => {
    expect(validateQuotationInput({ budget: -1, items: [{ category: 'Other', description: 'x', quantity: 1, unitPrice: 1 }] }))
      .toBe('Budget cannot be negative');
  });

  it('rejects a quantity of zero or less', () => {
    expect(validateQuotationInput({ budget: 100, items: [{ category: 'Other', description: 'x', quantity: 0, unitPrice: 1 }] }))
      .toBe('Quantity must be greater than zero');
  });

  it('rejects a negative unit price', () => {
    expect(validateQuotationInput({ budget: 100, items: [{ category: 'Other', description: 'x', quantity: 1, unitPrice: -5 }] }))
      .toBe('Unit price cannot be negative');
  });

  it('rejects a missing description', () => {
    expect(validateQuotationInput({ budget: 100, items: [{ category: 'Other', description: '', quantity: 1, unitPrice: 1 }] }))
      .toBe('Item description is required');
  });

  it('accepts a fully valid quotation', () => {
    expect(validateQuotationInput({ budget: 100, items: [{ category: 'Other', description: 'x', quantity: 1, unitPrice: 1 }] }))
      .toBeNull();
  });
});

describe('BillingPage form validation (UI)', () => {
  let fetchMock: ReturnType<typeof setupFetchMock>;

  beforeEach(() => {
    fetchMock = setupFetchMock();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('blocks saving a draft when quantity is not greater than zero and does not call the API', async () => {
    const user = userEvent.setup();
    render(<BillingPage />);
    await waitFor(() => expect(screen.getAllByText('quo-1').length).toBeGreaterThan(0));

    const quantityInput = screen.getByLabelText('Item quantity');
    await user.clear(quantityInput);
    await user.type(quantityInput, '0');

    const putCallsBefore = fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT').length;
    await user.click(screen.getByRole('button', { name: /save draft/i }));

    expect(await screen.findByText('Quantity must be greater than zero')).toBeInTheDocument();
    const putCallsAfter = fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT').length;
    expect(putCallsAfter).toBe(putCallsBefore);
  });

  it('blocks saving a draft when unit price is negative and does not call the API', async () => {
    const user = userEvent.setup();
    render(<BillingPage />);
    await waitFor(() => expect(screen.getAllByText('quo-1').length).toBeGreaterThan(0));

    const priceInput = screen.getByLabelText('Item unit price');
    fireEvent.change(priceInput, { target: { value: '-10' } });

    const putCallsBefore = fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT').length;
    await user.click(screen.getByRole('button', { name: /save draft/i }));

    expect(await screen.findByText('Unit price cannot be negative')).toBeInTheDocument();
    const putCallsAfter = fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT').length;
    expect(putCallsAfter).toBe(putCallsBefore);
  });

  it('blocks saving a draft when a required item description is cleared', async () => {
    const user = userEvent.setup();
    render(<BillingPage />);
    await waitFor(() => expect(screen.getAllByText('quo-1').length).toBeGreaterThan(0));

    const descriptionInput = screen.getByLabelText('Item description');
    await user.clear(descriptionInput);

    const putCallsBefore = fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT').length;
    await user.click(screen.getByRole('button', { name: /save draft/i }));

    expect(await screen.findByText('Item description is required')).toBeInTheDocument();
    const putCallsAfter = fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT').length;
    expect(putCallsAfter).toBe(putCallsBefore);
  });

  it('calls the updateQuotation API when the submission is valid', async () => {
    const user = userEvent.setup();
    render(<BillingPage />);
    await waitFor(() => expect(screen.getAllByText('quo-1').length).toBeGreaterThan(0));

    await user.click(screen.getByRole('button', { name: /save draft/i }));

    await waitFor(() => {
      const putCall = fetchMock.mock.calls.find(([url, init]) => url.includes('/quotations/quo-1') && init?.method === 'PUT');
      expect(putCall).toBeTruthy();
    });
    expect(await screen.findByText('Draft saved')).toBeInTheDocument();
  });
});
