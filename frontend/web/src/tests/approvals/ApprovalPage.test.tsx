import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApprovalPage } from '../../features/approvals/ApprovalPage';
import { renderWithAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return {
    ok,
    status,
    text: async () => (body === undefined ? '' : JSON.stringify(body)),
  } as Response;
}

const pendingApproval = {
  id: 'apr-1',
  quotationId: 'quo-1',
  quotationTotal: 4500,
  quotationBudget: 10000,
  status: 'Pending',
  reviewedBy: undefined,
  reviewedAt: undefined,
  comment: undefined,
};

const historyEntry = {
  id: 'hist-1',
  approvalId: 'apr-1',
  previousStatus: 'Pending',
  newStatus: 'Approved',
  changedBy: 'manager-1',
  reason: 'Looks good',
  changedAt: '2026-01-02T00:00:00',
};

describe('ApprovalPage component states', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders pending approvals when the API returns data', async () => {
    (fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(jsonResponse([pendingApproval]));
    renderWithAuth(<ApprovalPage />);

    await waitFor(() => expect(screen.getAllByText(/Quotation quo-1/).length).toBeGreaterThan(0));
    expect(screen.getAllByText('Pending').length).toBeGreaterThan(0);
  });

  it('displays the empty state when there are no pending approvals', async () => {
    (fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(jsonResponse([]));
    renderWithAuth(<ApprovalPage />);

    await waitFor(() => expect(screen.getByText('No pending approvals')).toBeInTheDocument());
  });

  it('displays the loading state while the request is pending', async () => {
    let resolveFn: (value: Response) => void = () => {};
    (fetch as unknown as ReturnType<typeof vi.fn>).mockImplementation(
      () => new Promise<Response>((resolve) => { resolveFn = resolve; }),
    );
    renderWithAuth(<ApprovalPage />);

    expect(screen.getByText('Loading approvals...')).toBeInTheDocument();
    resolveFn(jsonResponse([]));
    await waitFor(() => expect(screen.queryByText('Loading approvals...')).not.toBeInTheDocument());
  });

  it('displays an API error state when the request fails', async () => {
    (fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(jsonResponse({ detail: 'Approval service unavailable' }, false, 500));
    renderWithAuth(<ApprovalPage />);

    await waitFor(() => expect(screen.getByText('Approval service unavailable')).toBeInTheDocument());
  });

  it('renders approval history when requested', async () => {
    const user = userEvent.setup();
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
    fetchMock.mockImplementation((url: string) => {
      if (url.includes('/history')) return Promise.resolve(jsonResponse([historyEntry]));
      return Promise.resolve(jsonResponse([pendingApproval]));
    });
    renderWithAuth(<ApprovalPage />);

    await waitFor(() => expect(screen.getAllByText(/Quotation quo-1/).length).toBeGreaterThan(0));
    await user.click(screen.getByRole('button', { name: /view history/i }));

    expect(await screen.findByText(/Pending → Approved/)).toBeInTheDocument();
    expect(screen.getByText(/Looks good/)).toBeInTheDocument();
  });
});
