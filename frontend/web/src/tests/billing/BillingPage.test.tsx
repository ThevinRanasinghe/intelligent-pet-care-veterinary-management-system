import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { BillingPage } from '../../features/billing/BillingPage';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return {
    ok,
    status,
    text: async () => (body === undefined ? '' : JSON.stringify(body)),
  } as Response;
}

const quotationResponse = {
  id: 'quo-1',
  appointmentId: 'appt-1',
  budget: 10000,
  subtotal: 4500,
  total: 4500,
  isWithinBudget: true,
  status: 'Draft',
  items: [
    { id: 'li-1', category: 'Consultation', description: 'General checkup', quantity: 1, unitPrice: 3500, totalPrice: 3500 },
    { id: 'li-2', category: 'Medicine', description: 'Antibiotics', quantity: 1, unitPrice: 1000, totalPrice: 1000 },
  ],
  createdAt: '2026-01-01T00:00:00',
  updatedAt: '2026-01-01T00:00:00',
};

describe('BillingPage component states', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders the quotation list when the API returns data', async () => {
    (fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(jsonResponse([quotationResponse]));
    render(<BillingPage />);

    await waitFor(() => expect(screen.getAllByText('quo-1').length).toBeGreaterThan(0));
    expect(screen.getByText('1 records')).toBeInTheDocument();
  });

  it('displays the empty state when there are no quotations', async () => {
    (fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(jsonResponse([]));
    render(<BillingPage />);

    await waitFor(() => expect(screen.getByText('No quotations found')).toBeInTheDocument());
  });

  it('displays the loading state while the request is pending', async () => {
    let resolveFn: (value: Response) => void = () => {};
    (fetch as unknown as ReturnType<typeof vi.fn>).mockImplementation(
      () => new Promise<Response>((resolve) => { resolveFn = resolve; }),
    );
    render(<BillingPage />);

    expect(screen.getByText('Loading quotations...')).toBeInTheDocument();
    resolveFn(jsonResponse([]));
    await waitFor(() => expect(screen.queryByText('Loading quotations...')).not.toBeInTheDocument());
  });

  it('displays an API error state when the request fails', async () => {
    (fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(jsonResponse({ detail: 'Quotation service unavailable' }, false, 500));
    render(<BillingPage />);

    await waitFor(() => expect(screen.getByText('Quotation service unavailable')).toBeInTheDocument());
  });

  it('displays the backend-calculated total rather than a client-side recalculation', async () => {
    const skewedTotalQuote = { ...quotationResponse, total: 9999 };
    (fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(jsonResponse([skewedTotalQuote]));
    render(<BillingPage />);

    await waitFor(() => expect(screen.getAllByText(/9,999/).length).toBeGreaterThan(0));
  });
});
