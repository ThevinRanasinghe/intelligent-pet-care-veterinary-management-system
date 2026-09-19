import React, { useEffect, useState } from 'react';
import {
    ClipboardPlus,
    FileText,
    Pill,
    Search,
    Stethoscope,
    Sparkles,
    Trash2,
    Edit3,
    Activity,
    CheckCircle2,
    Calendar,
    User,
    Info,
    RefreshCw,
    AlertCircle
} from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Modal } from '../../components/ui/Modal';
import {
    getExaminations,
    createExamination,
    updateExamination,
    deleteExamination,
    getDiagnosisByExamination,
    createDiagnosis,
    updateDiagnosis,
    deleteDiagnosis,
    getTreatmentRecordsByDiagnosis,
    createTreatmentRecord,
    updateTreatmentRecord,
    updateTreatmentStatus,
    deleteTreatmentRecord,
    getPrescriptionsByTreatment,
    createPrescription,
    deletePrescription,
    getTreatmentRecommendations,
    getPetsLookup,
    getMedicinesLookup,
    type PetLookup,
    type MedicineLookup,
    type TreatmentRecommendation
} from '../../services/treatmentService';
import type {
    Examination,
    Diagnosis,
    TreatmentRecord,
    Prescription,
    DiagnosisSeverity,
    TreatmentStatus
} from '../../types/domain';
import { formatDate } from '../../utils/format';

const severityTone: Record<DiagnosisSeverity, 'success' | 'warning' | 'neutral' | 'danger' | 'info'> = {
    Low: 'success',
    Moderate: 'info',
    High: 'warning',
    Critical: 'danger',
};

const statusTone: Record<TreatmentStatus, 'success' | 'warning' | 'neutral' | 'danger' | 'info'> = {
    Planned: 'neutral',
    InProgress: 'info',
    Completed: 'success',
    Cancelled: 'danger',
};

export function TreatmentPage() {
    // ---- State ----
    const [examinations, setExaminations] = useState<Examination[]>([]);
    const [pets, setPets] = useState<PetLookup[]>([]);
    const [medicines, setMedicines] = useState<MedicineLookup[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadError, setLoadError] = useState('');

    // Search, Filter & Pagination
    const [query, setQuery] = useState('');
    const [severityFilter, setSeverityFilter] = useState<string>('All');
    const [statusFilter, setStatusFilter] = useState<string>('All');
    const [currentPage, setCurrentPage] = useState(1);
    const [sortBy, setSortBy] = useState('date-desc');
    const pageSize = 5;

    // Detail & Drill-down
    const [expandedId, setExpandedId] = useState<string | null>(null);
    const [diagnosis, setDiagnosis] = useState<Diagnosis | null>(null);
    const [treatments, setTreatments] = useState<TreatmentRecord[]>([]);
    const [prescriptionsByTreatment, setPrescriptionsByTreatment] = useState<Record<string, Prescription[]>>({});
    const [detailLoading, setDetailLoading] = useState(false);

    // Business-Specific Recommendation Engine
    const [recommendations, setRecommendations] = useState<TreatmentRecommendation | null>(null);
    const [recLoading, setRecLoading] = useState(false);
    const [showRecModal, setShowRecModal] = useState(false);

    // Modals
    const [showExamModal, setShowExamModal] = useState(false);
    const [editingExamId, setEditingExamId] = useState<string | null>(null);
    const [examForm, setExamForm] = useState({
        petId: '',
        veterinarianId: '22222222-2222-2222-2222-222222222222',
        symptoms: '',
        notes: '',
        examinationDate: new Date().toISOString().slice(0, 16)
    });

    const [showDiagnosisModal, setShowDiagnosisModal] = useState(false);
    const [editingDiagnosisId, setEditingDiagnosisId] = useState<string | null>(null);
    const [diagnosisForm, setDiagnosisForm] = useState({
        conditionName: '',
        description: '',
        severity: 'Moderate' as DiagnosisSeverity
    });

    const [showTreatmentModal, setShowTreatmentModal] = useState(false);
    const [editingTreatmentId, setEditingTreatmentId] = useState<string | null>(null);
    const [treatmentForm, setTreatmentForm] = useState({
        procedureName: '',
        notes: ''
    });

    const [showPrescriptionModal, setShowPrescriptionModal] = useState<string | null>(null);
    const [prescriptionForm, setPrescriptionForm] = useState({
        medicineId: '',
        dosage: '',
        durationDays: 5
    });

    const [formError, setFormError] = useState('');
    const [actionSuccess, setActionSuccess] = useState('');

    // ---- Initial Load ----
    const loadAllData = async () => {
        setLoading(true);
        setLoadError('');
        try {
            const [examsData, petsData, medsData] = await Promise.all([
                getExaminations(),
                getPetsLookup().catch(() => []),
                getMedicinesLookup().catch(() => [])
            ]);
            setExaminations(examsData);
            setPets(petsData);
            setMedicines(medsData);
            if (petsData.length > 0 && !examForm.petId) {
                setExamForm(prev => ({ ...prev, petId: petsData[0].id }));
            }
        } catch {
            setLoadError('Could not load data. Ensure PetCare.Api backend is running on port 5152.');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        loadAllData();
    }, []);

    // ---- Load Detailed Examination Data ----
    const loadDetail = (examinationId: string) => {
        setDetailLoading(true);
        setDiagnosis(null);
        setTreatments([]);
        setPrescriptionsByTreatment({});
        setRecommendations(null);

        getDiagnosisByExamination(examinationId)
            .then(async (d) => {
                setDiagnosis(d);
                if (d && d.id) {
                    const records = await getTreatmentRecordsByDiagnosis(d.id);
                    setTreatments(records);
                    const entries = await Promise.all(
                        records.map(async (r) => [r.id, await getPrescriptionsByTreatment(r.id)] as const)
                    );
                    setPrescriptionsByTreatment(Object.fromEntries(entries));
                }
            })
            .catch(() => setDiagnosis(null))
            .finally(() => setDetailLoading(false));
    };

    const toggleDetail = (examinationId: string) => {
        if (expandedId === examinationId) {
            setExpandedId(null);
            return;
        }
        setExpandedId(examinationId);
        loadDetail(examinationId);
    };

    // ---- Intelligent Recommendations (Business-Specific Operation) ----
    const handleFetchRecommendations = async (examinationId: string, e: React.MouseEvent) => {
        e.stopPropagation();
        setRecLoading(true);
        setShowRecModal(true);
        try {
            const rec = await getTreatmentRecommendations(examinationId);
            setRecommendations(rec);
        } catch {
            setRecommendations(null);
        } finally {
            setRecLoading(false);
        }
    };

    const handleApplyRecommendation = () => {
        if (!recommendations || !expandedId) return;
        setDiagnosisForm({
            conditionName: recommendations.suspectedCondition,
            description: recommendations.rationale,
            severity: recommendations.recommendedSeverity
        });
        setShowRecModal(false);
        setShowDiagnosisModal(true);
    };

    // ---- Examination Handlers ----
    const openCreateExamModal = () => {
        setEditingExamId(null);
        setExamForm({
            petId: pets[0]?.id || '',
            veterinarianId: '22222222-2222-2222-2222-222222222222',
            symptoms: '',
            notes: '',
            examinationDate: new Date().toISOString().slice(0, 16)
        });
        setFormError('');
        setShowExamModal(true);
    };

    const openEditExamModal = (exam: Examination, e: React.MouseEvent) => {
        e.stopPropagation();
        setEditingExamId(exam.id);
        setExamForm({
            petId: exam.petId,
            veterinarianId: exam.veterinarianId,
            symptoms: exam.symptoms,
            notes: exam.notes,
            examinationDate: exam.examinationDate.slice(0, 16)
        });
        setFormError('');
        setShowExamModal(true);
    };

    const handleSaveExamination = async () => {
        setFormError('');

        if (!examForm.petId) {
            setFormError('Please select a pet.');
            return;
        }

        if (!examForm.symptoms.trim()) {
            setFormError('Please enter the observed symptoms.');
            return;
        }

        if (!examForm.examinationDate) {
            setFormError('Please select the examination date and time.');
            return;
        }

        const examinationDate = new Date(examForm.examinationDate);

        if (Number.isNaN(examinationDate.getTime())) {
            setFormError('Please enter a valid examination date and time.');
            return;
        }

        if (examinationDate > new Date()) {
            setFormError('Examination date and time cannot be in the future.');
            return;
        }

        try {
            if (editingExamId) {
                await updateExamination(editingExamId, {
                    symptoms: examForm.symptoms,
                    notes: examForm.notes,
                    examinationDate: new Date(examForm.examinationDate).toISOString()
                });
                setActionSuccess('Examination updated successfully.');
            } else {
                await createExamination({
                    petId: examForm.petId,
                    veterinarianId: examForm.veterinarianId,
                    symptoms: examForm.symptoms,
                    notes: examForm.notes,
                    examinationDate: new Date(examForm.examinationDate).toISOString()
                });
                setActionSuccess('New examination created.');
            }
            setShowExamModal(false);
            const updated = await getExaminations();
            setExaminations(updated);
            setTimeout(() => setActionSuccess(''), 4000);
        } catch {
            setFormError('Failed to save examination.');
        }
    };

    const handleDeleteExam = async (id: string, e: React.MouseEvent) => {
        e.stopPropagation();
        if (!window.confirm('Are you sure you want to delete this examination record?')) return;
        try {
            await deleteExamination(id);
            if (expandedId === id) setExpandedId(null);
            const updated = await getExaminations();
            setExaminations(updated);
            setActionSuccess('Examination deleted successfully.');
            setTimeout(() => setActionSuccess(''), 4000);
        } catch {
            alert('Failed to delete examination.');
        }
    };

    // ---- Diagnosis Handlers ----
    const openCreateDiagnosisModal = () => {
        setEditingDiagnosisId(null);
        setDiagnosisForm({ conditionName: '', description: '', severity: 'Moderate' });
        setFormError('');
        setShowDiagnosisModal(true);
    };

    const openEditDiagnosisModal = (diag: Diagnosis) => {
        setEditingDiagnosisId(diag.id);
        setDiagnosisForm({
            conditionName: diag.conditionName,
            description: diag.description,
            severity: diag.severity
        });
        setFormError('');
        setShowDiagnosisModal(true);
    };

    const handleSaveDiagnosis = async () => {
        if (!expandedId) return;
        setFormError('');

        if (!diagnosisForm.conditionName.trim()) {
            setFormError('Please enter the diagnosis/condition name.');
            return;
        }

        if (!diagnosisForm.description.trim()) {
            setFormError('Please enter a description of the diagnosis.');
            return;
        }

        if (!diagnosisForm.severity) {
            setFormError('Please select the diagnosis severity.');
            return;
        }

        try {
            if (editingDiagnosisId) {
                await updateDiagnosis(editingDiagnosisId, diagnosisForm);
                setActionSuccess('Diagnosis updated.');
            } else {
                await createDiagnosis({
                    examinationId: expandedId,
                    ...diagnosisForm
                });
                setActionSuccess('Diagnosis created.');
            }
            setShowDiagnosisModal(false);
            loadDetail(expandedId);
            setTimeout(() => setActionSuccess(''), 4000);
        } catch {
            setFormError('Failed to save diagnosis.');
        }
    };

    const handleDeleteDiagnosis = async (id: string) => {
        if (!window.confirm('Are you sure you want to delete this diagnosis?')) return;
        try {
            await deleteDiagnosis(id);
            if (expandedId) loadDetail(expandedId);
            setActionSuccess('Diagnosis removed.');
            setTimeout(() => setActionSuccess(''), 4000);
        } catch {
            alert('Failed to delete diagnosis.');
        }
    };

    // ---- Treatment Handlers ----
    const openCreateTreatmentModal = () => {
        setEditingTreatmentId(null);
        setTreatmentForm({ procedureName: '', notes: '' });
        setFormError('');
        setShowTreatmentModal(true);
    };

    const openEditTreatmentModal = (tr: TreatmentRecord) => {
        setEditingTreatmentId(tr.id);
        setTreatmentForm({ procedureName: tr.procedureName, notes: tr.notes });
        setFormError('');
        setShowTreatmentModal(true);
    };

    const handleSaveTreatment = async () => {
        if (!diagnosis) return;
        setFormError('');

        if (!treatmentForm.procedureName.trim()) {
            setFormError('Please enter the procedure or intervention name.');
            return;
        }

        try {
            if (editingTreatmentId) {
                await updateTreatmentRecord(editingTreatmentId, treatmentForm);
                setActionSuccess('Treatment record updated.');
            } else {
                await createTreatmentRecord({
                    diagnosisId: diagnosis.id,
                    ...treatmentForm
                });
                setActionSuccess('Treatment record created.');
            }
            setShowTreatmentModal(false);
            if (expandedId) loadDetail(expandedId);
            setTimeout(() => setActionSuccess(''), 4000);
        } catch {
            setFormError('Failed to save treatment record.');
        }
    };

    const handleStatusChange = async (id: string, status: TreatmentStatus) => {
        try {
            await updateTreatmentStatus(id, status);
            if (expandedId) loadDetail(expandedId);
        } catch {
            alert('Failed to update treatment status.');
        }
    };

    const handleDeleteTreatment = async (id: string) => {
        if (!window.confirm('Delete this treatment record?')) return;
        try {
            await deleteTreatmentRecord(id);
            if (expandedId) loadDetail(expandedId);
        } catch {
            alert('Failed to delete treatment record.');
        }
    };

    // ---- Prescription Handlers ----


    const handleCreatePrescription = async () => {
        if (!treatments) return;

        setFormError('');

        if (!prescriptionForm.medicineId) {
            setFormError('Please select a medicine.');
            return;
        }

        if (!prescriptionForm.dosage.trim()) {
            setFormError('Please enter the dosage.');
            return;
        }

        if (
            !Number.isInteger(prescriptionForm.durationDays) ||
            prescriptionForm.durationDays < 1 ||
            prescriptionForm.durationDays > 90
        ) {
            setFormError('Duration must be a whole number between 1 and 90 days.');
            return;
        }

        try {

            if (!showPrescriptionModal) {
                setFormError('Please select a treatment record.');
                return;
            }
            await createPrescription({
                treatmentRecordId: showPrescriptionModal,
                ...prescriptionForm
            });
            setShowPrescriptionModal(null);
            setPrescriptionForm({ medicineId: medicines[0]?.id || '', dosage: '', durationDays: 5 });
            if (expandedId) loadDetail(expandedId);
            setActionSuccess('Prescription assigned.');
            setTimeout(() => setActionSuccess(''), 4000);
        } catch {
            setFormError('Failed to create prescription.');
        }
    };

    const handleDeletePrescription = async (id: string) => {
        if (!window.confirm('Remove this prescription?')) return;
        try {
            await deletePrescription(id);
            if (expandedId) loadDetail(expandedId);
        } catch {
            alert('Failed to delete prescription.');
        }
    };

    // ---- Helpers ----
    const getPetDisplay = (petId: string) => {
        const found = pets.find(p => p.id === petId);
        if (found) return `${found.name} (${found.species} · ${found.breed})`;
        return `Pet #${petId.slice(0, 8)}…`;
    };

    const getMedicineName = (medId: string) => {
        const found = medicines.find(m => m.id === medId);
        return found ? found.name : `Medicine #${medId.slice(0, 8)}…`;
    };

    // ---- Filter & Pagination Logic ----
    // ---- Search & Pagination Logic ----
    const normalizedQuery = query.trim().toLowerCase();

    const filteredExaminations = examinations.filter((exam) => {
        if (!normalizedQuery) return true;

        const petText = getPetDisplay(exam.petId).toLowerCase();

        const searchableText = [
            petText,
            exam.symptoms,
            exam.notes,
            exam.examinationDate
        ]
            .filter(Boolean)
            .join(' ')
            .toLowerCase();

        return searchableText.includes(normalizedQuery);
    });

    const sortedExaminations = [...filteredExaminations].sort((a, b) => {
        switch (sortBy) {
            case 'date-asc':
                return (
                    new Date(a.examinationDate).getTime() -
                    new Date(b.examinationDate).getTime()
                );

            case 'date-desc':
                return (
                    new Date(b.examinationDate).getTime() -
                    new Date(a.examinationDate).getTime()
                );

            case 'name-asc':
                return getPetDisplay(a.petId).localeCompare(
                    getPetDisplay(b.petId)
                );

            case 'name-desc':
                return getPetDisplay(b.petId).localeCompare(
                    getPetDisplay(a.petId)
                );

            default:
                return 0;
        }
    });

    const totalPages = Math.max(
        1,
        Math.ceil(filteredExaminations.length / pageSize)
    );


    // Make sure the current page is always valid
    const safeCurrentPage = Math.min(currentPage, totalPages);

    const startIndex = (safeCurrentPage - 1) * pageSize;

    const paginatedExaminations = sortedExaminations.slice(
        startIndex,
        startIndex + pageSize
    );

    // Summary Analytics
    const totalExams = examinations.length;
    const completedTreatments = treatments.filter(t => t.status === 'Completed').length;

    return (
        <div className="page-wrap treatment-page">
            {/* Header */}
            <div className="page-heading">
                <div>
                    <div className="eyebrow">Component 2 · Diagnosis &amp; Treatment Management</div>
                    <h2>Clinical Examinations &amp; Treatments</h2>
                    <p>Manage examinations, diagnosis protocols, treatment milestones, and electronic prescriptions.</p>
                </div>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                    <Button variant="secondary" icon={<RefreshCw size={16} />} onClick={loadAllData}>Refresh</Button>
                    <Button icon={<ClipboardPlus size={17} />} onClick={openCreateExamModal}>New Examination</Button>
                </div>
            </div>

            {/* Success / Alert Banner */}
            {actionSuccess && (
                <div style={{
                    backgroundColor: '#e6f7ed',
                    border: '1px solid #38a169',
                    color: '#22543d',
                    padding: '0.75rem 1rem',
                    borderRadius: '8px',
                    marginBottom: '1rem',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.5rem'
                }}>
                    <CheckCircle2 size={18} />
                    <span>{actionSuccess}</span>
                </div>
            )}

            {/* Quick Metrics Bar */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem', marginBottom: '1.5rem' }}>
                <Card>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                        <div style={{ background: 'var(--color-primary-light, #eff6ff)', padding: '0.75rem', borderRadius: '10px', color: '#2563eb' }}>
                            <Activity size={24} />
                        </div>
                        <div>
                            <div className="muted" style={{ fontSize: '0.85rem' }}>Total Examinations</div>
                            <strong style={{ fontSize: '1.4rem' }}>{totalExams}</strong>
                        </div>
                    </div>
                </Card>

                <Card>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                        <div style={{ background: '#fef3c7', padding: '0.75rem', borderRadius: '10px', color: '#d97706' }}>
                            <Stethoscope size={24} />
                        </div>
                        <div>
                            <div className="muted" style={{ fontSize: '0.85rem' }}>Registered Patients</div>
                            <strong style={{ fontSize: '1.4rem' }}>{pets.length} Pets</strong>
                        </div>
                    </div>
                </Card>

                <Card>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                        <div style={{ background: '#f0fdf4', padding: '0.75rem', borderRadius: '10px', color: '#16a34a' }}>
                            <Pill size={24} />
                        </div>
                        <div>
                            <div className="muted" style={{ fontSize: '0.85rem' }}>Formulary Items</div>
                            <strong style={{ fontSize: '1.4rem' }}>{medicines.length} Medicines</strong>
                        </div>
                    </div>
                </Card>
            </div>

            {/* Search & Filters */}
            <div className="filter-bar" style={{ display: 'flex', gap: '1rem', alignItems: 'center', flexWrap: 'wrap' }}>
                <div className="search-input" style={{ flex: '1 1 300px' }}>
                    <Search size={17} />
                    <input
                        value={query}
                        onChange={(e) => { setQuery(e.target.value); setCurrentPage(1); }}
                        placeholder="Search by pet name, breed, symptoms or clinical notes…"
                    />
                </div>

                <div className="sort-control">
                    <label htmlFor="sort-by">Sort By:</label>
                    <select
                        id="sort-by"
                        value={sortBy}
                        onChange={(e) => {
                            setSortBy(e.target.value);
                            setCurrentPage(1);
                        }}
                    >
                        <option value="date-desc">Examination Date — Newest First</option>
                        <option value="date-asc">Examination Date — Oldest First</option>
                        <option value="name-asc">Pet Name — A → Z</option>
                        <option value="name-desc">Pet Name — Z → A</option>
                    </select>
                </div>

                <div className="filter-summary">
                    Showing {paginatedExaminations.length} of {filteredExaminations.length} records
                </div>
            </div>

            {loading && <p className="muted" style={{ padding: '2rem 0' }}>Loading examinations from backend…</p>}
            {loadError && (
                <div className="form-error" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '1rem' }}>
                    <AlertCircle size={18} />
                    <span>{loadError}</span>
                </div>
            )}

            {/* Main Examination Table */}
            {!loading && !loadError && (
                <Card>
                    {filteredExaminations.length === 0 ? (
                        <div style={{ padding: '3rem', textAlign: 'center' }}>
                            <Info size={32} className="muted" style={{ margin: '0 auto 0.5rem' }} />
                            <p className="muted">No clinical examinations found matching your criteria.</p>
                        </div>
                    ) : (
                        <div className="table-wrap">
                            <table>
                                <thead>
                                    <tr>
                                        <th>Date</th>
                                        <th>Patient / Pet</th>
                                        <th>Observed Symptoms</th>
                                        <th>Clinical Notes</th>
                                        <th style={{ textAlign: 'right' }}>Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {paginatedExaminations.map((exam) => {
                                        const isExpanded = expandedId === exam.id;
                                        return (
                                            <React.Fragment key={exam.id}>
                                                <tr
                                                    onClick={() => toggleDetail(exam.id)}
                                                    style={{ cursor: 'pointer', backgroundColor: isExpanded ? 'var(--color-bg-subtle, #f9fafb)' : undefined }}
                                                >
                                                    <td style={{ whiteSpace: 'nowrap' }}>
                                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                                                            <Calendar size={14} className="muted" />
                                                            <strong>{formatDate(exam.examinationDate.split('T')[0])}</strong>
                                                        </div>
                                                    </td>
                                                    <td>
                                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                                                            <User size={14} className="muted" />
                                                            <span>{getPetDisplay(exam.petId)}</span>
                                                        </div>
                                                    </td>
                                                    <td>
                                                        <span style={{ fontWeight: 500 }}>{exam.symptoms}</span>
                                                    </td>
                                                    <td className="muted-line">{exam.notes || '—'}</td>
                                                    <td style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
                                                        <div style={{ display: 'inline-flex', gap: '0.4rem' }}>
                                                            <Button
                                                                variant="secondary"
                                                                icon={<Sparkles size={14} />}
                                                                onClick={(e) => handleFetchRecommendations(exam.id, e)}
                                                                title="Get intelligent diagnosis & treatment recommendation based on symptoms"
                                                            >
                                                                AI Assist
                                                            </Button>
                                                            <Button
                                                                variant="ghost"
                                                                icon={<Edit3 size={15} />}
                                                                onClick={(e) => openEditExamModal(exam, e)}
                                                                title="Edit examination"
                                                            />
                                                            <Button
                                                                variant="ghost"
                                                                icon={<Trash2 size={15} />}
                                                                onClick={(e) => handleDeleteExam(exam.id, e)}
                                                                title="Delete examination"
                                                            />
                                                        </div>
                                                    </td>
                                                </tr>

                                                {/* Expanded Details Strip */}
                                                {isExpanded && (
                                                    <tr>
                                                        <td colSpan={5} style={{ padding: '1rem 1.5rem', backgroundColor: '#fcfcfc', borderBottom: '2px solid #e5e7eb' }}>
                                                            <div className="info-strip" style={{ display: 'block' }}>
                                                                {detailLoading && <p className="muted">Loading clinical details…</p>}

                                                                {!detailLoading && !diagnosis && (
                                                                    <div style={{ padding: '1rem', border: '1px dashed #cbd5e1', borderRadius: '8px', background: '#fff' }}>
                                                                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                                                            <div>
                                                                                <strong>No diagnosis recorded yet for this examination.</strong>
                                                                                <p className="muted" style={{ margin: '0.25rem 0 0' }}>Click below to enter veterinary findings or use the AI Assist button to review automated recommendations.</p>
                                                                            </div>
                                                                            <div style={{ display: 'flex', gap: '0.5rem' }}>
                                                                                <Button variant="secondary" icon={<Sparkles size={15} />} onClick={(e) => handleFetchRecommendations(exam.id, e)}>
                                                                                    Suggest Recommendations
                                                                                </Button>
                                                                                <Button icon={<Stethoscope size={16} />} onClick={openCreateDiagnosisModal}>
                                                                                    Record Diagnosis
                                                                                </Button>
                                                                            </div>
                                                                        </div>
                                                                    </div>
                                                                )}

                                                                {!detailLoading && diagnosis && (
                                                                    <div>
                                                                        {/* Diagnosis Section */}
                                                                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', background: '#fff', padding: '1rem', borderRadius: '8px', border: '1px solid #e2e8f0', marginBottom: '1rem' }}>
                                                                            <div>
                                                                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '0.25rem' }}>
                                                                                    <h4 style={{ margin: 0 }}>{diagnosis.conditionName}</h4>
                                                                                    <Badge tone={severityTone[diagnosis.severity]}>{diagnosis.severity} Severity</Badge>
                                                                                </div>
                                                                                <p style={{ margin: '0.25rem 0 0', color: '#4b5563' }}>{diagnosis.description}</p>
                                                                            </div>
                                                                            <div style={{ display: 'flex', gap: '0.4rem' }}>
                                                                                <Button variant="ghost" icon={<Edit3 size={15} />} onClick={() => openEditDiagnosisModal(diagnosis)} title="Edit diagnosis" />
                                                                                <Button variant="ghost" icon={<Trash2 size={15} />} onClick={() => handleDeleteDiagnosis(diagnosis.id)} title="Delete diagnosis" />
                                                                            </div>
                                                                        </div>

                                                                        {/* Treatment Records & Prescriptions */}
                                                                        <div style={{ background: '#fff', padding: '1.25rem', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
                                                                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
                                                                                <strong style={{ fontSize: '1rem' }}>Treatment Protocols &amp; Prescriptions</strong>
                                                                                <Button variant="secondary" icon={<ClipboardPlus size={15} />} onClick={openCreateTreatmentModal}>
                                                                                    Add Treatment Procedure
                                                                                </Button>
                                                                            </div>

                                                                            {treatments.length === 0 ? (
                                                                                <p className="muted" style={{ margin: '0.5rem 0' }}>No treatment protocols planned yet.</p>
                                                                            ) : (
                                                                                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                                                                                    {treatments.map((tr) => (
                                                                                        <div key={tr.id} style={{ padding: '0.75rem 1rem', border: '1px solid #e5e7eb', borderRadius: '6px', background: '#fafafa' }}>
                                                                                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.4rem' }}>
                                                                                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                                                                                    <FileText size={16} style={{ color: '#6b7280' }} />
                                                                                                    <strong>{tr.procedureName}</strong>
                                                                                                    <Badge tone={statusTone[tr.status]}>{tr.status}</Badge>
                                                                                                </div>
                                                                                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                                                                                    <select
                                                                                                        value={tr.status}
                                                                                                        onChange={(e) => handleStatusChange(tr.id, e.target.value as TreatmentStatus)}
                                                                                                        style={{ fontSize: '0.85rem', padding: '0.2rem 0.5rem', borderRadius: '4px' }}
                                                                                                    >
                                                                                                        <option value="Planned">Planned</option>
                                                                                                        <option value="InProgress">InProgress</option>
                                                                                                        <option value="Completed">Completed</option>
                                                                                                        <option value="Cancelled">Cancelled</option>
                                                                                                    </select>
                                                                                                    <Button variant="ghost" icon={<Edit3 size={14} />} onClick={() => openEditTreatmentModal(tr)} title="Edit procedure" />
                                                                                                    <Button variant="ghost" icon={<Trash2 size={14} />} onClick={() => handleDeleteTreatment(tr.id)} title="Delete procedure" />
                                                                                                </div>
                                                                                            </div>

                                                                                            <p style={{ margin: '0.25rem 0 0.5rem', fontSize: '0.9rem', color: '#4b5563' }}>{tr.notes}</p>

                                                                                            {/* Prescriptions under this treatment */}
                                                                                            <div style={{ marginTop: '0.5rem', paddingLeft: '0.75rem', borderLeft: '2px solid #e2e8f0' }}>
                                                                                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                                                                                    <span style={{ fontSize: '0.85rem', fontWeight: 600, color: '#374151' }}>Prescribed Medicines:</span>
                                                                                                    <Button
                                                                                                        variant="ghost"
                                                                                                        icon={<Pill size={14} />}
                                                                                                        onClick={() => {
                                                                                                            setShowPrescriptionModal(tr.id);
                                                                                                            setPrescriptionForm({ medicineId: medicines[0]?.id || '', dosage: '', durationDays: 5 });
                                                                                                        }}
                                                                                                    >
                                                                                                        + Add Rx
                                                                                                    </Button>
                                                                                                </div>

                                                                                                {(prescriptionsByTreatment[tr.id] ?? []).length === 0 ? (
                                                                                                    <p className="muted" style={{ fontSize: '0.85rem', margin: '0.25rem 0' }}>No medicines assigned.</p>
                                                                                                ) : (
                                                                                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', marginTop: '0.25rem' }}>
                                                                                                        {(prescriptionsByTreatment[tr.id] ?? []).map((rx) => (
                                                                                                            <div key={rx.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '0.85rem', background: '#fff', padding: '0.3rem 0.6rem', borderRadius: '4px', border: '1px solid #e5e7eb' }}>
                                                                                                                <div>
                                                                                                                    <strong>{getMedicineName(rx.medicineId)}</strong> — {rx.dosage} ({rx.durationDays} days)
                                                                                                                </div>
                                                                                                                <button
                                                                                                                    onClick={() => handleDeletePrescription(rx.id)}
                                                                                                                    style={{ border: 'none', background: 'transparent', color: '#dc2626', cursor: 'pointer' }}
                                                                                                                    title="Remove prescription"
                                                                                                                >
                                                                                                                    <Trash2 size={13} />
                                                                                                                </button>
                                                                                                            </div>
                                                                                                        ))}
                                                                                                    </div>
                                                                                                )}
                                                                                            </div>
                                                                                        </div>
                                                                                    ))}
                                                                                </div>
                                                                            )}
                                                                        </div>
                                                                    </div>
                                                                )}
                                                            </div>
                                                        </td>
                                                    </tr>
                                                )}
                                            </React.Fragment>
                                        );
                                    })}
                                </tbody>
                            </table>
                        </div>
                    )}

                    {/* Pagination Controls */}
                    {totalPages > 1 && (
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '1rem', borderTop: '1px solid #e5e7eb' }}>
                            <div className="muted" style={{ fontSize: '0.85rem' }}>
                                Page {currentPage} of {totalPages}
                            </div>
                            <div style={{ display: 'flex', gap: '0.5rem' }}>
                                <Button
                                    variant="secondary"
                                    disabled={currentPage === 1}
                                    onClick={() => setCurrentPage(prev => Math.max(prev - 1, 1))}
                                >
                                    Previous
                                </Button>
                                <Button
                                    variant="secondary"
                                    disabled={currentPage === totalPages}
                                    onClick={() => setCurrentPage(prev => Math.min(prev + 1, totalPages))}
                                >
                                    Next
                                </Button>
                            </div>
                        </div>
                    )}
                </Card>
            )}

            {/* Modal: Intelligent Recommendations (Business-Specific Operation) */}
            {showRecModal && (
                <Modal title="Intelligent Clinical Treatment Recommendation" onClose={() => setShowRecModal(false)}>
                    {recLoading ? (
                        <p className="muted" style={{ padding: '2rem', textAlign: 'center' }}>Analyzing symptomology and matching clinic treatment protocols…</p>
                    ) : recommendations ? (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                            <div style={{ background: '#f0f9ff', border: '1px solid #bae6fd', padding: '1rem', borderRadius: '8px' }}>
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.25rem' }}>
                                    <h4 style={{ margin: 0, color: '#0369a1' }}>{recommendations.suspectedCondition}</h4>
                                    <Badge tone={severityTone[recommendations.recommendedSeverity]}>{recommendations.recommendedSeverity} Severity</Badge>
                                </div>
                                <p style={{ margin: '0.25rem 0 0', fontSize: '0.9rem', color: '#0c4a6e' }}>{recommendations.rationale}</p>
                            </div>

                            <div>
                                <strong style={{ fontSize: '0.9rem' }}>Recommended Procedures:</strong>
                                <ul style={{ margin: '0.25rem 0', paddingLeft: '1.25rem', fontSize: '0.9rem' }}>
                                    {recommendations.recommendedProcedures.map((proc, i) => (
                                        <li key={i} style={{ marginBottom: '0.25rem' }}>{proc}</li>
                                    ))}
                                </ul>
                            </div>

                            <div>
                                <strong style={{ fontSize: '0.9rem' }}>Suggested Formulary Medicines:</strong>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem', marginTop: '0.25rem' }}>
                                    {recommendations.suggestedMedicines.map((med, i) => (
                                        <div key={i} style={{ padding: '0.5rem', background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '6px', fontSize: '0.85rem' }}>
                                            <strong>{med.medicineName}</strong>: {med.suggestedDosage} for {med.suggestedDurationDays} days
                                        </div>
                                    ))}
                                </div>
                            </div>

                            {recommendations.precautionaryNotes.length > 0 && (
                                <div style={{ background: '#fffbeb', border: '1px solid #fef3c7', padding: '0.75rem', borderRadius: '6px', fontSize: '0.85rem', color: '#92400e' }}>
                                    <strong>Clinical Safety Precaution:</strong> {recommendations.precautionaryNotes.join('; ')}
                                </div>
                            )}

                            <div className="modal-actions" style={{ marginTop: '0.5rem' }}>
                                <Button variant="secondary" onClick={() => setShowRecModal(false)}>Dismiss</Button>
                                <Button icon={<CheckCircle2 size={16} />} onClick={handleApplyRecommendation}>
                                    Apply Diagnosis to Record
                                </Button>
                            </div>
                        </div>
                    ) : (
                        <p className="muted">Failed to generate recommendations.</p>
                    )}
                </Modal>
            )}

            {/* Modal: Create / Edit Examination */}
            {showExamModal && (
                <Modal title={editingExamId ? 'Edit Clinical Examination' : 'New Clinical Examination'} onClose={() => setShowExamModal(false)}>
                    <div className="form-grid">
                        <label>
                            Pet Patient *
                            <select
                                value={examForm.petId}
                                onChange={(e) => setExamForm({ ...examForm, petId: e.target.value })}
                                disabled={!!editingExamId}
                            >
                                {pets.map((p) => (
                                    <option key={p.id} value={p.id}>
                                        {p.name} — {p.species} ({p.breed})
                                    </option>
                                ))}
                            </select>
                        </label>

                        <label>
                            Observed Symptoms *
                            <textarea
                                value={examForm.symptoms}
                                onChange={(e) => setExamForm({ ...examForm, symptoms: e.target.value })}
                                placeholder="Describe observed physical signs, distress, behavioral changes…"
                                rows={3}
                            />
                        </label>

                        <label>
                            Clinical Examination Notes
                            <textarea
                                value={examForm.notes}
                                onChange={(e) => setExamForm({ ...examForm, notes: e.target.value })}
                                placeholder="Heart rate, temperature, palpation findings, differential notes…"
                                rows={3}
                            />
                        </label>

                        <label>
                            Examination Date &amp; Time *
                            <input
                                type="datetime-local"
                                value={examForm.examinationDate}
                                onChange={(e) => setExamForm({ ...examForm, examinationDate: e.target.value })}
                            />
                        </label>
                    </div>

                    {formError && <div className="form-error">{formError}</div>}

                    <div className="modal-actions">
                        <Button variant="secondary" onClick={() => setShowExamModal(false)}>Cancel</Button>
                        <Button onClick={handleSaveExamination}>
                            {editingExamId ? 'Save Changes' : 'Record Examination'}
                        </Button>
                    </div>
                </Modal>
            )}

            {/* Modal: Create / Edit Diagnosis */}
            {showDiagnosisModal && (
                <Modal title={editingDiagnosisId ? 'Edit Diagnosis' : 'Record Diagnosis'} onClose={() => setShowDiagnosisModal(false)}>
                    <div className="form-grid">
                        <label>
                            Condition / Disease Name *
                            <input
                                value={diagnosisForm.conditionName}
                                onChange={(e) => setDiagnosisForm({ ...diagnosisForm, conditionName: e.target.value })}
                                placeholder="e.g. Acute Gastroenteritis, Atopic Dermatitis…"
                            />
                        </label>

                        <label>
                            Clinical Description &amp; Findings
                            <textarea
                                value={diagnosisForm.description}
                                onChange={(e) => setDiagnosisForm({ ...diagnosisForm, description: e.target.value })}
                                placeholder="Detailed diagnosis justification and diagnostic tests run…"
                                rows={3}
                            />
                        </label>

                        <label>
                            Severity Classification *
                            <select
                                value={diagnosisForm.severity}
                                onChange={(e) => setDiagnosisForm({ ...diagnosisForm, severity: e.target.value as DiagnosisSeverity })}
                            >
                                <option value="Low">Low (Mild / Stable)</option>
                                <option value="Moderate">Moderate (Requires Intervention)</option>
                                <option value="High">High (Urgent / Acute)</option>
                                <option value="Critical">Critical (Immediate Emergency)</option>
                            </select>
                        </label>
                    </div>

                    {formError && <div className="form-error">{formError}</div>}

                    <div className="modal-actions">
                        <Button variant="secondary" onClick={() => setShowDiagnosisModal(false)}>Cancel</Button>
                        <Button onClick={handleSaveDiagnosis}>Save Diagnosis</Button>
                    </div>
                </Modal>
            )}

            {/* Modal: Create / Edit Treatment Record */}
            {showTreatmentModal && (
                <Modal title={editingTreatmentId ? 'Edit Treatment Procedure' : 'Add Treatment Procedure'} onClose={() => setShowTreatmentModal(false)}>
                    <div className="form-grid">
                        <label>
                            Procedure / Intervention Name *
                            <input
                                value={treatmentForm.procedureName}
                                onChange={(e) => setTreatmentForm({ ...treatmentForm, procedureName: e.target.value })}
                                placeholder="e.g. IV Fluid Therapy, Medicated Wash, Suture Dressing…"
                            />
                        </label>

                        <label>
                            Procedure Details &amp; Nursing Notes
                            <textarea
                                value={treatmentForm.notes}
                                onChange={(e) => setTreatmentForm({ ...treatmentForm, notes: e.target.value })}
                                placeholder="Protocols administered, observations during procedure, next steps…"
                                rows={3}
                            />
                        </label>
                    </div>

                    {formError && <div className="form-error">{formError}</div>}

                    <div className="modal-actions">
                        <Button variant="secondary" onClick={() => setShowTreatmentModal(false)}>Cancel</Button>
                        <Button onClick={handleSaveTreatment}>Save Procedure</Button>
                    </div>
                </Modal>
            )}

            {/* Modal: Add Prescription */}
            {showPrescriptionModal && (
                <Modal title="Issue Medication Prescription" onClose={() => setShowPrescriptionModal(null)}>
                    <div className="form-grid">
                        <label>
                            Prescribed Medicine *
                            <select
                                value={prescriptionForm.medicineId}
                                onChange={(e) => setPrescriptionForm({ ...prescriptionForm, medicineId: e.target.value })}
                            >
                                {medicines.map((m) => (
                                    <option key={m.id} value={m.id}>{m.name}</option>
                                ))}
                            </select>
                        </label>

                        <label>
                            Dosage &amp; Instructions *
                            <input
                                value={prescriptionForm.dosage}
                                onChange={(e) => setPrescriptionForm({ ...prescriptionForm, dosage: e.target.value })}
                                placeholder="e.g. 1 tablet twice daily with food, 5ml every 8 hours"
                            />
                        </label>

                        <label>
                            Duration (Days) *
                            <input
                                type="number"
                                min={1}
                                max={90}
                                value={prescriptionForm.durationDays}
                                onChange={(e) => setPrescriptionForm({ ...prescriptionForm, durationDays: parseInt(e.target.value, 10) || 1 })}
                            />
                        </label>
                    </div>

                    {formError && <div className="form-error">{formError}</div>}

                    <div className="modal-actions">
                        <Button variant="secondary" onClick={() => setShowPrescriptionModal(null)}>Cancel</Button>
                        <Button onClick={handleCreatePrescription}>Issue Prescription</Button>
                    </div>
                </Modal>
            )}
        </div>
    );
}
