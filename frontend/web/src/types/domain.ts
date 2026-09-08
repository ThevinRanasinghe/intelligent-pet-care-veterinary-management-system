export type Role = 'PetOwner' | 'Veterinarian' | 'InventoryOfficer' | 'ClinicManager' | 'Administrator';

export type AppointmentStatus = 'Available' | 'Reserved' | 'Confirmed' | 'Completed' | 'Cancelled';
export type QuotationStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected' | 'RevisionRequested' | 'Finalised';
export type ApprovalStatus = 'Pending' | 'Approved' | 'Rejected' | 'RevisionRequested';
export type WorkflowStatus = 'Draft' | 'Planning' | 'Validating' | 'PendingManagerApproval' | 'Approved' | 'Rejected' | 'RevisionRequested' | 'Completed' | 'Failed';

export interface Veterinarian {
  id: string;
  name: string;
  specialisation: string;
  branch: string;
  active: boolean;
}

export interface AppointmentSlot {
  id: string;
  veterinarianId: string;
  veterinarianName: string;
  date: string;
  startTime: string;
  endTime: string;
  branch: string;
  status: AppointmentStatus;
  petName?: string;
  ownerName?: string;
  requestId?: string;
}

export interface QuoteLineItem {
  id: string;
  category: 'Consultation' | 'Examination' | 'Treatment' | 'Medicine' | 'Other';
  description: string;
  quantity: number;
  unitPrice: number;
}

export interface Quotation {
  id: string;
  requestId: string;
  petName: string;
  ownerName: string;
  veterinarianName: string;
  appointmentDate: string;
  appointmentTime: string;
  branch: string;
  budget: number;
  status: QuotationStatus;
  items: QuoteLineItem[];
  createdAt: string;
  updatedAt: string;
}

export interface ValidationCheck {
  key: string;
  label: string;
  passed: boolean;
  detail: string;
}

export interface ApprovalProposal {
  id: string;
  workflowId: string;
  requestId: string;
  petName: string;
  ownerName: string;
  urgency: 'Routine' | 'Priority' | 'Urgent';
  proposedVet: string;
  proposedDate: string;
  proposedTime: string;
  branch: string;
  quotationTotal: number;
  budget: number;
  status: ApprovalStatus;
  submittedAt: string;
  summary: string;
  validationChecks: ValidationCheck[];
}

export interface WorkflowStep {
  id: string;
  name: string;
  responsibility: string;
  status: 'Pending' | 'Running' | 'Completed' | 'Blocked' | 'Skipped';
  startedAt?: string;
  completedAt?: string;
}

export interface AIWorkflow {
  id: string;
  requestId: string;
  objective: string;
  status: WorkflowStatus;
  createdAt: string;
  steps: WorkflowStep[];
  note?: string;
}

// ----------------------------------------------------
// Component 1: Pet & Consultation Request Management Types
// (UC-05 to UC-17, aligned with C# .NET 8 Backend DTOs)
// ----------------------------------------------------

export enum ConsultationStatus {
  Submitted = 1,
  Processing = 2,
  PendingApproval = 3,
  Approved = 4,
  Rejected = 5,
  RevisionRequired = 6,
  AppointmentConfirmed = 7,
}

export type ConsultationStatusString =
  | 'Submitted'
  | 'Processing'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'RevisionRequired'
  | 'AppointmentConfirmed';

export interface PetOwner {
  id: string; // e.g., OWN-2001
  fullName: string;
  email: string;
  phoneNumber: string;
  address?: string | null;
  createdAt?: string;
}

export interface Pet {
  id: string; // e.g., PET-1001
  ownerId: string; // e.g., OWN-2001
  name: string;
  species: string;
  breed: string;
  dateOfBirth?: string | null;
  age: number;
  notes?: string | null;
  photoUrl?: string | null;
  medicalHistorySummary?: string | null;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreatePetDto {
  id?: string;
  ownerId: string;
  name: string;
  species: string;
  breed: string;
  dateOfBirth?: string | null;
  age: number;
  notes?: string | null;
  photoUrl?: string | null;
  medicalHistorySummary?: string | null;
}

export interface UpdatePetProfileDto {
  name: string;
  species: string;
  breed: string;
  dateOfBirth?: string | null;
  age: number;
  notes?: string | null;
  photoUrl?: string | null;
}

export interface MedicalRecordDto {
  id: string; // e.g., MED-3001
  petId: string;
  recordDate: string;
  diagnosis: string;
  treatment: string;
  veterinarianName?: string | null;
  clinicalNotes?: string | null;
  createdAt?: string;
}

export interface CreateMedicalRecordDto {
  id?: string;
  recordDate?: string | null;
  diagnosis: string;
  treatment: string;
  veterinarianName?: string | null;
  clinicalNotes?: string | null;
}

export interface VaccinationRecordDto {
  id: string; // e.g., VAC-4001
  petId: string;
  vaccineName: string;
  dateAdministered: string;
  nextDueDate?: string | null;
  veterinarianName?: string | null;
  batchNumber?: string | null;
  createdAt?: string;
}

export interface CreateVaccinationRecordDto {
  id?: string;
  vaccineName: string;
  dateAdministered: string;
  nextDueDate?: string | null;
  veterinarianName?: string | null;
  batchNumber?: string | null;
}

export interface PetMedicalHistoryDto {
  petId: string;
  petName: string;
  medicalRecords: MedicalRecordDto[];
  vaccinationRecords: VaccinationRecordDto[];
}

export interface ConsultationRequest {
  id: string; // e.g., REQ-5001
  petId: string;
  petName?: string | null;
  ownerId: string;
  ownerName?: string | null;
  symptomsDescription: string;
  photoUrl?: string | null;
  preferredDate: string;
  budgetLimit: number;
  preferredClinicLocationLat?: number | null;
  preferredClinicLocationLong?: number | null;
  preferredBranch?: string | null;
  status: ConsultationStatusString;
  statusNotes?: string | null;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreateConsultationRequestDto {
  id?: string;
  petId: string;
  ownerId: string;
  symptomsDescription: string;
  photoUrl?: string | null;
  preferredDate: string;
  budgetLimit: number;
  preferredClinicLocationLat?: number | null;
  preferredClinicLocationLong?: number | null;
  preferredBranch?: string | null;
}

export interface ConsultationStatusTrackingDto {
  consultationId: string;
  petId: string;
  status: ConsultationStatusString;
  statusNotes?: string | null;
  updatedAt?: string;
  history: ConsultationHistoryItemDto[];
}

export interface ConsultationHistoryItemDto {
  id: number;
  status: ConsultationStatusString;
  comments?: string | null;
  changedAt: string;
}

export interface UpdateConsultationStatusDto {
  status: ConsultationStatus;
  comments?: string | null;
}

export interface ClinicBranch {
  name: string;
  address: string;
  lat: number;
  lng: number;
  distanceKm?: number;
}

