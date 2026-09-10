import { useState, useEffect, useCallback } from 'react';
import {
  DollarSign, TrendingUp, BarChart3, Stethoscope, Activity,
  Calendar, RefreshCw, AlertCircle, PieChart, ShieldCheck
} from 'lucide-react';
import {
  getRevenueReport,
  getCommonConditionsReport,
  getVeterinarianWorkloadReport
} from '../services/adminApi';

export default function ClinicReportsView() {
  const [activeTab, setActiveTab] = useState('revenue'); // 'revenue' | 'conditions' | 'workload'
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  // Revenue State
  const [revenueData, setRevenueData] = useState(null);
  const [revenuePeriod, setRevenuePeriod] = useState('Monthly');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');

  // Conditions State
  const [conditionsData, setConditionsData] = useState(null);

  // Workload State
  const [workloadData, setWorkloadData] = useState(null);

  const fetchReport = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      if (activeTab === 'revenue') {
        const data = await getRevenueReport({
          fromDate: fromDate || undefined,
          toDate: toDate || undefined,
          period: revenuePeriod || undefined
        });
        setRevenueData(data);
      } else if (activeTab === 'conditions') {
        const data = await getCommonConditionsReport();
        setConditionsData(data);
      } else if (activeTab === 'workload') {
        const data = await getVeterinarianWorkloadReport({
          fromDate: fromDate || undefined,
          toDate: toDate || undefined
        });
        setWorkloadData(data);
      }
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to load clinic report analytics.');
    } finally {
      setLoading(false);
    }
  }, [activeTab, revenuePeriod, fromDate, toDate]);

  useEffect(() => {
    fetchReport();
  }, [fetchReport]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Top Tab Bar & Filter Controls */}
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
        {/* Navigation Tabs */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}>
          {[
            { id: 'revenue', label: 'Revenue Analytics', icon: <DollarSign size={16} /> },
            { id: 'conditions', label: 'Common Conditions', icon: <Activity size={16} /> },
            { id: 'workload', label: 'Veterinarian Workload', icon: <Stethoscope size={16} /> },
          ].map((tab) => {
            const active = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                style={{
                  padding: '0.5rem 1rem',
                  borderRadius: '0.5rem',
                  fontSize: '0.875rem',
                  fontWeight: '600',
                  cursor: 'pointer',
                  border: active ? '1px solid #0d9488' : '1px solid #e5e7eb',
                  backgroundColor: active ? '#f0fdfa' : '#ffffff',
                  color: active ? '#0f766e' : '#4b5563',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.375rem',
                  transition: 'all 0.15s ease'
                }}
              >
                {tab.icon} {tab.label}
              </button>
            );
          })}
        </div>

        {/* Filters if on revenue or workload */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
          {activeTab === 'revenue' && (
            <select
              value={revenuePeriod}
              onChange={(e) => setRevenuePeriod(e.target.value)}
              style={{
                padding: '0.5rem 0.75rem',
                borderRadius: '0.5rem',
                border: '1px solid #d1d5db',
                fontSize: '0.875rem',
                backgroundColor: '#fff'
              }}
            >
              <option value="Monthly">Monthly View</option>
              <option value="Weekly">Weekly View</option>
              <option value="AllTime">All Time</option>
            </select>
          )}

          <button
            onClick={fetchReport}
            title="Refresh Report"
            style={{
              padding: '0.5rem 0.75rem',
              borderRadius: '0.5rem',
              border: '1px solid #d1d5db',
              backgroundColor: '#fff',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '0.375rem',
              fontSize: '0.85rem',
              color: '#374151'
            }}
          >
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
          </button>
        </div>
      </div>

      {error && (
        <div style={{ padding: '0.875rem 1rem', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#991b1b', fontSize: '0.875rem' }}>
          <AlertCircle size={18} />
          <span>{error}</span>
        </div>
      )}

      {loading ? (
        <div style={{ padding: '4rem', textAlign: 'center', color: '#6b7280' }}>
          <RefreshCw className="animate-spin" size={28} style={{ color: '#0d9488', margin: '0 auto 0.75rem' }} />
          <p style={{ margin: 0, fontSize: '0.95rem' }}>Calculating clinic reports and analytics...</p>
        </div>
      ) : (
        <>
          {/* ═════════ REVENUE ANALYTICS TAB ═════════ */}
          {activeTab === 'revenue' && revenueData && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
              {/* Summary Cards */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1.25rem' }}>
                <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.25rem' }}>
                  <div style={{ fontSize: '0.8rem', color: '#6b7280', fontWeight: '500' }}>Total Billed Revenue</div>
                  <div style={{ fontSize: '1.875rem', fontWeight: '700', color: '#059669', marginTop: '0.25rem' }}>
                    ${revenueData.totalRevenue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </div>
                  <div style={{ fontSize: '0.8rem', color: '#6b7280', marginTop: '0.25rem' }}>From completed quotations</div>
                </div>

                <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.25rem' }}>
                  <div style={{ fontSize: '0.8rem', color: '#6b7280', fontWeight: '500' }}>Total Invoices / Quotations</div>
                  <div style={{ fontSize: '1.875rem', fontWeight: '700', color: '#111827', marginTop: '0.25rem' }}>
                    {revenueData.totalQuotations}
                  </div>
                  <div style={{ fontSize: '0.8rem', color: '#6b7280', marginTop: '0.25rem' }}>Generated quotations</div>
                </div>

                <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.25rem' }}>
                  <div style={{ fontSize: '0.8rem', color: '#6b7280', fontWeight: '500' }}>Average Quotation Value</div>
                  <div style={{ fontSize: '1.875rem', fontWeight: '700', color: '#6366f1', marginTop: '0.25rem' }}>
                    ${revenueData.averageQuotationValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </div>
                  <div style={{ fontSize: '0.8rem', color: '#6b7280', marginTop: '0.25rem' }}>Per treatment plan</div>
                </div>
              </div>

              {/* Revenue Category Breakdown */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(380px, 1fr))', gap: '1.5rem' }}>
                <div style={{ backgroundColor: '#ffffff', borderRadius: '0.875rem', border: '1px solid #e5e7eb', padding: '1.5rem' }}>
                  <h3 style={{ fontSize: '1rem', fontWeight: '600', color: '#111827', marginBottom: '1.25rem' }}>
                    Revenue by Service Category
                  </h3>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                    {(revenueData.breakdownByCategory || []).map(cat => (
                      <div key={cat.category}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.875rem', marginBottom: '0.25rem' }}>
                          <span style={{ fontWeight: '500', color: '#374151' }}>{cat.category}</span>
                          <span style={{ fontWeight: '600', color: '#111827' }}>${cat.amount.toFixed(2)} ({cat.percentage}%)</span>
                        </div>
                        <div style={{ width: '100%', height: 7, backgroundColor: '#f3f4f6', borderRadius: 4, overflow: 'hidden' }}>
                          <div style={{ width: `${Math.min(100, cat.percentage)}%`, height: '100%', backgroundColor: '#0d9488', borderRadius: 4 }} />
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Periodic Trends */}
                <div style={{ backgroundColor: '#ffffff', borderRadius: '0.875rem', border: '1px solid #e5e7eb', padding: '1.5rem' }}>
                  <h3 style={{ fontSize: '1rem', fontWeight: '600', color: '#111827', marginBottom: '1.25rem' }}>
                    Periodic Revenue Trends
                  </h3>
                  {(!revenueData.periodicTrends || revenueData.periodicTrends.length === 0) ? (
                    <div style={{ padding: '2rem', textAlign: 'center', color: '#9ca3af', fontSize: '0.875rem' }}>
                      No periodic revenue history recorded yet.
                    </div>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                      {revenueData.periodicTrends.map(trend => (
                        <div
                          key={trend.period}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            padding: '0.75rem',
                            backgroundColor: '#f9fafb',
                            borderRadius: '0.5rem'
                          }}
                        >
                          <span style={{ fontWeight: '600', color: '#111827', fontSize: '0.875rem' }}>{trend.period}</span>
                          <div style={{ textAlign: 'right' }}>
                            <div style={{ fontWeight: '700', color: '#059669', fontSize: '0.95rem' }}>
                              ${trend.amount.toFixed(2)}
                            </div>
                            <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                              {trend.quotationCount} bills
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}

          {/* ═════════ COMMON CONDITIONS TAB ═════════ */}
          {activeTab === 'conditions' && conditionsData && (
            <div style={{ backgroundColor: '#ffffff', borderRadius: '0.875rem', border: '1px solid #e5e7eb', padding: '1.5rem' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
                <div>
                  <h3 style={{ fontSize: '1.125rem', fontWeight: '700', color: '#111827', margin: 0 }}>
                    Common Veterinary Health Conditions
                  </h3>
                  <span style={{ fontSize: '0.85rem', color: '#6b7280' }}>
                    Aggregated across {conditionsData.totalRecordsAnalysed} clinic medical records
                  </span>
                </div>
              </div>

              {(!conditionsData.topConditions || conditionsData.topConditions.length === 0) ? (
                <div style={{ padding: '3rem', textAlign: 'center', color: '#9ca3af' }}>
                  No medical record diagnoses recorded yet.
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                  {conditionsData.topConditions.map((cond, idx) => (
                    <div
                      key={idx}
                      style={{
                        padding: '1.25rem',
                        borderRadius: '0.75rem',
                        border: '1px solid #e5e7eb',
                        backgroundColor: '#fafafa'
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '0.75rem' }}>
                        <div>
                          <div style={{ fontSize: '1.05rem', fontWeight: '700', color: '#111827' }}>
                            #{idx + 1} {cond.diagnosis}
                          </div>
                          <div style={{ fontSize: '0.85rem', color: '#6b7280', marginTop: '0.2rem' }}>
                            {cond.caseCount} confirmed cases ({cond.percentage}% of all clinic consultations)
                          </div>
                        </div>
                        <span style={{
                          padding: '0.25rem 0.65rem',
                          borderRadius: '9999px',
                          backgroundColor: '#e0f2fe',
                          color: '#0369a1',
                          fontSize: '0.75rem',
                          fontWeight: '700'
                        }}>
                          {cond.percentage}% Frequency
                        </span>
                      </div>

                      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '1rem', marginTop: '0.5rem', backgroundColor: '#fff', padding: '0.875rem', borderRadius: '0.5rem', border: '1px solid #f3f4f6' }}>
                        <div>
                          <div style={{ fontSize: '0.75rem', fontWeight: '600', color: '#6b7280', textTransform: 'uppercase', marginBottom: '0.25rem' }}>
                            Common Treatment Protocols
                          </div>
                          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.35rem' }}>
                            {(cond.commonTreatments || []).map((t, tidx) => (
                              <span key={tidx} style={{ padding: '0.15rem 0.5rem', backgroundColor: '#f0fdf4', color: '#15803d', borderRadius: '0.25rem', fontSize: '0.8rem', fontWeight: '500' }}>
                                {t}
                              </span>
                            ))}
                          </div>
                        </div>

                        <div>
                          <div style={{ fontSize: '0.75rem', fontWeight: '600', color: '#6b7280', textTransform: 'uppercase', marginBottom: '0.25rem' }}>
                            Affected Species
                          </div>
                          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.35rem' }}>
                            {(cond.affectedSpecies || []).map((s, sidx) => (
                              <span key={sidx} style={{ padding: '0.15rem 0.5rem', backgroundColor: '#f5f3ff', color: '#6d28d9', borderRadius: '0.25rem', fontSize: '0.8rem', fontWeight: '500' }}>
                                {s}
                              </span>
                            ))}
                          </div>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* ═════════ VETERINARIAN WORKLOAD TAB ═════════ */}
          {activeTab === 'workload' && workloadData && (
            <div style={{ backgroundColor: '#ffffff', borderRadius: '0.875rem', border: '1px solid #e5e7eb', overflow: 'hidden' }}>
              <div style={{ padding: '1.25rem', borderBottom: '1px solid #e5e7eb' }}>
                <h3 style={{ fontSize: '1rem', fontWeight: '600', color: '#111827', margin: 0 }}>
                  Veterinarian Utilization & Performance
                </h3>
              </div>

              {(!workloadData.veterinarians || workloadData.veterinarians.length === 0) ? (
                <div style={{ padding: '3rem', textAlign: 'center', color: '#9ca3af' }}>
                  No veterinarians registered in your organization yet.
                </div>
              ) : (
                <div style={{ overflowX: 'auto' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
                    <thead>
                      <tr style={{ backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                        <th style={{ padding: '0.75rem 1.25rem' }}>Veterinarian</th>
                        <th style={{ padding: '0.75rem 1.25rem' }}>Specialisation</th>
                        <th style={{ padding: '0.75rem 1.25rem' }}>Completed</th>
                        <th style={{ padding: '0.75rem 1.25rem' }}>Upcoming</th>
                        <th style={{ padding: '0.75rem 1.25rem' }}>Cancelled</th>
                        <th style={{ padding: '0.75rem 1.25rem' }}>Total Consultations</th>
                        <th style={{ padding: '0.75rem 1.25rem' }}>Completion Rate</th>
                      </tr>
                    </thead>
                    <tbody>
                      {workloadData.veterinarians.map((vet) => {
                        const total = vet.totalAppointments || 0;
                        const rate = total > 0 ? Math.round((vet.completedAppointments / total) * 100) : 0;
                        return (
                          <tr key={vet.veterinarianId} style={{ borderBottom: '1px solid #f3f4f6' }}>
                            <td style={{ padding: '1rem 1.25rem', fontWeight: '600', color: '#111827' }}>
                              {vet.veterinarianName}
                            </td>
                            <td style={{ padding: '1rem 1.25rem', color: '#4b5563' }}>
                              {vet.specialisation}
                            </td>
                            <td style={{ padding: '1rem 1.25rem', fontWeight: '600', color: '#16a34a' }}>
                              {vet.completedAppointments}
                            </td>
                            <td style={{ padding: '1rem 1.25rem', fontWeight: '600', color: '#0284c7' }}>
                              {vet.upcomingAppointments}
                            </td>
                            <td style={{ padding: '1rem 1.25rem', color: '#991b1b' }}>
                              {vet.cancelledAppointments}
                            </td>
                            <td style={{ padding: '1rem 1.25rem', fontWeight: '700', color: '#111827' }}>
                              {vet.totalAppointments}
                            </td>
                            <td style={{ padding: '1rem 1.25rem' }}>
                              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                <span style={{ fontWeight: '600', color: '#374151', minWidth: '35px' }}>{rate}%</span>
                                <div style={{ width: 80, height: 6, backgroundColor: '#e5e7eb', borderRadius: 3, overflow: 'hidden' }}>
                                  <div style={{ width: `${rate}%`, height: '100%', backgroundColor: '#0d9488', borderRadius: 3 }} />
                                </div>
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
          )}
        </>
      )}
    </div>
  );
}
