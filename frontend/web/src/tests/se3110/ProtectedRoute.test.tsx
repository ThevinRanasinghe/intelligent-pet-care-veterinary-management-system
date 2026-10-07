// SE3110 TC-RE-003: authenticated user with the WRONG role must not see a
// manager-only route. Role gating lives in RoleRoute (ProtectedRoute only
// checks authentication) — the catalogue path is kept for both cases.
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../features/auth/AuthContext';
import RoleRoute from '../../features/auth/components/RoleRoute';
import { ProtectedRoute } from '../../features/auth/ProtectedRoute';
import { seedAuth } from '../testUtils';

const ManagerContent = () => <div data-testid="manager-content">manager dashboard</div>;
const Unauthorized = () => <div data-testid="unauthorized">unauthorized</div>;
const Login = () => <div data-testid="login">login page</div>;

function renderManagerAs(role: 'PetOwner' | 'Veterinarian' | 'ClinicManager' | null) {
  if (role) seedAuth(role);
  return render(
    <MemoryRouter initialEntries={['/manager']}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/unauthorized" element={<Unauthorized />} />
          <Route
            path="/manager"
            element={
              <ProtectedRoute>
                <RoleRoute allowedRoles={['ClinicManager', 'Administrator']}>
                  <ManagerContent />
                </RoleRoute>
              </ProtectedRoute>
            }
          />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('ProtectedRoute + RoleRoute (SE3110)', () => {
  beforeEach(() => { localStorage.clear(); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  // TC-RE-002: no token → /login (also covered by src/tests/auth/ProtectedRoute.test.tsx)
  it('redirects an unauthenticated user to /login', () => {
    renderManagerAs(null);
    expect(screen.getByTestId('login')).toBeInTheDocument();
    expect(screen.queryByTestId('manager-content')).not.toBeInTheDocument();
  });

  // TC-RE-003: wrong role is denied — manager content never renders.
  it('redirects a PetOwner away from a manager-only route', () => {
    renderManagerAs('PetOwner');
    expect(screen.getByTestId('unauthorized')).toBeInTheDocument();
    expect(screen.queryByTestId('manager-content')).not.toBeInTheDocument();
  });

  it('redirects a Veterinarian away from a manager-only route', () => {
    renderManagerAs('Veterinarian');
    expect(screen.getByTestId('unauthorized')).toBeInTheDocument();
    expect(screen.queryByTestId('manager-content')).not.toBeInTheDocument();
  });

  it('renders manager content for an allowed role', () => {
    renderManagerAs('ClinicManager');
    expect(screen.getByTestId('manager-content')).toBeInTheDocument();
    expect(screen.queryByTestId('unauthorized')).not.toBeInTheDocument();
  });
});
