import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import PetsPage from '../../features/pets/PetsPage';
import { renderWithAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const activePet = {
  id: 'pet-1', ownerId: 'own-1', name: 'Shadow', species: 'Dog',
  breed: 'Mixed', gender: 'Male', dateOfBirth: '2020-05-10',
  weight: 4.5, isArchived: false,
};

const archivedPet = {
  id: 'pet-2', ownerId: 'own-1', name: 'Milo', species: 'Cat',
  breed: 'Siamese', gender: 'Female', dateOfBirth: '2021-03-01',
  weight: 3.2, isArchived: true,
};

function stubFetch() {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';

    if (method === 'POST' && url.includes('/pets/pet-1/archive')) {
      return jsonResponse({ ...activePet, isArchived: true });
    }
    if (method === 'POST' && url.includes('/pets/pet-2/restore')) {
      return jsonResponse({ ...archivedPet, isArchived: false });
    }
    if (url.includes('/petowners') || url.includes('/owners')) {
      return jsonResponse([
        { id: 'own-1', fullName: 'Test Manager', email: 'manager@petcare.lk', phoneNumber: '011' },
      ]);
    }
    if (url.includes('/pets/owner/') || url.includes('/pets')) {
      return jsonResponse([activePet, archivedPet]);
    }
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

async function openStatusFilter(value: 'Active' | 'Archived') {
  await userEvent.selectOptions(screen.getByLabelText('Pet status'), value);
}

describe('PetsPage — archive/remove lifecycle', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('shows active pets by default with a Remove action (no Delete)', async () => {
    stubFetch();
    renderWithAuth(<PetsPage />, { role: 'PetOwner' });

    expect(await screen.findByText('Shadow')).toBeInTheDocument();
    // Archived pet is not in the active view.
    expect(screen.queryByText('Milo')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remove' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
  });

  it('requests pets with includeArchived so the archived view can be populated', async () => {
    const fetchMock = stubFetch();
    renderWithAuth(<PetsPage />, { role: 'PetOwner' });

    await screen.findByText('Shadow');
    expect(
      fetchMock.mock.calls.some(([input]) =>
        String(input).includes('includeArchived=true')),
    ).toBe(true);
  });

  it('Remove confirms with a history-preserved message, then archives the pet', async () => {
    const fetchMock = stubFetch();
    const confirmSpy = vi.fn((_message?: string) => true);
    vi.stubGlobal('confirm', confirmSpy);
    renderWithAuth(<PetsPage />, { role: 'PetOwner' });

    await userEvent.click(await screen.findByRole('button', { name: 'Remove' }));

    expect(confirmSpy).toHaveBeenCalledOnce();
    expect(confirmSpy.mock.calls[0][0]).toContain('Medical and billing history will be preserved');
    expect(
      fetchMock.mock.calls.some(([input, init]) =>
        String(input).includes('/pets/pet-1/archive') &&
        (init?.method ?? 'GET') === 'POST'),
    ).toBe(true);

    // The pet left the active view.
    expect(await screen.findByText(/removed from your active pets/)).toBeInTheDocument();
    expect(screen.queryByText('Shadow')).not.toBeInTheDocument();
  });

  it('does not archive when the confirmation is cancelled', async () => {
    const fetchMock = stubFetch();
    vi.stubGlobal('confirm', vi.fn(() => false));
    renderWithAuth(<PetsPage />, { role: 'PetOwner' });

    await userEvent.click(await screen.findByRole('button', { name: 'Remove' }));

    expect(
      fetchMock.mock.calls.some(([input]) =>
        String(input).includes('/archive')),
    ).toBe(false);
    expect(screen.getByText('Shadow')).toBeInTheDocument();
  });

  it('archived view shows the Archived badge and Restore action', async () => {
    stubFetch();
    renderWithAuth(<PetsPage />, { role: 'PetOwner' });
    await screen.findByText('Shadow');

    await openStatusFilter('Archived');

    expect(await screen.findByText('Milo')).toBeInTheDocument();
    // Badge + filter option both render "Archived".
    expect(screen.getAllByText('Archived').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByRole('button', { name: 'Restore' })).toBeInTheDocument();
    expect(screen.queryByText('Shadow')).not.toBeInTheDocument();
  });

  it('Restore returns the pet to the active list', async () => {
    const fetchMock = stubFetch();
    renderWithAuth(<PetsPage />, { role: 'PetOwner' });
    await screen.findByText('Shadow');
    await openStatusFilter('Archived');

    await userEvent.click(await screen.findByRole('button', { name: 'Restore' }));

    expect(
      fetchMock.mock.calls.some(([input, init]) =>
        String(input).includes('/pets/pet-2/restore') &&
        (init?.method ?? 'GET') === 'POST'),
    ).toBe(true);
    expect(await screen.findByText(/restored to your active pets/)).toBeInTheDocument();
    expect(screen.queryByText('Milo')).not.toBeInTheDocument();
  });

  it('shows a clear error when the archive request fails', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if ((init?.method ?? 'GET') === 'POST' && url.includes('/archive')) {
        return { ok: false, status: 400, text: async () => JSON.stringify({ message: 'Pet is already archived.' }) } as Response;
      }
      if (url.includes('/owners')) {
        return jsonResponse([{ id: 'own-1', fullName: 'Test Manager', email: 'manager@petcare.lk', phoneNumber: '011' }]);
      }
      return jsonResponse([activePet]);
    });
    vi.stubGlobal('fetch', fetchMock);
    vi.stubGlobal('confirm', vi.fn(() => true));
    renderWithAuth(<PetsPage />, { role: 'PetOwner' });

    await userEvent.click(await screen.findByRole('button', { name: 'Remove' }));

    expect(await screen.findByText(/Unable to remove the pet|API request failed/)).toBeInTheDocument();
    expect(screen.getByText('Shadow')).toBeInTheDocument();
  });
});
