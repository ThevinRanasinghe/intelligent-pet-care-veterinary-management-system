import { act, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../../lib/googleMaps', () => ({
  loadGoogleMaps: vi.fn(),
  MapsUnavailableError: class MapsUnavailableError extends Error {},
  directionsUrl: (lat: number, lng: number) =>
    `https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}`,
}));

import { loadGoogleMaps } from '../../lib/googleMaps';
import { NewConsultationModal } from '../../features/consultations/NewConsultationModal';
import { renderWithAuth } from '../testUtils';
import { makeFakeGoogle } from '../fakeGoogle';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const LOCATED_ORG = {
  id: 'org-1',
  name: 'Happy Paws',
  city: 'Colombo',
  address: '5 Galle Road',
  latitude: 6.93,
  longitude: 79.86,
};
const SECOND_ORG = {
  id: 'org-2',
  name: 'Kandy Pet Hospital',
  city: 'Kandy',
  address: '1 Hill Street',
  latitude: 7.29,
  longitude: 80.63,
};
const UNLOCATED_ORG = {
  id: 'org-3',
  name: 'No Pin Clinic',
  city: 'Galle',
  address: '2 Sea Street',
  latitude: null,
  longitude: null,
};

const organizations = [LOCATED_ORG, SECOND_ORG, UNLOCATED_ORG];

function dayAvailability(date: string) {
  const starts = ['09:00', '10:00', '11:00', '12:00', '13:00', '14:00', '15:00', '16:00', '17:00'];
  return {
    date,
    isPast: false,
    slots: starts.map((start, i) => ({
      start,
      end: `${String(9 + i + 1).padStart(2, '0')}:00`,
      available: true,
      availableVeterinarianIds: ['vet-1'],
    })),
  };
}

function monthAvailability(url: string) {
  const params = new URL(url).searchParams;
  const year = Number(params.get('year'));
  const month = Number(params.get('month'));
  const daysInMonth = new Date(year, month, 0).getDate();
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  return Array.from({ length: daysInMonth }, (_, i) => {
    const date = new Date(year, month - 1, i + 1);
    const m = String(month).padStart(2, '0');
    const d = String(i + 1).padStart(2, '0');
    const isPast = date < today;
    return {
      date: `${year}-${m}-${d}`,
      isPast,
      fullyBooked: false,
      available: !isPast,
    };
  });
}

function stubFetch(options?: { failOrganizations?: boolean }) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';

    if (url.endsWith('/owners')) {
      return jsonResponse([{ id: 'own-1', fullName: 'Amal', email: 'manager@petcare.lk', phoneNumber: '077' }]);
    }
    if (url.includes('/pets/owner/')) {
      return jsonResponse([{ id: 'pet-1', ownerId: 'own-1', name: 'Shadow', species: 'Dog', breed: 'Mixed' }]);
    }
    if (url.endsWith('/lookups/organizations')) {
      return options?.failOrganizations
        ? jsonResponse({ message: 'server error' }, false, 500)
        : jsonResponse(organizations);
    }
    if (url.includes('/consultations/availability/month')) return jsonResponse(monthAvailability(url));
    if (url.includes('/consultations/availability')) {
      const date = new URL(url).searchParams.get('date') ?? '';
      return jsonResponse(dayAvailability(date));
    }
    if (url.includes('/consultations/validate-ownership')) {
      return jsonResponse({ isValid: true, message: '' });
    }
    if (method === 'POST' && url.endsWith('/consultations')) {
      return jsonResponse({ id: 'CON-0001', petId: 'pet-1', ownerId: 'own-1', status: 'Draft' });
    }
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function isoFor(deltaMonths: number, day: number): string {
  const target = new Date();
  const shifted = new Date(target.getFullYear(), target.getMonth() + deltaMonths, 1);
  const m = String(shifted.getMonth() + 1).padStart(2, '0');
  const d = String(day).padStart(2, '0');
  return `${shifted.getFullYear()}-${m}-${d}`;
}

async function renderOpenModal() {
  renderWithAuth(
    <NewConsultationModal isOpen onClose={() => {}} onSubmit={() => {}} />,
    { role: 'PetOwner' },
  );
  await screen.findByRole('option', { name: /Shadow/ });
  await screen.findByRole('option', { name: /Happy Paws/ });
}

describe('NewConsultationModal — clinic map', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
    vi.mocked(loadGoogleMaps).mockReset();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it('creates one marker per clinic with coordinates and skips coordinate-less clinics', async () => {
    const fake = makeFakeGoogle();
    vi.mocked(loadGoogleMaps).mockResolvedValue(fake as never);
    stubFetch();
    await renderOpenModal();

    // Two of the three clinics have coordinates.
    expect(fake.__markers).toHaveLength(2);
    expect(fake.__markers[0].opts.position).toEqual({ lat: 6.93, lng: 79.86 });
    expect(fake.__markers[1].opts.position).toEqual({ lat: 7.29, lng: 80.63 });
  });

  it('fits the map bounds to all located clinics', async () => {
    const fake = makeFakeGoogle();
    vi.mocked(loadGoogleMaps).mockResolvedValue(fake as never);
    stubFetch();
    await renderOpenModal();

    expect(fake.__maps).toHaveLength(1);
    expect(fake.__maps[0].fitBoundsCalls).toHaveLength(1);
    const bounds = fake.__maps[0].fitBoundsCalls[0] as { points: unknown[] };
    expect(bounds.points).toEqual([
      { lat: 6.93, lng: 79.86 },
      { lat: 7.29, lng: 80.63 },
    ]);
  });

  it('shows the clinic name and address when a marker is clicked', async () => {
    const fake = makeFakeGoogle();
    vi.mocked(loadGoogleMaps).mockResolvedValue(fake as never);
    stubFetch();
    await renderOpenModal();

    act(() => fake.__markers[0].fire('click'));

    const infoCard = screen.getAllByTestId('clinic-card')[0];
    expect(within(infoCard).getByText(/Happy Paws/)).toBeInTheDocument();
    expect(within(infoCard).getByText('5 Galle Road')).toBeInTheDocument();
  });

  it('Select Clinic in the marker info card selects the clinic and the POST carries that organizationId', async () => {
    const fake = makeFakeGoogle();
    vi.mocked(loadGoogleMaps).mockResolvedValue(fake as never);
    const fetchMock = stubFetch();
    await renderOpenModal();

    act(() => fake.__markers[0].fire('click'));
    const infoCard = screen.getAllByTestId('clinic-card')[0];
    await userEvent.click(within(infoCard).getByRole('button', { name: 'Select Clinic' }));

    // Selected-clinic summary card confirms the choice.
    const selected = await screen.findByTestId('selected-clinic-card');
    expect(selected).toHaveTextContent('Happy Paws');
    expect(selected).toHaveTextContent('Organization ID: org-1');

    // Drive the remaining booking flow and submit.
    await userEvent.selectOptions(screen.getByLabelText('Registered Pet'), 'pet-1');
    await userEvent.click(screen.getByRole('button', { name: 'Next month' }));
    await userEvent.click(await screen.findByRole('button', { name: isoFor(1, 16) }));
    await userEvent.click(await screen.findByRole('button', { name: '09:00 AM – 10:00 AM' }));
    await userEvent.type(screen.getByLabelText(/symptoms/i), 'Limping');
    await userEvent.click(screen.getByRole('button', { name: 'Create Consultation' }));

    const postCall = fetchMock.mock.calls.find(
      ([url, init]) => String(url).endsWith('/consultations') && init?.method === 'POST',
    );
    expect(postCall).toBeDefined();
    expect(JSON.parse(String(postCall![1]?.body)).organizationId).toBe('org-1');
  });

  it('Get Directions links to the clinic coordinates', async () => {
    const fake = makeFakeGoogle();
    vi.mocked(loadGoogleMaps).mockResolvedValue(fake as never);
    stubFetch();
    await renderOpenModal();

    const links = screen.getAllByRole('link', { name: 'Get Directions' });
    expect(links[0]).toHaveAttribute('href', expect.stringContaining('destination=6.93,79.86'));
    expect(links[0]).toHaveAttribute('target', '_blank');
    // The coordinate-less clinic has no directions link.
    expect(links).toHaveLength(2);
  });

  it('shows an error when the organization lookup fails', async () => {
    vi.mocked(loadGoogleMaps).mockResolvedValue(makeFakeGoogle() as never);
    stubFetch({ failOrganizations: true });

    renderWithAuth(
      <NewConsultationModal isOpen onClose={() => {}} onSubmit={() => {}} />,
      { role: 'PetOwner' },
    );

    expect(await screen.findByText('Unable to load available clinics.')).toBeInTheDocument();
  });

  it('renders the map as the primary UI and never shows the fallback during normal operation', async () => {
    vi.mocked(loadGoogleMaps).mockResolvedValue(makeFakeGoogle() as never);
    stubFetch();
    await renderOpenModal();

    expect(await screen.findByTestId('clinic-map')).toBeInTheDocument();
    expect(screen.queryByTestId('clinic-map-fallback')).not.toBeInTheDocument();
    expect(screen.queryByText(/Unable to load the clinic map/)).not.toBeInTheDocument();
  });

  it('shows a clear error with Try Again when Google Maps fails to load, and retries successfully', async () => {
    vi.mocked(loadGoogleMaps).mockRejectedValueOnce(new Error('script failed to load'));
    stubFetch();
    await renderOpenModal();

    const fallback = await screen.findByTestId('clinic-map-fallback');
    expect(within(fallback).getByText('Unable to load the clinic map. Please try again.')).toBeInTheDocument();
    // The fallback list is labelled as a fallback, not the primary UI.
    expect(within(fallback).getByText('Fallback clinic list')).toBeInTheDocument();
    const cards = within(fallback).getAllByTestId('clinic-card');
    expect(cards).toHaveLength(3);

    // Selecting a clinic from the fallback list still drives booking state.
    await userEvent.click(within(cards[0]).getByRole('button', { name: 'Select Clinic' }));
    const selected = await screen.findByTestId('selected-clinic-card');
    expect(selected).toHaveTextContent('Happy Paws');
    expect(selected).toHaveTextContent('Organization ID: org-1');

    // Try Again reloads the map.
    vi.mocked(loadGoogleMaps).mockResolvedValue(makeFakeGoogle() as never);
    await userEvent.click(screen.getByRole('button', { name: 'Try Again' }));
    expect(await screen.findByTestId('clinic-map')).toBeInTheDocument();
    expect(screen.queryByTestId('clinic-map-fallback')).not.toBeInTheDocument();

    // The calendar step is available after selection.
    expect(await screen.findByTestId('booking-calendar')).toBeInTheDocument();
  });
});
