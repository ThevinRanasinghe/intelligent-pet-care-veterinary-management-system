import { useState, useEffect, useCallback } from 'react';
import {
  Sparkles, CheckCircle2, XCircle, AlertTriangle, Clock, RefreshCw,
  FileText, ShieldCheck, DollarSign, Calendar, Stethoscope, User,
  MessageSquare, ChevronRight, History, ArrowRight, Eye, Check, X
} from 'lucide-react';
import {
  getProposals,
  getProposalById,
  approveProposal,
  rejectProposal,
  requestProposalRevision,
  getProposalHistory
} from '../services/adminApi';

export default function ApprovalManagementView() {
  const [proposals, setProposals] = useState([]);
  const [loading, setLoading] = useState(true);
  const [statusTab, setStatusTab] = useState('Pending'); // 'Pending' | 'Approved' | 'Rejected' | 'RevisionRequested' | ''
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  // Selected Proposal for Review Modal
  const [activeProposal, setActiveProposal] = useState(null);
  const [detailsLoading, setDetailsLoading] = useState(false);

  // Decision Modal States
  const [decisionType, setDecisionType] = useState(null); // 'approve' | 'reject' | 'revision' | null
  const [decisionNote, setDecisionNote] = useState('');
  const [decisionSubmitting, setDecisionSubmitting] = useState(false);
  const [decisionError, setDecisionError] = useState('');

  // History Modal State
  const [historyModalOpen, setHistoryModalOpen] = useState(false);
  const [historyLogs, setHistoryLogs] = useState([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  const fetchProposalsList = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getProposals({ status: statusTab || undefined });
      setProposals(data);
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to load AI proposals queue.');
    } finally {
      setLoading(false);
    }
  }, [statusTab]);

  useEffect(() => {
    fetchProposalsList();
  }, [fetchProposalsList]);

  const handleOpenReview = async (proposalId) => {
    setDetailsLoading(true);
    setDecisionError('');
    try {
      const fullDetails = await getProposalById(proposalId);
      setActiveProposal(fullDetails);
    } catch (err) {
      alert(err.response?.data?.message || 'Failed to load proposal details.');
    } finally {
      setDetailsLoading(false);
    }
  };

  const handleOpenHistory = async (proposalId) => {
    setHistoryLoading(true);
    setHistoryModalOpen(true);
    try {
      const logs = await getProposalHistory(proposalId);
      setHistoryLogs(logs);
    } catch (err) {
      alert(err.response?.data?.message || 'Failed to load approval history.');
    } finally {
      setHistoryLoading(false);
    }
  };

  const handleExecuteDecision = async (e) => {
    e.preventDefault();
    if (!activeProposal || !decisionType) return;
    setDecisionError('');

    if ((decisionType === 'reject' || decisionType === 'revision') && !decisionNote.trim()) {
      setDecisionError('A clear justification note is required for rejections and revision requests.');
      return;
    }

    setDecisionSubmitting(true);
    try {
      if (decisionType === 'approve') {
        await approveProposal(activeProposal.id, { comment: decisionNote.trim() });
        setSuccessMsg('Proposal approved successfully! Appointment booked, veterinarian assigned, and medicine reserved.');
      } else if (decisionType === 'reject') {
        await rejectProposal(activeProposal.id, { reason: decisionNote.trim() });
        setSuccessMsg('Proposal marked as rejected.');
      } else if (decisionType === 'revision') {
        await requestProposalRevision(activeProposal.id, { reason: decisionNote.trim() });
        setSuccessMsg('Proposal returned for revision.');
      }

      setDecisionType(null);
      setDecisionNote('');
      setActiveProposal(null);
      fetchProposalsList();
      setTimeout(() => setSuccessMsg(''), 5000);
    } catch (err) {
      setDecisionError(err.response?.data?.message || 'Transaction failed. Please review errors and try again.');
    } finally {
      setDecisionSubmitting(false);
    }
  };

  const getUrgencyBadge = (urgency) => {
    switch (urgency) {
      case 'Urgent':
        return { bg: '#fee2e2', text: '#b91c1c', border: '#fca5a5' };
      case 'Moderate':
        return { bg: '#fef3c7', text: '#b45309', border: '#fde68a' };
      default:
        return { bg: '#f0fdf4', text: '#15803d', border: '#bbf7d0' };
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Top Header & Status Tabs */}
      <div style={{
        backgroundColor: '#ffffff',
        borderRadius: '0.875rem',
        border: '1px solid #e5e7eb',
        padding: '1.25rem',
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '1rem'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}>
          {[
            { id: 'Pending', label: 'Pending Review' },
            { id: 'Approved', label: 'Approved' },
            { id: 'RevisionRequested', label: 'In Revision' },
            { id: 'Rejected', label: 'Rejected' },
            { id: '', label: 'All Proposals' },
          ].map((tab) => {
            const active = statusTab === tab.id;
            return (
              <button
                key={tab.id}
                onClick={() => setStatusTab(tab.id)}
                style={{
                  padding: '0.45rem 0.95rem',
                  borderRadius: '0.5rem',
                  fontSize: '0.85rem',
                  fontWeight: '600',
                  cursor: 'pointer',
                  border: active ? '1px solid #0d9488' : '1px solid #e5e7eb',
                  backgroundColor: active ? '#f0fdfa' : '#ffffff',
                  color: active ? '#0f766e' : '#4b5563',
                  transition: 'all 0.15s ease'
                }}
              >
                {tab.label}
              </button>
            );
          })}
        </div>

        <button
          onClick={fetchProposalsList}
          title="Refresh Queue"
          style={{
            padding: '0.5rem 0.75rem',
            borderRadius: '0.5rem',
            border: '1px solid #d1d5db',
            backgroundColor: '#ffffff',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '0.375rem',
            fontSize: '0.85rem',
            color: '#374151'
          }}
        >
          <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh Queue
        </button>
      </div>

      {/* Notifications */}
      {successMsg && (
        <div style={{ padding: '0.875rem 1rem', backgroundColor: '#ecfdf5', border: '1px solid #a7f3d0', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#065f46', fontSize: '0.875rem' }}>
          <CheckCircle2 size={18} />
          <span>{successMsg}</span>
        </div>
      )}
      {error && (
        <div style={{ padding: '0.875rem 1rem', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#991b1b', fontSize: '0.875rem' }}>
          <AlertTriangle size={18} />
          <span>{error}</span>
        </div>
      )}

      {/* Proposals Queue Table / Cards */}
      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.875rem', border: '1px solid #e5e7eb', overflow: 'hidden' }}>
        <div style={{ padding: '1.25rem', borderBottom: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Sparkles size={18} style={{ color: '#d97706' }} />
            <h3 style={{ fontSize: '1rem', fontWeight: '600', color: '#111827', margin: 0 }}>
              AI Consultation Proposals ({proposals.length})
            </h3>
          </div>
          <span style={{ fontSize: '0.8rem', color: '#6b7280' }}>
            Decision Support & Atomic Verification
          </span>
        </div>

        {loading ? (
          <div style={{ padding: '3.5rem', textAlign: 'center', color: '#6b7280' }}>
            <RefreshCw className="animate-spin" size={24} style={{ color: '#0d9488', margin: '0 auto 0.5rem' }} />
            <p style={{ margin: 0, fontSize: '0.875rem' }}>Loading proposals queue...</p>
          </div>
        ) : proposals.length === 0 ? (
          <div style={{ padding: '3.5rem 2rem', textAlign: 'center', color: '#6b7280' }}>
            <Sparkles size={36} style={{ color: '#9ca3af', margin: '0 auto 0.75rem' }} />
            <h4 style={{ fontWeight: '600', color: '#374151', margin: '0 0 0.25rem' }}>No proposals in this category</h4>
            <p style={{ fontSize: '0.85rem', color: '#9ca3af', margin: 0 }}>
              AI Consultation requests generated by the multi-agent workflow will appear here for manager sign-off.
            </p>
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
              <thead>
                <tr style={{ backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Pet & Owner</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Urgency</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Symptoms & AI Recommendation</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Proposed Doctor</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Schedule Slot</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Estimate</th>
                  <th style={{ padding: '0.75rem 1.25rem' }}>Status</th>
                  <th style={{ padding: '0.75rem 1.25rem', textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {proposals.map((p) => {
                  const urg = getUrgencyBadge(p.urgency);
                  const isPending = p.status === 'Pending';
                  return (
                    <tr key={p.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <div>
                          <div style={{ fontWeight: '600', color: '#111827' }}>{p.petName}</div>
                          <div style={{ fontSize: '0.8rem', color: '#6b7280' }}>Owner: {p.ownerName}</div>
                        </div>
                      </td>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <span style={{
                          padding: '0.2rem 0.55rem',
                          borderRadius: '9999px',
                          backgroundColor: urg.bg,
                          color: urg.text,
                          border: `1px solid ${urg.border}`,
                          fontSize: '0.75rem',
                          fontWeight: '700'
                        }}>
                          {p.urgency}
                        </span>
                      </td>
                      <td style={{ padding: '1rem 1.25rem', maxWidth: '280px' }}>
                        <div style={{ color: '#374151', fontSize: '0.85rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={p.symptomsSummary}>
                          {p.symptomsSummary}
                        </div>
                      </td>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', color: '#0d9488', fontWeight: '500' }}>
                          <Stethoscope size={14} />
                          <span>{p.proposedVeterinarianName}</span>
                        </div>
                      </td>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <div style={{ fontSize: '0.85rem', color: '#374151' }}>{p.proposedDate}</div>
                        <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>{p.proposedTime}</div>
                      </td>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <div style={{ fontWeight: '700', color: '#059669' }}>
                          ${p.quotationTotal.toFixed(2)}
                        </div>
                        <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                          Limit: ${p.budgetLimit.toFixed(2)}
                        </div>
                      </td>
                      <td style={{ padding: '1rem 1.25rem' }}>
                        <span style={{
                          padding: '0.2rem 0.55rem',
                          borderRadius: '9999px',
                          backgroundColor: p.status === 'Approved' ? '#dcfce7' : p.status === 'Pending' ? '#fffbeb' : p.status === 'Rejected' ? '#fee2e2' : '#f3f4f6',
                          color: p.status === 'Approved' ? '#15803d' : p.status === 'Pending' ? '#b45309' : p.status === 'Rejected' ? '#b91c1c' : '#4b5563',
                          fontSize: '0.75rem',
                          fontWeight: '700'
                        }}>
                          {p.status}
                        </span>
                      </td>
                      <td style={{ padding: '1rem 1.25rem', textAlign: 'right' }}>
                        <div style={{ display: 'inline-flex', alignItems: 'center', gap: '0.5rem' }}>
                          <button
                            onClick={() => handleOpenReview(p.id)}
                            style={{
                              padding: '0.4rem 0.75rem',
                              borderRadius: '0.375rem',
                              backgroundColor: isPending ? '#0d9488' : '#ffffff',
                              color: isPending ? '#ffffff' : '#374151',
                              border: isPending ? 'none' : '1px solid #d1d5db',
                              fontSize: '0.8rem',
                              fontWeight: '600',
                              cursor: 'pointer',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '0.25rem'
                            }}
                          >
                            <Eye size={13} /> {isPending ? 'Review & Decide' : 'Inspect'}
                          </button>
                          <button
                            onClick={() => handleOpenHistory(p.id)}
                            title="Decision History"
                            style={{
                              padding: '0.4rem',
                              borderRadius: '0.375rem',
                              border: '1px solid #d1d5db',
                              backgroundColor: '#fff',
                              color: '#6b7280',
                              cursor: 'pointer',
                              display: 'inline-flex',
                              alignItems: 'center'
                            }}
                          >
                            <History size={14} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Proposal Review Modal */}
      {activeProposal && (
        <div style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: 'rgba(0,0,0,0.6)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 50,
          padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: '0.875rem',
            width: '100%',
            maxWidth: '840px',
            maxHeight: '90vh',
            boxShadow: '0 25px 50px -12px rgba(0,0,0,0.25)',
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden'
          }}>
            {/* Modal Header */}
            <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Sparkles size={22} style={{ color: '#d97706' }} />
                <div>
                  <h3 style={{ fontSize: '1.125rem', fontWeight: '700', color: '#111827', margin: 0 }}>
                    AI Consultation Proposal Review
                  </h3>
                  <span style={{ fontSize: '0.8rem', color: '#6b7280' }}>
                    Consultation #{activeProposal.consultationRequestId || activeProposal.id.slice(0, 8)}
                  </span>
                </div>
              </div>
              <button
                onClick={() => setActiveProposal(null)}
                style={{ background: 'none', border: 'none', color: '#6b7280', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            {/* Scrollable Content */}
            <div style={{ padding: '1.5rem', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
              {/* Decision Support Banner */}
              <div style={{
                backgroundColor: '#fffbeb',
                border: '1px solid #fde68a',
                borderRadius: '0.75rem',
                padding: '1rem 1.25rem',
                display: 'flex',
                alignItems: 'flex-start',
                gap: '0.75rem'
              }}>
                <AlertTriangle size={20} style={{ color: '#b45309', flexShrink: 0, marginTop: '0.15rem' }} />
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.25rem' }}>
                    <span style={{ fontWeight: '700', color: '#92400e', fontSize: '0.9rem' }}>
                      Clinical Decision Support:
                    </span>
                    <span style={{
                      padding: '0.15rem 0.5rem',
                      borderRadius: '9999px',
                      backgroundColor: getUrgencyBadge(activeProposal.urgency).bg,
                      color: getUrgencyBadge(activeProposal.urgency).text,
                      fontSize: '0.75rem',
                      fontWeight: '700'
                    }}>
                      {activeProposal.urgency} Urgency
                    </span>
                  </div>
                  <p style={{ margin: 0, color: '#78350f', fontSize: '0.875rem', lineHeight: '1.4' }}>
                    {activeProposal.preliminaryRecommendation}
                  </p>
                </div>
              </div>

              {/* Patient & Doctor Match */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '1rem', backgroundColor: '#f9fafb', padding: '1rem', borderRadius: '0.75rem' }}>
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Patient (Pet)</div>
                  <div style={{ fontSize: '1rem', fontWeight: '700', color: '#111827', marginTop: '0.15rem' }}>{activeProposal.petName}</div>
                  <div style={{ fontSize: '0.8rem', color: '#4b5563' }}>Owner: {activeProposal.ownerName}</div>
                </div>

                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Recommended Veterinarian</div>
                  <div style={{ fontSize: '1rem', fontWeight: '700', color: '#0d9488', marginTop: '0.15rem' }}>{activeProposal.proposedVeterinarianName}</div>
                  <div style={{ fontSize: '0.8rem', color: '#4b5563' }}>Assigned for Consultation</div>
                </div>

                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Proposed Appointment Slot</div>
                  <div style={{ fontSize: '0.95rem', fontWeight: '600', color: '#111827', marginTop: '0.15rem' }}>
                    {activeProposal.proposedDate}
                  </div>
                  <div style={{ fontSize: '0.8rem', color: '#4b5563' }}>{activeProposal.proposedTime}</div>
                </div>

                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', textTransform: 'uppercase', fontWeight: '600' }}>Quotation vs Budget</div>
                  <div style={{ fontSize: '1rem', fontWeight: '700', color: '#059669', marginTop: '0.15rem' }}>
                    ${activeProposal.quotationTotal.toFixed(2)}
                  </div>
                  <div style={{ fontSize: '0.8rem', color: activeProposal.quotationTotal <= activeProposal.budgetLimit ? '#15803d' : '#dc2626' }}>
                    Limit: ${activeProposal.budgetLimit.toFixed(2)} {activeProposal.quotationTotal <= activeProposal.budgetLimit ? '✓ In Budget' : '⚠ Exceeds'}
                  </div>
                </div>
              </div>

              {/* Deterministic System Validation Checks */}
              <div>
                <h4 style={{ fontSize: '0.9rem', fontWeight: '700', color: '#111827', marginBottom: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                  <ShieldCheck size={16} style={{ color: '#0d9488' }} /> Deterministic System Validation Checks
                </h4>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                  {(activeProposal.validationChecks || []).map((check) => (
                    <div
                      key={check.key}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '0.75rem 1rem',
                        borderRadius: '0.5rem',
                        border: `1px solid ${check.passed ? '#bbf7d0' : '#fecaca'}`,
                        backgroundColor: check.passed ? '#f0fdf4' : '#fef2f2'
                      }}
                    >
                      <div>
                        <div style={{ fontWeight: '600', fontSize: '0.85rem', color: check.passed ? '#166534' : '#991b1b' }}>
                          {check.label}
                        </div>
                        <div style={{ fontSize: '0.75rem', color: '#4b5563' }}>{check.detail}</div>
                      </div>
                      {check.passed ? (
                        <CheckCircle2 size={18} style={{ color: '#16a34a', flexShrink: 0 }} />
                      ) : (
                        <XCircle size={18} style={{ color: '#dc2626', flexShrink: 0 }} />
                      )}
                    </div>
                  ))}
                </div>
              </div>

              {/* AI Workflow Execution Steps Timeline */}
              <div>
                <h4 style={{ fontSize: '0.9rem', fontWeight: '700', color: '#111827', marginBottom: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                  <Clock size={16} style={{ color: '#6366f1' }} /> AI Multi-Agent Workflow Execution
                </h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', borderLeft: '2px solid #e0e7ff', paddingLeft: '1rem', marginLeft: '0.5rem' }}>
                  {(activeProposal.executionSteps || []).map((step) => (
                    <div key={step.id} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '0.85rem' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        <div style={{ width: 8, height: 8, borderRadius: '50%', backgroundColor: step.status === 'Completed' ? '#10b981' : '#f59e0b', marginLeft: '-1.3rem' }} />
                        <span style={{ fontWeight: '600', color: '#374151' }}>{step.name}</span>
                        <span style={{ color: '#6b7280', fontSize: '0.8rem' }}>({step.responsibility})</span>
                      </div>
                      <span style={{
                        padding: '0.15rem 0.5rem',
                        borderRadius: '9999px',
                        backgroundColor: step.status === 'Completed' ? '#dcfce7' : '#fef3c7',
                        color: step.status === 'Completed' ? '#15803d' : '#b45309',
                        fontSize: '0.75rem',
                        fontWeight: '700'
                      }}>
                        {step.status}
                      </span>
                    </div>
                  ))}
                </div>
              </div>

              {/* Symptoms & Treatment Details */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
                <div style={{ padding: '0.875rem', backgroundColor: '#f9fafb', borderRadius: '0.5rem' }}>
                  <div style={{ fontSize: '0.75rem', fontWeight: '700', color: '#6b7280', textTransform: 'uppercase', marginBottom: '0.25rem' }}>
                    Reported Symptoms
                  </div>
                  <div style={{ fontSize: '0.875rem', color: '#374151', lineHeight: '1.4' }}>
                    {activeProposal.symptomsSummary}
                  </div>
                </div>

                <div style={{ padding: '0.875rem', backgroundColor: '#f9fafb', borderRadius: '0.5rem' }}>
                  <div style={{ fontSize: '0.75rem', fontWeight: '700', color: '#6b7280', textTransform: 'uppercase', marginBottom: '0.25rem' }}>
                    Proposed Treatment & Medicine
                  </div>
                  <div style={{ fontSize: '0.875rem', color: '#374151', lineHeight: '1.4' }}>
                    {activeProposal.proposedTreatment}
                  </div>
                  <div style={{ fontSize: '0.75rem', color: '#059669', fontWeight: '600', marginTop: '0.25rem' }}>
                    Status: {activeProposal.medicineAvailabilityStatus}
                  </div>
                </div>
              </div>

              {/* Current Status / Review Info if already decided */}
              {activeProposal.status !== 'Pending' && (
                <div style={{ padding: '0.875rem', backgroundColor: '#f3f4f6', borderRadius: '0.5rem', fontSize: '0.85rem' }}>
                  <div style={{ fontWeight: '600', color: '#374151' }}>
                    Decision: {activeProposal.status} by {activeProposal.reviewedBy || 'Manager'}
                  </div>
                  {activeProposal.decisionNote && (
                    <div style={{ color: '#4b5563', marginTop: '0.25rem' }}>
                      Notes: {activeProposal.decisionNote}
                    </div>
                  )}
                </div>
              )}
            </div>

            {/* Modal Footer Actions */}
            <div style={{ padding: '1rem 1.5rem', borderTop: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center', backgroundColor: '#f9fafb' }}>
              <button
                type="button"
                onClick={() => handleOpenHistory(activeProposal.id)}
                style={{
                  padding: '0.5rem 0.85rem',
                  borderRadius: '0.5rem',
                  border: '1px solid #d1d5db',
                  backgroundColor: '#fff',
                  color: '#374151',
                  fontSize: '0.85rem',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.375rem'
                }}
              >
                <History size={15} /> Audit History
              </button>

              <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                <button
                  type="button"
                  onClick={() => setActiveProposal(null)}
                  style={{
                    padding: '0.5rem 1rem',
                    borderRadius: '0.5rem',
                    border: '1px solid #d1d5db',
                    backgroundColor: '#fff',
                    color: '#374151',
                    fontSize: '0.875rem',
                    cursor: 'pointer'
                  }}
                >
                  Close
                </button>

                {activeProposal.status === 'Pending' && (
                  <>
                    <button
                      type="button"
                      onClick={() => {
                        setDecisionType('revision');
                        setDecisionNote('');
                      }}
                      style={{
                        padding: '0.5rem 1rem',
                        borderRadius: '0.5rem',
                        border: '1px solid #fde68a',
                        backgroundColor: '#fffbeb',
                        color: '#b45309',
                        fontWeight: '600',
                        fontSize: '0.875rem',
                        cursor: 'pointer'
                      }}
                    >
                      Request Revision
                    </button>

                    <button
                      type="button"
                      onClick={() => {
                        setDecisionType('reject');
                        setDecisionNote('');
                      }}
                      style={{
                        padding: '0.5rem 1rem',
                        borderRadius: '0.5rem',
                        border: '1px solid #fecaca',
                        backgroundColor: '#fef2f2',
                        color: '#dc2626',
                        fontWeight: '600',
                        fontSize: '0.875rem',
                        cursor: 'pointer'
                      }}
                    >
                      Reject
                    </button>

                    <button
                      type="button"
                      onClick={() => {
                        setDecisionType('approve');
                        setDecisionNote('');
                      }}
                      style={{
                        padding: '0.5rem 1.25rem',
                        borderRadius: '0.5rem',
                        border: 'none',
                        backgroundColor: '#0d9488',
                        color: '#ffffff',
                        fontWeight: '600',
                        fontSize: '0.875rem',
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.375rem',
                        boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
                      }}
                    >
                      <Check size={16} /> Approve & Execute
                    </button>
                  </>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Decision Justification Modal */}
      {decisionType && (
        <div style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: 'rgba(0,0,0,0.6)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 60,
          padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: '0.875rem',
            width: '100%',
            maxWidth: '500px',
            boxShadow: '0 25px 50px -12px rgba(0,0,0,0.25)',
            overflow: 'hidden'
          }}>
            <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h3 style={{ fontSize: '1.125rem', fontWeight: '700', color: '#111827', margin: 0 }}>
                {decisionType === 'approve' && 'Confirm Proposal Approval'}
                {decisionType === 'reject' && 'Reject Proposal with Reason'}
                {decisionType === 'revision' && 'Request Revision Instructions'}
              </h3>
              <button
                onClick={() => setDecisionType(null)}
                style={{ background: 'none', border: 'none', color: '#6b7280', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleExecuteDecision} style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              {decisionError && (
                <div style={{ padding: '0.75rem', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '0.5rem', color: '#b91c1c', fontSize: '0.85rem' }}>
                  {decisionError}
                </div>
              )}

              {decisionType === 'approve' && (
                <div style={{ padding: '0.875rem', backgroundColor: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: '0.5rem', color: '#166534', fontSize: '0.875rem' }}>
                  <strong>Atomic Transaction Details:</strong>
                  <ul style={{ margin: '0.5rem 0 0 1.25rem', padding: 0 }}>
                    <li>Finalizes appointment and reserves selected slot.</li>
                    <li>Officially assigns veterinarian {activeProposal?.proposedVeterinarianName}.</li>
                    <li>Reserves necessary pharmaceutical inventory.</li>
                    <li>Approves quotation (${activeProposal?.quotationTotal.toFixed(2)}).</li>
                  </ul>
                </div>
              )}

              <div>
                <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: '600', color: '#374151', marginBottom: '0.375rem' }}>
                  {decisionType === 'approve' ? 'Approval Note (Optional)' : 'Documented Justification *'}
                </label>
                <textarea
                  value={decisionNote}
                  onChange={(e) => setDecisionNote(e.target.value)}
                  rows={4}
                  placeholder={
                    decisionType === 'approve'
                      ? 'e.g. Cleared for consultation; room 2 prepped.'
                      : decisionType === 'reject'
                      ? 'e.g. Quotation exceeds clinic emergency guidelines or vet schedule conflict.'
                      : 'e.g. Please adjust recommended medication dosage and reduce quotation budget.'
                  }
                  style={{
                    width: '100%',
                    padding: '0.65rem 0.75rem',
                    borderRadius: '0.5rem',
                    border: '1px solid #d1d5db',
                    fontSize: '0.875rem'
                  }}
                  required={decisionType !== 'approve'}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '0.5rem' }}>
                <button
                  type="button"
                  onClick={() => setDecisionType(null)}
                  style={{
                    padding: '0.55rem 1rem',
                    borderRadius: '0.5rem',
                    border: '1px solid #d1d5db',
                    backgroundColor: '#fff',
                    color: '#374151',
                    fontSize: '0.875rem',
                    cursor: 'pointer'
                  }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={decisionSubmitting}
                  style={{
                    padding: '0.55rem 1.25rem',
                    borderRadius: '0.5rem',
                    border: 'none',
                    backgroundColor: decisionType === 'approve' ? '#0d9488' : decisionType === 'reject' ? '#dc2626' : '#d97706',
                    color: '#fff',
                    fontWeight: '600',
                    fontSize: '0.875rem',
                    cursor: decisionSubmitting ? 'not-allowed' : 'pointer',
                    opacity: decisionSubmitting ? 0.7 : 1
                  }}
                >
                  {decisionSubmitting ? 'Processing Transaction...' : decisionType === 'approve' ? 'Confirm Approval' : decisionType === 'reject' ? 'Confirm Rejection' : 'Submit Revision'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* History Modal */}
      {historyModalOpen && (
        <div style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 60,
          padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#ffffff',
            borderRadius: '0.875rem',
            width: '100%',
            maxWidth: '560px',
            boxShadow: '0 25px 50px -12px rgba(0,0,0,0.25)',
            overflow: 'hidden'
          }}>
            <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <History size={18} style={{ color: '#0d9488' }} />
                <h3 style={{ fontSize: '1.125rem', fontWeight: '700', color: '#111827', margin: 0 }}>
                  Proposal Decision History
                </h3>
              </div>
              <button
                onClick={() => setHistoryModalOpen(false)}
                style={{ background: 'none', border: 'none', color: '#6b7280', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <div style={{ padding: '1.5rem', maxHeight: '60vh', overflowY: 'auto' }}>
              {historyLoading ? (
                <div style={{ padding: '2rem', textAlign: 'center', color: '#6b7280' }}>
                  <RefreshCw className="animate-spin" size={20} style={{ color: '#0d9488', margin: '0 auto 0.5rem' }} />
                  <p style={{ margin: 0, fontSize: '0.875rem' }}>Loading decision timeline...</p>
                </div>
              ) : historyLogs.length === 0 ? (
                <div style={{ padding: '2rem', textAlign: 'center', color: '#9ca3af', fontSize: '0.875rem' }}>
                  No historical decisions logged yet.
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                  {historyLogs.map((log) => (
                    <div key={log.id} style={{ padding: '0.875rem', borderRadius: '0.5rem', backgroundColor: '#f9fafb', border: '1px solid #e5e7eb' }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.375rem' }}>
                        <div style={{ fontWeight: '600', color: '#111827', fontSize: '0.875rem' }}>
                          {log.previousStatus} → <span style={{ color: '#0d9488' }}>{log.newStatus}</span>
                        </div>
                        <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                          {new Date(log.changedAt).toLocaleString()}
                        </div>
                      </div>
                      {log.reason && (
                        <div style={{ fontSize: '0.8rem', color: '#4b5563' }}>
                          Note: "{log.reason}"
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div style={{ padding: '1rem 1.5rem', borderTop: '1px solid #e5e7eb', display: 'flex', justifyContent: 'flex-end' }}>
              <button
                onClick={() => setHistoryModalOpen(false)}
                style={{
                  padding: '0.5rem 1rem',
                  borderRadius: '0.5rem',
                  border: '1px solid #d1d5db',
                  backgroundColor: '#fff',
                  color: '#374151',
                  fontSize: '0.875rem',
                  cursor: 'pointer'
                }}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
