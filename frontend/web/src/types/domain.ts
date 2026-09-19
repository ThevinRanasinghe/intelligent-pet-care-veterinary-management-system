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
  subtotal?: number;
  total?: number;
  isWithinBudget?: boolean;
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

export type DiagnosisSeverity = 'Low' | 'Moderate' | 'High' | 'Critical';
export type TreatmentStatus = 'Planned' | 'InProgress' | 'Completed' | 'Cancelled';

export interface Examination {
  id: string;
  petId: string;
  veterinarianId: string;
  symptoms: string;
  notes: string;
  examinationDate: string;
  createdAt: string;
}

export interface Diagnosis {
  id: string;
  examinationId: string;
  conditionName: string;
  description: string;
  severity: DiagnosisSeverity;
  createdAt: string;
}

export interface TreatmentRecord {
  id: string;
  diagnosisId: string;
  procedureName: string;
  notes: string;
  status: TreatmentStatus;
  createdAt: string;
  updatedAt: string;
}

export interface Prescription {
  id: string;
  treatmentRecordId: string;
  medicineId: string;
  dosage: string;
  durationDays: number;
  createdAt: string;
}

export interface Medicine {
  id: string;
  name: string;
  category: string;
  description: string;
  dosageForm: string;
  strength: string;
  unitPrice: number;
  manufacturer: string;
  status: 'Active' | 'Discontinued';
  reorderLevel: number;
  totalQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  createdAt: string;
  updatedAt: string;
}

export interface MedicineBatch {
  id: string;
  medicineId: string;
  batchNumber: string;
  quantity: number;
  expiryDate: string;
  receivedDate?: string;
  status?: string;
  isExpired?: boolean;
}

export interface Supplier {
  id: string;
  name: string;
  contactPerson: string;
  phone: string;
  email: string;
  address: string;
  status: 'Active' | 'Inactive';
}

export interface InventoryTransaction {
  id: string;
  medicineId: string;
  batchId?: string;
  reservationId?: string;
  type: string;
  quantityChange: number;
  performedByUserId: string;
  notes?: string;
  occurredAt: string;
}

export interface MedicineReservation {
  id: string;
  medicineId: string;
  quantity: number;
  status: 'Reserved' | 'Dispensed' | 'Cancelled' | 'Expired';
  referenceType?: string;
  referenceId?: string;
  createdAt?: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
