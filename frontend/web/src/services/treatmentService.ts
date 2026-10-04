import { apiRequest } from './api';
import type {
    Examination,
    Diagnosis,
    TreatmentRecord,
    Prescription,
    DiagnosisSeverity,
    TreatmentStatus,
} from '../types/domain';


// ---- Request DTOs (shapes sent to the backend) ----

export interface CreateExaminationInput {
    petId: string;
    veterinarianId: string;
    consultationRequestId?: string | null;
    /** The appointment this examination completes (vet appointment flow). */
    appointmentId?: string | null;
    /** The veterinarian's fee — billed onto the appointment's quotation. */
    veterinarianCharge?: number;
    symptoms: string;
    notes: string;
    examinationDate: string;
}

export interface UpdateExaminationInput {
    symptoms: string;
    notes: string;
    examinationDate: string;
}

export interface CreateDiagnosisInput {
    examinationId: string;
    conditionName: string;
    description: string;
    severity: DiagnosisSeverity;
}

export interface CreateTreatmentRecordInput {
    diagnosisId: string;
    procedureName: string;
    notes: string;
}

/** One medicine line inside a vet's medicine request. */
export interface CreatePrescriptionItemInput {
    medicineId: string;
    dosage: string;
    durationDays: number;
    /** Units requested from inventory (≥ 1); defaults to 1 server-side. */
    quantity?: number;
    frequency?: string;
    /** Route/method of administration recorded by the vet, e.g. "Oral". */
    route?: string;
    instructions?: string;
}

/** One medicine request can hold 1–10 different medicines. */
export interface CreatePrescriptionInput {
    treatmentRecordId: string;
    items: CreatePrescriptionItemInput[];
}

export interface UpdateDiagnosisInput {
    conditionName: string;
    description: string;
    severity: DiagnosisSeverity;
}

export interface UpdateTreatmentRecordInput {
    procedureName: string;
    notes: string;
}
// ---- Examinations ----

export function getExaminations() {
    return apiRequest<Examination[]>('/examinations');
}

export function getExaminationById(id: string) {
    return apiRequest<Examination>(`/examinations/${id}`);
}

export function getExaminationsByPet(petId: string) {
    return apiRequest<Examination[]>(`/examinations/pet/${petId}`);
}

export function createExamination(input: CreateExaminationInput) {
    return apiRequest<Examination>('/examinations', {
        method: 'POST',
        body: JSON.stringify(input),
    });
}

export function updateExamination(id: string, input: UpdateExaminationInput) {
    return apiRequest<void>(`/examinations/${id}`, {
        method: 'PUT',
        body: JSON.stringify(input),
    });
}

export function deleteExamination(id: string) {
    return apiRequest<void>(`/examinations/${id}`, { method: 'DELETE' });
}

// ---- Diagnoses ----

export function getAllDiagnoses() {
    return apiRequest<Diagnosis[]>('/diagnoses');
}

export function getDiagnosisByExamination(examinationId: string) {
    return apiRequest<Diagnosis[]>(`/diagnoses/examination/${examinationId}`);
}

export function createDiagnosis(input: CreateDiagnosisInput) {
    return apiRequest<Diagnosis>('/diagnoses', {
        method: 'POST',
        body: JSON.stringify(input),
    });
}

// ---- Treatment Records ----

export function getAllTreatmentRecords() {
    return apiRequest<TreatmentRecord[]>('/treatmentrecords');
}

export function getTreatmentRecordsByDiagnosis(diagnosisId: string) {
    return apiRequest<TreatmentRecord[]>(`/treatmentrecords/diagnosis/${diagnosisId}`);
}

export function createTreatmentRecord(input: CreateTreatmentRecordInput) {
    return apiRequest<TreatmentRecord>('/treatmentrecords', {
        method: 'POST',
        body: JSON.stringify(input),
    });
}

export function updateTreatmentStatus(id: string, status: TreatmentStatus) {
    return apiRequest<TreatmentRecord>(`/treatmentrecords/${id}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ status }),
    });
}

// ---- Prescriptions ----

export function getAllPrescriptions() {
    return apiRequest<Prescription[]>('/prescriptions');
}

export function getPrescriptionsByTreatment(treatmentRecordId: string) {
    return apiRequest<Prescription[]>(`/prescriptions/treatment/${treatmentRecordId}`);
}

/** Creates a medicine request — the response contains one row per item. */
export function createPrescription(input: CreatePrescriptionInput) {
    return apiRequest<Prescription[]>('/prescriptions', {
        method: 'POST',
        body: JSON.stringify(input),
    });
}

export function updateDiagnosis(id: string, input: UpdateDiagnosisInput) {
    return apiRequest<void>(`/diagnoses/${id}`, { method: 'PUT', body: JSON.stringify(input) });
}

export function deleteDiagnosis(id: string) {
    return apiRequest<void>(`/diagnoses/${id}`, { method: 'DELETE' });
}

export function updateTreatmentRecord(id: string, input: UpdateTreatmentRecordInput) {
    return apiRequest<void>(`/treatmentrecords/${id}`, { method: 'PUT', body: JSON.stringify(input) });
}

export function deleteTreatmentRecord(id: string) {
    return apiRequest<void>(`/treatmentrecords/${id}`, { method: 'DELETE' });
}

export function deletePrescription(id: string) {
    return apiRequest<void>(`/prescriptions/${id}`, { method: 'DELETE' });
}

// ---- Intelligent Treatment Recommendations (Business-Specific Operation) ----

export interface RecommendedMedicine {
    /** Null when the AI suggestion could not be uniquely matched to a formulary record — advisory name only. */
    medicineId: string | null;
    medicineName: string;
    suggestedDosage: string;
    suggestedDurationDays: number;
}

export interface TreatmentRecommendation {
    suspectedCondition: string;
    recommendedSeverity: DiagnosisSeverity;
    rationale: string;
    recommendedProcedures: string[];
    suggestedMedicines: RecommendedMedicine[];
    precautionaryNotes: string[];
    /** "agentic-ai" for AI output; "unavailable" when the AI service could not respond. */
    source?: string;
}

export function getTreatmentRecommendations(examinationId: string) {
    return apiRequest<TreatmentRecommendation>(`/examinations/${examinationId}/recommendations`);
}

// ---- Lookups (Pets & Medicines for Dropdowns) ----

export interface PetLookup {
    id: string;
    name: string;
    species: string;
    breed: string;
}

export interface MedicineLookup {
    id: string;
    name: string;
}

export function getPetsLookup() {
    return apiRequest<PetLookup[]>('/lookups/pets');
}

export function getMedicinesLookup() {
    return apiRequest<MedicineLookup[]>('/lookups/medicines');
}

// ---- Medicine Requests (inventory officer workflow) ----

/** Full prescription row returned by GET /prescriptions/requests —
 *  denormalised with pet, owner, veterinarian and medicine fields. */
export type MedicineRequest = Prescription;

export function getMedicineRequests(status?: 'Pending' | 'Issued' | 'Unavailable') {
    const query = status ? `?status=${encodeURIComponent(status)}` : '';
    return apiRequest<MedicineRequest[]>(`/prescriptions/requests${query}`);
}

export function issueMedicineRequest(prescriptionId: string) {
    return apiRequest<MedicineRequest>(`/prescriptions/${prescriptionId}/issue`, {
        method: 'POST',
    });
}

export function markMedicineRequestUnavailable(prescriptionId: string, reason: string) {
    return apiRequest<MedicineRequest>(`/prescriptions/${prescriptionId}/unavailable`, {
        method: 'POST',
        body: JSON.stringify({ reason }),
    });
}

