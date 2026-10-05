import { fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TreatmentPage } from '../../features/treatment/TreatmentPage';
import { renderWithAuth } from '../testUtils';

const exam = {
    id: 'ex-1',
    petId: 'pet-1',
    veterinarianId: 'vet-1',
    symptoms: 'Vomiting and lethargy',
    notes: '',
    examinationDate: '2026-10-01T10:00:00',
    createdAt: '2026-10-01T10:00:00',
};

const aiRecommendation = {
    suspectedCondition: 'Acute gastroenteritis',
    recommendedSeverity: 'High',
    rationale: 'AI rationale text',
    recommendedProcedures: ['Fluid therapy'],
    suggestedMedicines: [
        { medicineId: 'med-1', medicineName: 'Amoxicillin', suggestedDosage: '250mg twice daily', suggestedDurationDays: 7 },
        { medicineId: null, medicineName: 'Special Compound', suggestedDosage: 'topical', suggestedDurationDays: 5 },
    ],
    precautionaryNotes: ['Monitor hydration'],
    source: 'agentic-ai',
};

function jsonResponse(body: unknown, status = 200): Response {
    return { ok: status >= 200 && status < 300, status, text: async () => JSON.stringify(body) } as Response;
}

/** Routes fetch calls by path; the recommendations handler is injectable per test. */
function stubFetch(recommendations: () => Response | Promise<Response>) {
    return vi.fn(async (input: RequestInfo | URL) => {
        const url = typeof input === 'string' ? input : input.toString();
        if (url.includes('/recommendations')) return recommendations();
        if (url.includes('/examinations')) return jsonResponse([exam]);
        if (url.includes('/lookups/pets')) return jsonResponse([{ id: 'pet-1', name: 'Buddy', species: 'Dog', breed: 'Labrador' }]);
        if (url.includes('/lookups/medicines')) return jsonResponse([]);
        if (url.includes('/diagnoses')) return jsonResponse([]);
        return jsonResponse([]);
    });
}

describe('TreatmentPage AI recommendations', () => {
    beforeEach(() => {
        vi.stubGlobal('fetch', stubFetch(() => jsonResponse(aiRecommendation)));
    });
    afterEach(() => {
        vi.unstubAllGlobals();
        localStorage.clear();
    });

    it('shows the AI Assist action and renders the recommendation with advisory banner', async () => {
        renderWithAuth(<TreatmentPage />, { role: 'Veterinarian' });

        const assist = await screen.findByRole('button', { name: /AI Assist/i });
        fireEvent.click(assist);

        expect(await screen.findByText('Acute gastroenteritis')).toBeInTheDocument();
        expect(screen.getByText(/requires veterinarian review/i)).toBeInTheDocument();
        expect(screen.getByText(/High Severity/i)).toBeInTheDocument();
        expect(screen.getByText('Amoxicillin')).toBeInTheDocument();
        expect(screen.getByText('Special Compound')).toBeInTheDocument();
    });

    it('marks unresolved medicine names as not matched to formulary', async () => {
        renderWithAuth(<TreatmentPage />, { role: 'Veterinarian' });

        fireEvent.click(await screen.findByRole('button', { name: /AI Assist/i }));

        expect(await screen.findByText(/not matched to formulary/i)).toBeInTheDocument();
    });

    it('Apply prefills the diagnosis form without persisting anything', async () => {
        renderWithAuth(<TreatmentPage />, { role: 'Veterinarian' });

        // Expand the row so expandedId is set (Apply requires it).
        fireEvent.click(await screen.findByText('Vomiting and lethargy'));
        await waitFor(() => expect(screen.getByRole('button', { name: /Suggest Recommendations/i })).toBeInTheDocument());
        fireEvent.click(screen.getByRole('button', { name: /Suggest Recommendations/i }));
        fireEvent.click(await screen.findByRole('button', { name: /Apply Diagnosis to Record/i }));

        // Diagnosis modal opens prefilled; only a user save would persist.
        const conditionInput = await screen.findByDisplayValue('Acute gastroenteritis');
        expect(conditionInput).toBeInTheDocument();
    });

    it('shows safe unavailable state and hides Apply when the agent reports unavailable', async () => {
        vi.stubGlobal('fetch', stubFetch(() => jsonResponse({
            ...aiRecommendation,
            source: 'unavailable',
            suspectedCondition: 'AI recommendation unavailable',
            rationale: 'The AI advisory service is currently unavailable. Proceed with a manual clinical assessment.',
            suggestedMedicines: [],
            recommendedProcedures: [],
        })));
        renderWithAuth(<TreatmentPage />, { role: 'Veterinarian' });

        fireEvent.click(await screen.findByRole('button', { name: /AI Assist/i }));

        expect(await screen.findByText(/currently unavailable/i)).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Apply Diagnosis to Record/i })).not.toBeInTheDocument();
        // Vet can still act manually.
        fireEvent.click(await screen.findByText('Vomiting and lethargy'));
        expect(await screen.findByRole('button', { name: /Record Diagnosis/i })).toBeInTheDocument();
    });

    it('shows safe error text when the request fails', async () => {
        vi.stubGlobal('fetch', stubFetch(() => { throw new Error('network down'); }));
        renderWithAuth(<TreatmentPage />, { role: 'Veterinarian' });

        fireEvent.click(await screen.findByRole('button', { name: /AI Assist/i }));

        expect(await screen.findByText(/currently unavailable/i)).toBeInTheDocument();
    });
});
