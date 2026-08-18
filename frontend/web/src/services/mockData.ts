import type { AIWorkflow, ApprovalProposal, AppointmentSlot, Quotation, Veterinarian } from '../types/domain';

export const veterinarians: Veterinarian[] = [
  { id: 'vet-01', name: 'Dr. Anika Perera', specialisation: 'Small Animal Medicine', branch: 'Colombo', active: true },
  { id: 'vet-02', name: 'Dr. Kavindu Silva', specialisation: 'Surgery & Emergency Care', branch: 'Colombo', active: true },
  { id: 'vet-03', name: 'Dr. Malith Fernando', specialisation: 'Dermatology', branch: 'Kandy', active: true },
  { id: 'vet-04', name: 'Dr. Senuri Jayasinghe', specialisation: 'General Practice', branch: 'Nugegoda', active: true },
];

export const appointmentSlots: AppointmentSlot[] = [
  { id: 'slot-01', veterinarianId: 'vet-01', veterinarianName: 'Dr. Anika Perera', date: '2026-08-20', startTime: '09:00', endTime: '09:30', branch: 'Colombo', status: 'Confirmed', petName: 'Milo', ownerName: 'Nadeesha Silva', requestId: 'REQ-1042' },
  { id: 'slot-02', veterinarianId: 'vet-01', veterinarianName: 'Dr. Anika Perera', date: '2026-08-20', startTime: '10:00', endTime: '10:30', branch: 'Colombo', status: 'Available' },
  { id: 'slot-03', veterinarianId: 'vet-02', veterinarianName: 'Dr. Kavindu Silva', date: '2026-08-20', startTime: '11:00', endTime: '11:45', branch: 'Colombo', status: 'Reserved', petName: 'Max', ownerName: 'Miran Perera', requestId: 'REQ-1048' },
  { id: 'slot-04', veterinarianId: 'vet-03', veterinarianName: 'Dr. Malith Fernando', date: '2026-08-21', startTime: '14:00', endTime: '14:30', branch: 'Kandy', status: 'Available' },
  { id: 'slot-05', veterinarianId: 'vet-04', veterinarianName: 'Dr. Senuri Jayasinghe', date: '2026-08-21', startTime: '15:00', endTime: '15:30', branch: 'Nugegoda', status: 'Confirmed', petName: 'Bella', ownerName: 'Tharindu Senanayake', requestId: 'REQ-1051' },
  { id: 'slot-06', veterinarianId: 'vet-02', veterinarianName: 'Dr. Kavindu Silva', date: '2026-08-22', startTime: '16:00', endTime: '16:45', branch: 'Colombo', status: 'Available' },
];

export const quotations: Quotation[] = [
  {
    id: 'QUO-2026-021', requestId: 'REQ-1048', petName: 'Max', ownerName: 'Miran Perera', veterinarianName: 'Dr. Kavindu Silva', appointmentDate: '2026-08-20', appointmentTime: '11:00', branch: 'Colombo', budget: 15000, status: 'PendingApproval', createdAt: '2026-08-18T09:15:00', updatedAt: '2026-08-18T10:25:00',
    items: [
      { id: 'li-01', category: 'Consultation', description: 'Urgent consultation', quantity: 1, unitPrice: 3500 },
      { id: 'li-02', category: 'Examination', description: 'Clinical examination & diagnostic panel', quantity: 1, unitPrice: 4200 },
      { id: 'li-03', category: 'Medicine', description: 'Supportive medicine pack', quantity: 1, unitPrice: 2800 },
    ],
  },
  {
    id: 'QUO-2026-020', requestId: 'REQ-1042', petName: 'Milo', ownerName: 'Nadeesha Silva', veterinarianName: 'Dr. Anika Perera', appointmentDate: '2026-08-20', appointmentTime: '09:00', branch: 'Colombo', budget: 10000, status: 'Approved', createdAt: '2026-08-17T11:00:00', updatedAt: '2026-08-17T13:20:00',
    items: [
      { id: 'li-04', category: 'Consultation', description: 'General consultation', quantity: 1, unitPrice: 2500 },
      { id: 'li-05', category: 'Treatment', description: 'Wound dressing', quantity: 2, unitPrice: 1200 },
    ],
  },
  {
    id: 'QUO-2026-019', requestId: 'REQ-1039', petName: 'Coco', ownerName: 'Sashika Perera', veterinarianName: 'Dr. Malith Fernando', appointmentDate: '2026-08-19', appointmentTime: '14:30', branch: 'Kandy', budget: 8000, status: 'RevisionRequested', createdAt: '2026-08-16T14:40:00', updatedAt: '2026-08-18T08:10:00',
    items: [
      { id: 'li-06', category: 'Consultation', description: 'Dermatology consultation', quantity: 1, unitPrice: 3000 },
      { id: 'li-07', category: 'Medicine', description: 'Topical treatment', quantity: 1, unitPrice: 3600 },
    ],
  },
];

export const approvalProposals: ApprovalProposal[] = [
  {
    id: 'APR-2026-011', workflowId: 'WF-2026-031', requestId: 'REQ-1048', petName: 'Max', ownerName: 'Miran Perera', urgency: 'Urgent', proposedVet: 'Dr. Kavindu Silva', proposedDate: '2026-08-20', proposedTime: '11:00–11:45', branch: 'Colombo', quotationTotal: 10500, budget: 15000, status: 'Pending', submittedAt: '2026-08-18T10:25:00', summary: 'Urgent vomiting + lethargy consultation. Proposal includes consultation, diagnostic examination and supportive medicine.',
    validationChecks: [
      { key: 'vet', label: 'Veterinarian specialisation', passed: true, detail: 'Emergency Care is compatible with the request priority.' },
      { key: 'slot', label: 'Appointment conflict', passed: true, detail: 'Selected slot has no overlap.' },
      { key: 'budget', label: 'Budget rule', passed: true, detail: 'Quotation is within the owner budget.' },
      { key: 'quote', label: 'Quotation total', passed: true, detail: 'Line items sum to LKR 10,500.' },
    ],
  },
  {
    id: 'APR-2026-010', workflowId: 'WF-2026-028', requestId: 'REQ-1039', petName: 'Coco', ownerName: 'Sashika Perera', urgency: 'Priority', proposedVet: 'Dr. Malith Fernando', proposedDate: '2026-08-19', proposedTime: '14:30–15:00', branch: 'Kandy', quotationTotal: 6600, budget: 8000, status: 'RevisionRequested', submittedAt: '2026-08-18T08:10:00', summary: 'Dermatology review proposal. Manager requested a clearer medication line item before approval.',
    validationChecks: [
      { key: 'vet', label: 'Veterinarian specialisation', passed: true, detail: 'Dermatology specialist selected.' },
      { key: 'slot', label: 'Appointment conflict', passed: true, detail: 'Selected slot has no overlap.' },
      { key: 'budget', label: 'Budget rule', passed: true, detail: 'Quotation is within the owner budget.' },
      { key: 'quote', label: 'Quotation detail', passed: false, detail: 'Medication description requires revision.' },
    ],
  },
];

export const workflows: AIWorkflow[] = [
  {
    id: 'WF-2026-031', requestId: 'REQ-1048', objective: 'Find an urgent conflict-free veterinary consultation for Max this week before 6 PM within LKR 15,000.', status: 'PendingManagerApproval', createdAt: '2026-08-18T09:55:00', note: 'UI-only monitor. No agent execution is connected in this phase.',
    steps: [
      { id: 's1', name: 'Plan objective', responsibility: 'Planning Agent', status: 'Completed', completedAt: '2026-08-18T09:56:00' },
      { id: 's2', name: 'Analyse symptoms', responsibility: 'Diagnosis Analysis Agent', status: 'Completed', completedAt: '2026-08-18T09:58:00' },
      { id: 's3', name: 'Check medicine availability', responsibility: 'Inventory & Medicine Agent', status: 'Completed', completedAt: '2026-08-18T10:02:00' },
      { id: 's4', name: 'Find vet + appointment + validate', responsibility: 'Scheduling & Validation Agent', status: 'Completed', completedAt: '2026-08-18T10:15:00' },
      { id: 's5', name: 'Manager approval', responsibility: 'Human approval checkpoint', status: 'Running', startedAt: '2026-08-18T10:25:00' },
    ],
  },
];
