import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { LoginPage } from '../../features/auth/LoginPage';
import { AuthProvider } from '../../features/auth/AuthContext';
import { getStoredAuth } from '../../utils/authStorage';
import { seedAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const loginResponse = {
  token: 'jwt-123',
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  userId: 'u-1',
  email: 'manager@petcare.lk',
  name: 'Clinic Manager',
  role: 'ClinicManager',
};

function renderLogin(route = '/login') {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/" element={<div data-testid="dashboard">Dashboard</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('LoginPage', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('renders email, password and sign-in button', () => {
    renderLogin();
    expect(screen.getByRole('textbox', { name: /email/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();
  });

  it('calls the API, stores the session, and redirects to / on success', async () => {
    const user = userEvent.setup();
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
    fetchMock.mockResolvedValueOnce(jsonResponse(loginResponse));

    renderLogin();
    await user.type(screen.getByRole('textbox', { name: /email/i }), 'manager@petcare.lk');
    await user.type(screen.getByLabelText(/password/i), 'password');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    const [url] = fetchMock.mock.calls[0];
    expect(url).toContain('/auth/login');

    await waitFor(() => expect(getStoredAuth()?.token).toBe('jwt-123'));
    expect(screen.getByTestId('dashboard')).toBeInTheDocument();
  });

  it('displays an error message and returns to the sign-in button on invalid credentials', async () => {
    const user = userEvent.setup();
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
    fetchMock.mockResolvedValueOnce(jsonResponse({ detail: 'Invalid credentials' }, false, 401));

    renderLogin();
    await user.type(screen.getByRole('textbox', { name: /email/i }), 'manager@petcare.lk');
    await user.type(screen.getByLabelText(/password/i), 'wrong');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => expect(screen.getByText('Invalid credentials')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();
    expect(getStoredAuth()).toBeNull();
  });

  it('shows a loading state while submitting', async () => {
    const user = userEvent.setup();
    let resolve: (value: Response) => void = () => {};
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
    fetchMock.mockImplementation(() => new Promise<Response>((res) => { resolve = res; }));

    renderLogin();
    await user.type(screen.getByRole('textbox', { name: /email/i }), 'manager@petcare.lk');
    await user.type(screen.getByLabelText(/password/i), 'password');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => expect(screen.getByRole('button', { name: /signing in/i })).toBeDisabled());
    resolve(jsonResponse(loginResponse));

    await waitFor(() => expect(screen.getByTestId('dashboard')).toBeInTheDocument());
  });

  it('redirects to / when the user is already authenticated', () => {
    seedAuth('ClinicManager');
    renderLogin();
    expect(screen.getByTestId('dashboard')).toBeInTheDocument();
  });
});
