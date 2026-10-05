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

const analysis = {
  source: 'agentic-ai',
  consultationRequestId: 'CON-0001',
  priority: 'High',
  consultationType: 'Urgent',
  keyConcerns: [{ concern: 'Possible fracture', reason: 'Non-weight-bearing lameness' }],
  recommendedChecks: ['Physical orthopedic exam', 'Radiographs'],
  suggestedNextStep: 'Schedule an orthopedic examination',
  disclaimer: 'Preliminary AI consultation assessment — requires veterinary review and confirmation.',
};

function stubFetch(
  analysisResponse: { body: unknown; ok?: boolean; status?: number } | undefined,
  item: Record<string, unknown> = consultation,
) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.endsWith('/owners')) return jsonResponse([{ id: 'own-1', fullName: 'Amal', email: 'amal@x.lk', phoneNumber: '077' }]);
    if (url.endsWith('/pets')) return jsonResponse([{ id: 'pet-1', ownerId: 'own-1', name: 'Shadow', species: 'Dog', breed: 'Mixed' }]);
    if (url.endsWith('/manager/veterinarians')) return jsonResponse([]);
    if (url.includes('/analysis')) {
      return analysisResponse === undefined
        ? jsonResponse(analysis)
        : jsonResponse(analysisResponse.body, analysisResponse.ok ?? true, analysisResponse.status ?? 200);
    }
    if (url.includes('/history')) return jsonResponse([]);
    if (url.includes('/consultations/')) return jsonResponse(item);
    if (url.endsWith('/consultations')) return jsonResponse([item]);
    return jsonResponse([]);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

async function openDetails() {
  await userEvent.click(await screen.findByRole('button', { name: /view details/i }));
}

describe('ConsultationRequestsPage — AI consultation analysis', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });

  it('shows the AI analysis button to a ClinicManager on a Submitted request', async () => {
    stubFetch(undefined);
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager' });
    await openDetails();

    expect(await screen.findByRole('button', { name: 'AI Consultation Analysis' })).toBeInTheDocument();
  });

  it('does NOT show the AI analysis button to a Veterinarian or PetOwner', async () => {
    stubFetch(undefined);
    renderWithAuth(<ConsultationRequestsPage />, { role: 'Veterinarian' });
    await openDetails();

    expect(screen.queryByRole('button', { name: 'AI Consultation Analysis' })).not.toBeInTheDocument();
  });

  it('renders the advisory analysis panel on success without changing the request', async () => {
    const fetchMock = stubFetch(undefined);
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager' });
    await openDetails();

    await userEvent.click(await screen.findByRole('button', { name: 'AI Consultation Analysis' }));

    expect(await screen.findByText('AI Consultation Analysis', { selector: 'div' })).toBeInTheDocument();
    expect(screen.getByText(/Priority: High/)).toBeInTheDocument();
    expect(screen.getByText(/Type: Urgent/)).toBeInTheDocument();
    expect(screen.getByText(/Possible fracture/)).toBeInTheDocument();
    expect(screen.getByText(/Schedule an orthopedic examination/)).toBeInTheDocument();
    expect(screen.getByText(/manager\/veterinarian review required/i)).toBeInTheDocument();

    // The analysis hit the advisory endpoint — and nothing else mutated.
    const analysisCall = fetchMock.mock.calls.find(([url]) =>
      String(url).includes('/consultations/CON-0001/analysis'),
    );
    expect(analysisCall).toBeTruthy();
    expect(
      fetchMock.mock.calls.filter(
        ([url, init]) => (init as RequestInit)?.method && (init as RequestInit).method !== 'GET' && String(url).includes('/consultations'),
      ),
    ).toHaveLength(0);

    // Existing workflow controls are still intact.
    expect(screen.getByRole('button', { name: 'Assign Veterinarian' })).toBeInTheDocument();
  });

  it('shows a safe unavailable message when the agent cannot produce an assessment', async () => {
    stubFetch({ body: { source: 'unavailable', consultationRequestId: 'CON-0001', priority: '', consultationType: '', keyConcerns: [], recommendedChecks: [], suggestedNextStep: '', disclaimer: 'unavailable' } });
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager' });
    await openDetails();

    await userEvent.click(await screen.findByRole('button', { name: 'AI Consultation Analysis' }));

    expect(await screen.findByText(/currently unavailable/)).toBeInTheDocument();
    // Workflow still usable — Assign button remains.
    expect(screen.getByRole('button', { name: 'Assign Veterinarian' })).toBeInTheDocument();
  });

  it('shows a safe message when the analysis call fails', async () => {
    stubFetch({ body: { message: 'boom' }, ok: false, status: 500 });
    renderWithAuth(<ConsultationRequestsPage />, { role: 'ClinicManager' });
    await openDetails();

    await userEvent.click(await screen.findByRole('button', { name: 'AI Consultation Analysis' }));

    expect(await screen.findByText(/currently unavailable/)).toBeInTheDocument();
  });
});
