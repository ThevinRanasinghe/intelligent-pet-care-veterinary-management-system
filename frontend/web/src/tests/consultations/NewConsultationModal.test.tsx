import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { NewConsultationModal } from '../../features/consultations/NewConsultationModal';
import { renderWithAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const ORG_ID = 'org-1';

const organizations = [
  { id: ORG_ID, name: 'Happy Paws', city: 'Colombo' },
];

/** Nine fixed slots — 10:00 is booked out. */
function dayAvailability(date: string) {
  const starts = ['09:00', '10:00', '11:00', '12:00', '13:00', '14:00', '15:00', '16:00', '17:00'];
  return {
    date,
    isPast: false,
    slots: starts.map((start, i) => ({
      start,
      end: `${String(9 + i + 1).padStart(2, '0')}:00`,
      available: start !== '10:00',
      availableVeterinarianIds: start === '10:00' ? [] : ['vet-1', 'vet-2'],
    })),
  };
}

/**
 * Month days for the requested (year, month): day 15 fully booked, days
 * before today marked past, everything else available.
 */
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
    const fullyBooked = !isPast && i + 1 === 15;
    return {
      date: `${year}-${m}-${d}`,
      isPast,
      fullyBooked,
      available: !isPast && !fullyBooked,
    };
  });
}

function stubFetch(createResponse?: { body: unknown; ok?: boolean; status?: number }) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';

    if (url.endsWith('/owners')) {
      return jsonResponse([{ id: 'own-1', fullName: 'Amal', email: 'manager@petcare.lk', phoneNumber: '077' }]);
    }
    if (url.includes('/pets/owner/')) {
      return jsonResponse([{ id: 'pet-1', ownerId: 'own-1', name: 'Shadow', species: 'Dog', breed: 'Mixed' }]);
    }
    if (url.endsWith('/pets')) {
      return jsonResponse([{ id: 'pet-1', ownerId: 'own-1', name: 'Shadow', species: 'Dog', breed: 'Mixed' }]);
    }
    if (url.endsWith('/lookups/organizations')) return jsonResponse(organizations);
    if (url.includes('/consultations/availability/month')) return jsonResponse(monthAvailability(url));
    if (url.includes('/consultations/availability')) {
      const date = new URL(url).searchParams.get('date') ?? '';
      return jsonResponse(dayAvailability(date));
    }
    if (url.includes('/consultations/validate-ownership')) {
      return jsonResponse({ isValid: true, message: '' });
    }
    if (method === 'POST' && url.endsWith('/consultations')) {
      return jsonResponse(
        createResponse?.body ?? { id: 'CON-0001', petId: 'pet-1', ownerId: 'own-1', organizationId: ORG_ID, status: 'Draft' },
        createResponse?.ok ?? true,
        createResponse?.status ?? 200,
      );
    }
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

/** iso string for day `day` of the month `delta` months from now. */
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
  // Wait for pets + organizations to finish loading.
  await screen.findByRole('option', { name: /Shadow/ });
  await screen.findByRole('option', { name: /Happy Paws/ });
}

async function selectClinic() {
  await userEvent.selectOptions(screen.getByLabelText('Clinic / Organization'), ORG_ID);
}

async function selectFutureDate() {
  // Next month is entirely in the future.
  await userEvent.click(screen.getByRole('button', { name: 'Next month' }));
  const iso = isoFor(1, 16);
  await userEvent.click(await screen.findByRole('button', { name: iso }));
  return iso;
}

describe('NewConsultationModal — fixed-slot booking', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('renders the organization picker and the booking calendar', async () => {
    stubFetch();
    await renderOpenModal();

    expect(screen.getByLabelText('Clinic / Organization')).toBeInTheDocument();

    await selectClinic();
    expect(await screen.findByTestId('booking-calendar')).toBeInTheDocument();
  });

  it('disables past dates', async () => {
    stubFetch();
    await renderOpenModal();
    await selectClinic();
    await screen.findByTestId('booking-calendar');

    // Every day of the previous month is in the past → all disabled.
    await userEvent.click(screen.getByRole('button', { name: 'Previous month' }));
    const calendar = await screen.findByTestId('booking-calendar');
    await waitFor(() => {
      const days = calendar.querySelectorAll<HTMLButtonElement>('button.booking-day');
      expect(days.length).toBeGreaterThan(0);
      expect(Array.from(days).every((d) => d.disabled)).toBe(true);
    });
  });

  it('disables and labels a fully booked date', async () => {
    stubFetch();
    await renderOpenModal();
    await selectClinic();

    // Day 15 of next month is marked fully booked.
    await userEvent.click(screen.getByRole('button', { name: 'Next month' }));
    const bookedDay = await screen.findByRole('button', { name: isoFor(1, 15) });

    expect(bookedDay).toBeDisabled();
    expect(bookedDay).toHaveTextContent(/fully booked/i);
  });

  it('renders the nine fixed one-hour slots for a chosen date', async () => {
    stubFetch();
    await renderOpenModal();
    await selectClinic();
    await selectFutureDate();

    const slots = await screen.findAllByRole('button', { name: /– (0[1-9]|1[0-2]):00 [AP]M/ });
    expect(slots).toHaveLength(9);
    expect(screen.getByRole('button', { name: '09:00 AM – 10:00 AM' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '05:00 PM – 06:00 PM' })).toBeInTheDocument();
  });

  it('disables a booked slot', async () => {
    stubFetch();
    await renderOpenModal();
    await selectClinic();
    await selectFutureDate();

    expect(await screen.findByRole('button', { name: '10:00 AM – 11:00 AM' })).toBeDisabled();
  });

  it('keeps submit disabled until pet, clinic, date and slot are chosen', async () => {
    stubFetch();
    await renderOpenModal();

    const submit = screen.getByRole('button', { name: 'Create Consultation' });
    expect(submit).toBeDisabled();

    await userEvent.selectOptions(screen.getByLabelText('Registered Pet'), 'pet-1');
    await selectClinic();
    expect(submit).toBeDisabled();

    await selectFutureDate();
    // Date picked, but no slot yet.
    expect(submit).toBeDisabled();

    await userEvent.click(await screen.findByRole('button', { name: '09:00 AM – 10:00 AM' }));
    expect(submit).toBeEnabled();
  });

  it('shows the "no longer available" message on a 409 conflict', async () => {
    stubFetch({
      body: { detail: 'This appointment slot is no longer available. Please select another time.' },
      ok: false,
      status: 409,
    });
    await renderOpenModal();

    await userEvent.selectOptions(screen.getByLabelText('Registered Pet'), 'pet-1');
    await selectClinic();
    await selectFutureDate();
    await userEvent.click(await screen.findByRole('button', { name: '09:00 AM – 10:00 AM' }));
    await userEvent.type(screen.getByLabelText(/symptoms/i), 'Limping');
    await userEvent.click(screen.getByRole('button', { name: 'Create Consultation' }));

    expect(await screen.findByText(/no longer available/i)).toBeInTheDocument();
  });
});
