import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ConsultationRequestsPage } from '../../features/consultations/ConsultationRequestsPage';
import { renderWithAuth } from '../testUtils';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const consultation = {
  id: 'CON-0001',
  petId: 'pet-1',
  ownerId: 'own-1',
  petName: 'Shadow',
  symptoms: 'Limping on left leg',
  urgency: 'Medium',
  preferredDate: '2026-10-05T00:00:00',
  preferredTime: '10:00:00',
  organizationId: 'org-1',
  organizationName: 'Happy Paws',
  budget: 5000,
  additionalNotes: null,
  status: 'Submitted',
  requestType: 'Initial',
  requestedByVeterinarianId: null,
  requestedByVeterinarianName: null,
  createdAt: '2026-09-26T00:00:00Z',
  updatedAt: '2026-09-26T00:00:00Z',
};

const plan = {
  source: 'agentic-ai',
  requestId: 'CON-0001',
  recommendedAppointment: {
    appointmentSlotId: 'slot-1',
    veterinarianId: 'vet-1',
    date: '2026-10-06',
    startTime: '10:00',
    endTime: '11:00',
    branch: 'Main',
    reason: 'Requested slot is free',
  },
  alternativeSlots: [
    {
      appointmentSlotId: 'slot-2',
      veterinarianId: 'vet-2',
      date: '2026-10-05',
      startTime: '14:00',
      endTime: '15:00',
      branch: 'Main',
      reason: 'Next free slot',
    },
  ],
  quotationProposal: {
    budget: 5000,
    items: [
      { category: 'Consultation', description: 'Standard consultation', quantity: 1, unitPrice: 2500, reason: 'Initial visit' },
    ],
    estimatedSubtotal: 2500,
    estimatedTotal: 2500,
    withinBudget: true,
  },
  validationSummary: {
    slotFound: true,
    veterinarianAvailable: true,
    noKnownConflict: true,
    withinRequestedTime: true,
    withinBudget: true,
  },
  confidence: 'High',
  planningNotes: 'Requested slot is free for Dr. Perera',
  disclaimer: 'AI-generated scheduling assistance — final availability is determined by the PetCare scheduling system.',
};

function stubFetch(
  planResponse: { body: unknown; ok?: boolean; status?: number } | undefined,
) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = (init?.method ?? 'GET').toUpperCase();
    if (url.endsWith('/owners')) return jsonResponse([{ id: 'own-1', fullName: 'Amal', email: 'amal@x.lk', phoneNumber: '077' }]);
    if (url.endsWith('/pets')) return jsonResponse([{ id: 'pet-1', ownerId: 'own-1', name: 'Shadow', species: 'Dog', breed: 'Mixed' }]);
    if (url.endsWith('/manager/veterinarians')) return jsonResponse([{ id: 'vet-1', name: 'Dr. Perera', specialisation: 'General' }]);
    if (url.includes('/scheduling-plan')) {
      return planResponse === undefined
        ? jsonResponse(plan)
        : jsonResponse(planResponse.body, planResponse.ok ?? true, planResponse.status ?? 200);
    }
    if (url.includes('/availability')) return jsonResponse({ date: '2026-10-05', slots: [] });
    if (url.includes('/history')) return jsonResponse([]);
    if (method === 'POST' && url.includes('/assign')) {
      return jsonResponse({ id: 'appt-1', petId: 'pet-1', veterinarianId: 'vet-1', status: 'Confirmed' });
    }
    if (url.includes('/consultations/')) return jsonResponse(consultation);
    if (url.endsWith('/consultations')) return jsonResponse([consultation]);
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

async function openAssignPanel() {
  await userEvent.click(await screen.findByRole('button', { name: /view details/i }));
  await userEvent.click(await screen.findByRole('button', { name: 'Assign Veterinarian' }));
}

describe('ConsultationRequestsPage — AI scheduling analysis', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('shows the AI Scheduling Analysis button to a ClinicManager inside the assign panel', async () => {
    stubFetch(undefined);
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager' });
    await openAssignPanel();

    expect(await screen.findByRole('button', { name: 'AI Scheduling Analysis' })).toBeInTheDocument();
  });

  it('does NOT show scheduling analysis to a Veterinarian (no assign panel)', async () => {
    stubFetch(undefined);
    renderWithAuth(<ConsultationRequestsPage />, { role: 'Veterinarian' });
    await userEvent.click(await screen.findByRole('button', { name: /view details/i }));

    expect(screen.queryByRole('button', { name: 'Assign Veterinarian' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'AI Scheduling Analysis' })).not.toBeInTheDocument();
  });

  it('renders the advisory plan without auto-selecting vet/date/slot', async () => {
    const fetchMock = stubFetch(undefined);
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager' });
    await openAssignPanel();

    await userEvent.click(await screen.findByRole('button', { name: 'AI Scheduling Analysis' }));

    expect(await screen.findByText('AI Scheduling Analysis', { selector: 'div' })).toBeInTheDocument();
    expect(screen.getByText(/Confidence: High/)).toBeInTheDocument();
    expect(screen.getByText(/Requested slot is free for Dr. Perera/)).toBeInTheDocument();
    expect(screen.getByText(/Slot found: Yes/)).toBeInTheDocument();
    expect(screen.getByText(/Estimated total: 2500/)).toBeInTheDocument();
    expect(screen.getByText(/Advisory only; nothing is booked automatically/i)).toBeInTheDocument();

    // The advisory call hit the scheduling-plan endpoint — and nothing was posted.
    const planCall = fetchMock.mock.calls.find(([url]) =>
      String(url).includes('/consultations/CON-0001/scheduling-plan'),
    );
    expect(planCall).toBeTruthy();
    expect(
      fetchMock.mock.calls.filter(
        ([url, init]) => (init as RequestInit)?.method && (init as RequestInit).method !== 'GET' && String(url).includes('/consultations'),
      ),
    ).toHaveLength(0);

    // AI must NOT auto-fill the manual scheduling controls: vet stays
    // empty, and the date keeps the owner's preferred-date prefill
    // (2026-10-05) rather than the AI-suggested 2026-10-06.
    expect(screen.getByLabelText('Veterinarian')).toHaveValue('');
    expect(screen.getByLabelText('Appointment date')).toHaveValue('2026-10-05');
    expect(screen.getByRole('button', { name: 'Confirm assignment' })).toBeInTheDocument();
  });

  it('shows a safe unavailable message when the agent cannot produce a plan', async () => {
    stubFetch({ body: { source: 'unavailable', requestId: 'CON-0001', recommendedAppointment: null, alternativeSlots: [], quotationProposal: null, validationSummary: { slotFound: false, veterinarianAvailable: false, noKnownConflict: false, withinRequestedTime: false, withinBudget: false }, confidence: '', planningNotes: '', disclaimer: 'unavailable' } });
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager' });
    await openAssignPanel();

    await userEvent.click(await screen.findByRole('button', { name: 'AI Scheduling Analysis' }));

    expect(await screen.findByText(/currently unavailable/)).toBeInTheDocument();
    // Manual scheduling controls are still usable.
    expect(screen.getByRole('button', { name: 'Confirm assignment' })).toBeInTheDocument();
  });

  it('shows a safe message when the plan call fails', async () => {
    stubFetch({ body: { message: 'boom' }, ok: false, status: 500 });
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager' });
    await openAssignPanel();

    await userEvent.click(await screen.findByRole('button', { name: 'AI Scheduling Analysis' }));

    expect(await screen.findByText(/currently unavailable/)).toBeInTheDocument();
  });
});
