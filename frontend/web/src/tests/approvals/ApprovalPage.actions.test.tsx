import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
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
};

const approvedApproval = { ...pendingApproval, status: 'Approved' };
const rejectedApproval = { ...pendingApproval, status: 'Rejected' };

function setupFetchMock(finalApproval: typeof pendingApproval) {
  let pendingCallCount = 0;
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    if (url.includes('/approve') && init?.method === 'POST') {
      return Promise.resolve(jsonResponse(finalApproval));
    }
    if (url.includes('/reject') && init?.method === 'POST') {
      return Promise.resolve(jsonResponse(finalApproval));
    }
    if (url.includes('/revision') && init?.method === 'POST') {
      return Promise.resolve(jsonResponse(finalApproval));
    }
    if (url.includes('/pending')) {
      pendingCallCount += 1;
      return Promise.resolve(jsonResponse([pendingCallCount === 1 ? pendingApproval : finalApproval]));
    }
    return Promise.resolve(jsonResponse([pendingApproval]));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('ApprovalPage decision actions', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('calls the approve service when a manager approves a proposal', async () => {
    const user = userEvent.setup();
    const fetchMock = setupFetchMock(approvedApproval);
    renderWithAuth(<ApprovalPage />);

    await waitFor(() => expect(screen.getAllByText(/Quotation quo-1/).length).toBeGreaterThan(0));
    await user.click(screen.getByRole('button', { name: /approve proposal/i }));
    await user.click(screen.getByRole('button', { name: /confirm decision/i }));

    await waitFor(() => {
      const approveCall = fetchMock.mock.calls.find(([url, init]) => url.includes('/approve') && init?.method === 'POST');
      expect(approveCall).toBeTruthy();
    });
    expect(await screen.findByText(/Decision recorded: Approved/)).toBeInTheDocument();
  });

  it('requires a reason before rejecting a proposal', async () => {
    const user = userEvent.setup();
    const fetchMock = setupFetchMock(rejectedApproval);
    renderWithAuth(<ApprovalPage />);

    await waitFor(() => expect(screen.getAllByText(/Quotation quo-1/).length).toBeGreaterThan(0));
    await user.click(screen.getByRole('button', { name: /^reject$/i }));

    const confirmButton = screen.getByRole('button', { name: /confirm decision/i });
    expect(confirmButton).toBeDisabled();

    const rejectCallsBefore = fetchMock.mock.calls.filter(([url, init]) => url.includes('/reject') && init?.method === 'POST').length;
    await user.type(screen.getByLabelText(/manager note/i), 'Not within policy');
    expect(confirmButton).toBeEnabled();
    await user.click(confirmButton);

    await waitFor(() => {
      const rejectCallsAfter = fetchMock.mock.calls.filter(([url, init]) => url.includes('/reject') && init?.method === 'POST').length;
      expect(rejectCallsAfter).toBe(rejectCallsBefore + 1);
    });
  });

  it('requires a reason before requesting a revision', async () => {
    const user = userEvent.setup();
    setupFetchMock({ ...pendingApproval, status: 'RevisionRequested' });
    renderWithAuth(<ApprovalPage />);

    await waitFor(() => expect(screen.getAllByText(/Quotation quo-1/).length).toBeGreaterThan(0));
    await user.click(screen.getByRole('button', { name: /request revision/i }));

    const confirmButton = screen.getByRole('button', { name: /confirm decision/i });
    expect(confirmButton).toBeDisabled();
  });

  it('displays a backend 409/400 error when the decision request fails', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((url: string, init?: RequestInit) => {
      if (url.includes('/approve') && init?.method === 'POST') {
        return Promise.resolve(jsonResponse({ detail: 'Quotation is no longer pending approval' }, false, 409));
      }
      return Promise.resolve(jsonResponse([pendingApproval]));
    });
    vi.stubGlobal('fetch', fetchMock);
    renderWithAuth(<ApprovalPage />);

    await waitFor(() => expect(screen.getAllByText(/Quotation quo-1/).length).toBeGreaterThan(0));
    await user.click(screen.getByRole('button', { name: /approve proposal/i }));
    await user.click(screen.getByRole('button', { name: /confirm decision/i }));

    expect(await screen.findByText('Quotation is no longer pending approval')).toBeInTheDocument();
  });
});
