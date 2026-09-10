import { useState, useEffect, useCallback } from 'react';
import {
  Calendar, Clock, AlertTriangle, Users, Stethoscope, DollarSign,
  TrendingUp, Activity, CheckCircle, ArrowUpRight, Sparkles, RefreshCw
} from 'lucide-react';
import { getClinicDashboardSummary } from '../services/adminApi';

export default function ClinicManagerOverviewView({ onNavigate }) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const fetchDashboard = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const result = await getClinicDashboardSummary();
      setData(result);
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to load clinic dashboard metrics.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchDashboard();
  }, [fetchDashboard]);

  if (loading) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', padding: '4rem 2rem' }}>
        <RefreshCw className="animate-spin" size={32} style={{ color: '#0d9488', marginBottom: '1rem' }} />
        <p style={{ color: '#6b7280', fontSize: '0.95rem' }}>Loading live clinic operational metrics...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div style={{ backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '0.75rem', padding: '1.5rem', margin: '1rem 0' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#b91c1c', fontWeight: '600' }}>
          <AlertTriangle size={20} />
          <span>Failed to load overview</span>
        </div>
        <p style={{ color: '#7f1d1d', marginTop: '0.5rem', fontSize: '0.9rem' }}>{error}</p>
        <button
          onClick={fetchDashboard}
          style={{ marginTop: '1rem', padding: '0.5rem 1rem', backgroundColor: '#dc2626', color: '#fff', borderRadius: '0.5rem', border: 'none', cursor: 'pointer', fontWeight: '500' }}
        >
          Try Again
        </button>
      </div>
    );
  }

  const statCards = [
    {
      title: "Today's Appointments",
      value: data?.todayAppointmentsCount ?? 0,
      icon: <Calendar size={22} style={{ color: '#0284c7' }} />,
      bg: '#f0f9ff',
      border: '#bae6fd',
      sub: 'Scheduled for today',
      action: () => onNavigate?.('appointments')
    },
    {
      title: 'Pending AI Proposals',
      value: data?.pendingProposalsCount ?? 0,
      icon: <Sparkles size={22} style={{ color: '#d97706' }} />,
      bg: '#fffbeb',
      border: '#fde68a',
      sub: 'Requires manager approval',
      highlight: (data?.pendingProposalsCount ?? 0) > 0,
      action: () => onNavigate?.('approvals')
    },
    {
      title: 'Upcoming Consultations',
      value: data?.upcomingAppointmentsCount ?? 0,
      icon: <Clock size={22} style={{ color: '#16a34a' }} />,
      bg: '#f0fdf4',
      border: '#bbf7d0',
      sub: 'Next 7 days',
      action: () => onNavigate?.('appointments')
    },
    {
      title: 'Active Veterinarians',
      value: data?.activeVeterinariansCount ?? 0,
      icon: <Stethoscope size={22} style={{ color: '#7c3aed' }} />,
      bg: '#f5f3ff',
      border: '#ddd6fe',
      sub: 'Available doctors',
      action: () => onNavigate?.('schedules')
    },
    {
      title: 'Clinic Staff Team',
      value: data?.totalStaffCount ?? 0,
      icon: <Users size={22} style={{ color: '#0d9488' }} />,
      bg: '#f0fdfa',
      border: '#99f6e4',
      sub: 'Vets & inventory officers',
      action: () => onNavigate?.('staff')
    },
    {
      title: 'Total Revenue (Completed)',
      value: `$${(data?.totalRevenue ?? 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`,
      icon: <DollarSign size={22} style={{ color: '#059669' }} />,
      bg: '#ecfdf5',
      border: '#a7f3d0',
      sub: 'Approved billing',
      action: () => onNavigate?.('reports')
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.75rem' }}>
      {/* Top Banner Alert for Pending Proposals */}
      {(data?.pendingProposalsCount ?? 0) > 0 && (
        <div style={{
          backgroundColor: '#fffbeb',
          border: '1px solid #fcd34d',
          borderRadius: '0.75rem',
          padding: '1rem 1.25rem',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '1rem'
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <div style={{ width: 36, height: 36, borderRadius: '50%', backgroundColor: '#fef3c7', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Sparkles size={20} style={{ color: '#b45309' }} />
            </div>
            <div>
              <div style={{ fontWeight: '600', color: '#92400e', fontSize: '0.95rem' }}>
                {data.pendingProposalsCount} AI Consultation Proposal{data.pendingProposalsCount > 1 ? 's' : ''} Awaiting Review
              </div>
              <div style={{ fontSize: '0.85rem', color: '#b45309' }}>
                Pending manager sign-off to finalize appointment bookings, assign veterinarians, and reserve medicine.
              </div>
            </div>
          </div>
          <button
            onClick={() => onNavigate?.('approvals')}
            style={{
              padding: '0.5rem 1.125rem',
              backgroundColor: '#d97706',
              color: '#ffffff',
              borderRadius: '0.5rem',
              border: 'none',
              fontWeight: '600',
              fontSize: '0.875rem',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '0.375rem',
              boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
            }}
          >
            Review Proposals <ArrowUpRight size={16} />
          </button>
        </div>
      )}

      {/* KPI Cards Grid */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
        gap: '1.25rem'
      }}>
        {statCards.map((c, idx) => (
          <div
            key={idx}
            onClick={c.action}
            style={{
              backgroundColor: '#ffffff',
              borderRadius: '0.875rem',
              border: `1px solid ${c.border}`,
              padding: '1.25rem',
              cursor: 'pointer',
              transition: 'transform 0.15s ease, box-shadow 0.15s ease',
              boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between'
            }}
            onMouseEnter={e => {
              e.currentTarget.style.transform = 'translateY(-2px)';
              e.currentTarget.style.boxShadow = '0 4px 6px -1px rgba(0,0,0,0.1)';
            }}
            onMouseLeave={e => {
              e.currentTarget.style.transform = 'translateY(0)';
              e.currentTarget.style.boxShadow = '0 1px 3px rgba(0,0,0,0.04)';
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.75rem' }}>
              <span style={{ fontSize: '0.875rem', fontWeight: '500', color: '#4b5563' }}>{c.title}</span>
              <div style={{ width: 40, height: 40, borderRadius: '0.5rem', backgroundColor: c.bg, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                {c.icon}
              </div>
            </div>
            <div>
              <div style={{ fontSize: '1.875rem', fontWeight: '700', color: '#111827', letterSpacing: '-0.025em' }}>
                {c.value}
              </div>
              <div style={{ fontSize: '0.8rem', color: '#6b7280', marginTop: '0.25rem' }}>
                {c.sub}
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Middle Split: Workload & Status Distribution */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(400px, 1fr))', gap: '1.5rem' }}>
        {/* Veterinarian Workload Snapshot */}
        <div style={{ backgroundColor: '#ffffff', borderRadius: '0.875rem', border: '1px solid #e5e7eb', padding: '1.5rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.25rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <Stethoscope size={18} style={{ color: '#0d9488' }} />
              <h3 style={{ fontSize: '1rem', fontWeight: '600', color: '#111827', margin: 0 }}>Veterinarian Workload</h3>
            </div>
            <button
              onClick={() => onNavigate?.('reports')}
              style={{ fontSize: '0.8rem', color: '#0d9488', fontWeight: '600', background: 'none', border: 'none', cursor: 'pointer' }}
            >
              Full Analytics →
            </button>
          </div>

          {(!data?.veterinarianWorkloads || data.veterinarianWorkloads.length === 0) ? (
            <div style={{ padding: '2rem', textAlign: 'center', color: '#9ca3af', fontSize: '0.875rem' }}>
              No active veterinarians registered yet.
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              {data.veterinarianWorkloads.map(vet => {
                const total = vet.totalAppointments || 1;
                const completedPct = Math.min(100, Math.round((vet.completedAppointments / total) * 100));
                return (
                  <div key={vet.veterinarianId} style={{ padding: '0.75rem', borderRadius: '0.5rem', backgroundColor: '#f9fafb' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.37rem' }}>
                      <div>
                        <span style={{ fontWeight: '600', fontSize: '0.9rem', color: '#1f2937' }}>{vet.veterinarianName}</span>
                        <span style={{ fontSize: '0.75rem', color: '#6b7280', marginLeft: '0.5rem' }}>({vet.specialisation})</span>
                      </div>
                      <span style={{ fontSize: '0.8rem', fontWeight: '600', color: '#374151' }}>
                        {vet.totalAppointments} appts ({vet.completedAppointments} completed)
                      </span>
                    </div>
                    {/* Progress Bar */}
                    <div style={{ width: '100%', height: 6, backgroundColor: '#e5e7eb', borderRadius: 3, overflow: 'hidden' }}>
                      <div style={{ width: `${completedPct}%`, height: '100%', backgroundColor: '#0d9488', borderRadius: 3 }} />
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Appointment Status Breakdown */}
        <div style={{ backgroundColor: '#ffffff', borderRadius: '0.875rem', border: '1px solid #e5e7eb', padding: '1.5rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.25rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <Activity size={18} style={{ color: '#6366f1' }} />
              <h3 style={{ fontSize: '1rem', fontWeight: '600', color: '#111827', margin: 0 }}>Appointment Status Distribution</h3>
            </div>
            <button
              onClick={() => onNavigate?.('appointments')}
              style={{ fontSize: '0.8rem', color: '#6366f1', fontWeight: '600', background: 'none', border: 'none', cursor: 'pointer' }}
            >
              View Schedule →
            </button>
          </div>

          {(!data?.appointmentStatusCounts || data.appointmentStatusCounts.length === 0) ? (
            <div style={{ padding: '2rem', textAlign: 'center', color: '#9ca3af', fontSize: '0.875rem' }}>
              No appointment records in this organization.
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              {data.appointmentStatusCounts.map(item => (
                <div
                  key={item.status}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '0.625rem 0.875rem',
                    borderRadius: '0.5rem',
                    border: '1px solid #f3f4f6',
                    backgroundColor: '#fafafa'
                  }}
                >
                  <span style={{ fontSize: '0.875rem', fontWeight: '500', color: '#374151' }}>
                    {item.status}
                  </span>
                  <span style={{
                    padding: '0.2rem 0.6rem',
                    borderRadius: '9999px',
                    backgroundColor: item.status === 'Confirmed' ? '#dcfce7' : item.status === 'Completed' ? '#e0e7ff' : '#fef3c7',
                    color: item.status === 'Confirmed' ? '#15803d' : item.status === 'Completed' ? '#4338ca' : '#b45309',
                    fontSize: '0.8rem',
                    fontWeight: '700'
                  }}>
                    {item.count}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Recent Activity Trail */}
      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.875rem', border: '1px solid #e5e7eb', padding: '1.5rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.25rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <TrendingUp size={18} style={{ color: '#0d9488' }} />
            <h3 style={{ fontSize: '1rem', fontWeight: '600', color: '#111827', margin: 0 }}>Recent Clinic Operations & Audit Log</h3>
          </div>
          <button
            onClick={() => onNavigate?.('audit')}
            style={{ fontSize: '0.8rem', color: '#0d9488', fontWeight: '600', background: 'none', border: 'none', cursor: 'pointer' }}
          >
            Full Audit Trail →
          </button>
        </div>

        {(!data?.recentActivities || data.recentActivities.length === 0) ? (
          <div style={{ padding: '2rem', textAlign: 'center', color: '#9ca3af', fontSize: '0.875rem' }}>
            No recent administrative actions recorded.
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            {data.recentActivities.slice(0, 6).map(act => (
              <div
                key={act.id}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '0.75rem',
                  borderRadius: '0.5rem',
                  backgroundColor: '#f9fafb',
                  fontSize: '0.875rem'
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                  <div style={{
                    padding: '0.2rem 0.5rem',
                    borderRadius: '0.375rem',
                    backgroundColor: '#e0f2fe',
                    color: '#0369a1',
                    fontSize: '0.75rem',
                    fontWeight: '700'
                  }}>
                    {act.action}
                  </div>
                  <span style={{ color: '#374151' }}>{act.details}</span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', color: '#6b7280', fontSize: '0.8rem' }}>
                  <span>{act.userEmail}</span>
                  <span>{new Date(act.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
