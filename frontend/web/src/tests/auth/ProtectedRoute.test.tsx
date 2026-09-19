import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../features/auth/AuthContext';
import { ProtectedRoute } from '../../features/auth/ProtectedRoute';
import { seedAuth } from '../testUtils';

const Protected = () => <div data-testid="protected">protected content</div>;
const Login = () => <div data-testid="login">login page</div>;

function renderWithRoutes(initial: string) {
  return render(
    <MemoryRouter initialEntries={[initial]}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/approvals" element={<ProtectedRoute><Protected /></ProtectedRoute>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('ProtectedRoute', () => {
  beforeEach(() => { localStorage.clear(); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('redirects to /login when the user is not authenticated', () => {
    renderWithRoutes('/approvals');
    expect(screen.getByTestId('login')).toBeInTheDocument();
    expect(screen.queryByTestId('protected')).not.toBeInTheDocument();
  });

  it('renders children when the user is authenticated', () => {
    seedAuth('ClinicManager');
    renderWithRoutes('/approvals');
    expect(screen.getByTestId('protected')).toBeInTheDocument();
    expect(screen.queryByTestId('login')).not.toBeInTheDocument();
  });
});
