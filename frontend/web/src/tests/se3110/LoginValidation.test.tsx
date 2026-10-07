// SE3110 TC-RE-004: empty-field / malformed-email submission must show
// validation messages and must NOT hit the API (zero fetch calls).
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { LoginPage } from '../../features/auth/LoginPage';
import { AuthProvider } from '../../features/auth/AuthContext';

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('LoginPage form validation (SE3110 TC-RE-004)', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('shows required-field messages and makes no API call on empty submit', async () => {
    const user = userEvent.setup();
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;

    renderLogin();
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText('Email is required.')).toBeInTheDocument();
    expect(screen.getByText('Password is required.')).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('shows invalid-email message and makes no API call', async () => {
    const user = userEvent.setup();
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;

    renderLogin();
    await user.type(screen.getByRole('textbox', { name: /email/i }), 'not-an-email');
    await user.type(screen.getByPlaceholderText('Enter your password'), 'secret123');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText('Please enter a valid email address.')).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
