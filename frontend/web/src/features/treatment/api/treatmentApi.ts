import axios from 'axios';

const API_BASE_URL = 'http://localhost:5000/api';

export interface Examination {
    id?: string;
    petId: string;
    vetId: string;
    examinationDate: string;
    symptoms: string;
    notes?: string;
}

export interface Diagnosis {
    id?: string;
    examinationId: string;
    diseaseName: string;
    severity: string;
    notes?: string;
}

export interface TreatmentRecord {
    id?: string;
    diagnosisId: string;
    treatmentDetails: string;
    status: string;
    startDate: string;
    endDate?: string;
}

export interface Prescription {
    id?: string;
    treatmentRecordId: string;
    medicineId: string;
    dosage: string;
    durationDays: number;
}

// API Calls matching your C# Controllers
export const treatmentApi = {
    // Examinations
    getExaminations: () => axios.get<Examination[]>(`${API_BASE_URL}/examinations`),
    getExaminationsByPet: (petId: string) => axios.get<Examination[]>(`${API_BASE_URL}/examinations/pet/${petId}`),
    createExamination: (data: Examination) => axios.post<Examination>(`${API_BASE_URL}/examinations`, data),

    // Diagnoses
    getDiagnoses: () => axios.get<Diagnosis[]>(`${API_BASE_URL}/diagnoses`),
    createDiagnosis: (data: Diagnosis) => axios.post<Diagnosis>(`${API_BASE_URL}/diagnoses`, data),

    // Treatment Records
    getTreatmentRecords: () => axios.get<TreatmentRecord[]>(`${API_BASE_URL}/treatmentrecords`),
    updateTreatmentStatus: (id: string, status: string) =>
        axios.patch(`${API_BASE_URL}/treatmentrecords/${id}/status`, { status }),

    // Prescriptions
    getPrescriptionsByTreatment: (treatmentRecordId: string) =>
        axios.get<Prescription[]>(`${API_BASE_URL}/prescriptions/treatment/${treatmentRecordId}`),
    createPrescription: (data: Prescription) => axios.post<Prescription>(`${API_BASE_URL}/prescriptions`, data),
};