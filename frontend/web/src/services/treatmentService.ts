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

export interface CreatePrescriptionInput {
    treatmentRecordId: string;
    medicineId: string;
    dosage: string;
    durationDays: number;
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

export function getDiagnosisByExamination(examinationId: string) {
    return apiRequest<Diagnosis>(`/diagnoses/examination/${examinationId}`);
}

export function createDiagnosis(input: CreateDiagnosisInput) {
    return apiRequest<Diagnosis>('/diagnoses', {
        method: 'POST',
        body: JSON.stringify(input),
    });
}

// ---- Treatment Records ----

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

export function getPrescriptionsByTreatment(treatmentRecordId: string) {
    return apiRequest<Prescription[]>(`/prescriptions/treatment/${treatmentRecordId}`);
}

export function createPrescription(input: CreatePrescriptionInput) {
    return apiRequest<Prescription>('/prescriptions', {
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
    medicineId: string;
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

