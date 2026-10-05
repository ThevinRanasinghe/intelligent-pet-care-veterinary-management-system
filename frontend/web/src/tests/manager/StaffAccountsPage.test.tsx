import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { render } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { StaffAccountsPage } from '../../features/manager/StaffAccountsPage';
import { AuthProvider } from '../../features/auth/AuthContext';
import RoleRoute from '../../features/auth/components/RoleRoute';
import { UnauthorizedPage } from '../../features/shared/UnauthorizedPage';
import { renderWithAuth, seedAuth } from '../testUtils';
import type { Role } from '../../types/domain';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

function stubStaffFetch(createResponse?: { body: unknown; ok?: boolean; status?: number }) {
  const fetchMock = vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
    if (init?.method === 'POST') {
      return jsonResponse(createResponse?.body ?? {}, createResponse?.ok ?? true, createResponse?.status ?? 201);
    }
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

/** Renders the page through the same RoleRoute guard used in AppRoutes. */
function renderStaffRoute(role: Role) {
  seedAuth(role);
  return render(
    <MemoryRouter initialEntries={['/manager/staff']}>
      <AuthProvider>
        <Routes>
          <Route path="/manager/staff" element={
            <RoleRoute allowedRoles={['ClinicManager']}><StaffAccountsPage /></RoleRoute>
          } />
          <Route path="/unauthorized" element={<UnauthorizedPage />} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('StaffAccountsPage — ClinicManager staff management', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('shows the staff management section for ClinicManager', () => {
    stubStaffFetch();
    renderWithAuth(<StaffAccountsPage />, { role: 'ClinicManager' });

    expect(screen.getByText('Staff Account Management')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Create Veterinarian' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Create Inventory Officer' })).toBeInTheDocument();
  });

  it('route-gates non-ClinicManager users out of the page', () => {
    stubStaffFetch();
    renderStaffRoute('Administrator');

    expect(screen.queryByText('Staff Account Management')).not.toBeInTheDocument();
    expect(screen.getByText(/access denied/i)).toBeInTheDocument();
  });

  it('opens the create form without any organization selector', async () => {
    stubStaffFetch();
    renderWithAuth(<StaffAccountsPage />, { role: 'ClinicManager' });

    await userEvent.click(screen.getByRole('button', { name: 'Create Veterinarian' }));

    expect(await screen.findByLabelText('First name')).toBeInTheDocument();
    expect(screen.getByText('Account will be created for your organization.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Organization')).not.toBeInTheDocument();
    expect(document.querySelector('select')).toBeNull();
  });

  it('submits to the manager-scoped endpoint and shows the temporary password once', async () => {
    const fetchMock = stubStaffFetch({
      body: {
        id: 'u-new', firstName: 'Nimal', lastName: 'Perera', name: 'Nimal Perera',
        email: 'nimal@vet.lk', role: 'Veterinarian', active: true, mustChangePassword: true,
        organizationId: 'org-1', organizationName: 'Test Org', createdAt: '2026-09-26T00:00:00Z',
        temporaryPassword: 'TmpPass123!xx',
      },
    });
    renderWithAuth(<StaffAccountsPage />, { role: 'ClinicManager' });

    await userEvent.click(screen.getByRole('button', { name: 'Create Veterinarian' }));
    await userEvent.type(screen.getByLabelText('First name'), 'Nimal');
    await userEvent.type(screen.getByLabelText('Last name'), 'Perera');
    await userEvent.type(screen.getByLabelText('Email'), 'nimal@vet.lk');
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText('TmpPass123!xx')).toBeInTheDocument();
    const call = fetchMock.mock.calls.find(([u, i]) => String(u).includes('/manager/users/veterinarians') && (i as RequestInit)?.method === 'POST');
    expect(call).toBeTruthy();
    expect(JSON.parse(String((call![1] as RequestInit).body))).not.toHaveProperty('organizationId');
  });

  it('shows the API error message on failure', async () => {
    stubStaffFetch({ body: { message: 'An account with this email already exists.' }, ok: false, status: 400 });
    renderWithAuth(<StaffAccountsPage />, { role: 'ClinicManager' });

    await userEvent.click(screen.getByRole('button', { name: 'Create Inventory Officer' }));
    await userEvent.type(screen.getByLabelText('First name'), 'A');
    await userEvent.type(screen.getByLabelText('Last name'), 'B');
    await userEvent.type(screen.getByLabelText('Email'), 'dup@vet.lk');
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText(/email already exists/i)).toBeInTheDocument();
  });
});
