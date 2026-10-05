import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { RegisterPage } from '../../features/auth/RegisterPage';

function jsonResponse(body: unknown, ok = true, status = 201) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

function renderRegister(route = '/register') {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <RegisterPage />
    </MemoryRouter>,
  );
}

describe('RegisterPage', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('renders the registration type switcher with Pet Owner and Organization options', () => {
    renderRegister();
    expect(screen.getByRole('tab', { name: /pet owner/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /veterinary organization/i })).toBeInTheDocument();
  });

  it('shows pet owner registration form by default', () => {
    renderRegister();
    expect(screen.getByLabelText(/first name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/last name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/email address/i)).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Min 8 characters (mixed case, digit, symbol)')).toBeInTheDocument();
  });

  it('validates required fields on pet owner submit', async () => {
    const user = userEvent.setup();
    renderRegister();
    await user.click(screen.getByRole('button', { name: /create pet owner account/i }));
    await waitFor(() => expect(screen.getByText('First name is required.')).toBeInTheDocument());
    expect(screen.getByText('Last name is required.')).toBeInTheDocument();
    expect(screen.getByText('Email is required.')).toBeInTheDocument();
  });

  it('validates password complexity on pet owner form', async () => {
    const user = userEvent.setup();
    renderRegister();
    await user.type(screen.getByLabelText(/first name/i), 'John');
    await user.type(screen.getByLabelText(/last name/i), 'Doe');
    await user.type(screen.getByLabelText(/email address/i), 'john@example.com');
    await user.type(screen.getByPlaceholderText('Min 8 characters (mixed case, digit, symbol)'), 'weak');
    await user.click(screen.getByRole('button', { name: /create pet owner account/i }));
    await waitFor(() => expect(screen.getByText('Password must be at least 8 characters.')).toBeInTheDocument());
  });

  it('switches to organization wizard when clicked', async () => {
    const user = userEvent.setup();
    renderRegister();
    await user.click(screen.getByRole('tab', { name: /veterinary organization/i }));
    expect(screen.getByLabelText(/organization \/ clinic name/i)).toBeInTheDocument();
    expect(screen.getByText(/step 1 of 2/i)).toBeInTheDocument();
  });

  it('does not advance to step 2 with empty organization fields', async () => {
    const user = userEvent.setup();
    renderRegister();
    await user.click(screen.getByRole('tab', { name: /veterinary organization/i }));
    await user.click(screen.getByRole('button', { name: /continue to manager account/i }));
    await waitFor(() => expect(screen.getByText('Organization name is required.')).toBeInTheDocument());
  });
});
