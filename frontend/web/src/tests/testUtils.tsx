import type { ReactElement } from 'react';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AuthProvider } from '../features/auth/AuthContext';
import { setStoredAuth, type StoredAuth } from '../utils/authStorage';
import type { Role } from '../types/domain';

/** Seeds localStorage with a valid, non-expired session for the given role. */
export function seedAuth(role: Role = 'ClinicManager'): StoredAuth {
  const auth: StoredAuth = {
    token: 'test-token',
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    userId: 'user-1',
    email: 'manager@petcare.lk',
    name: 'Test Manager',
    role,
  };
  setStoredAuth(auth);
  return auth;
}

/** Renders a component as an already-authenticated user of the given role (defaults to ClinicManager). */
export function renderWithAuth(
  ui: ReactElement,
  { role = 'ClinicManager' as Role, route = '/' }: { role?: Role; route?: string } = {},
) {
  seedAuth(role);
  return render(
    <MemoryRouter initialEntries={[route]}>
      <AuthProvider>{ui}</AuthProvider>
    </MemoryRouter>,
  );
}
