import { useState } from 'react';
import {
  LayoutDashboard, Sparkles, Calendar, Clock, Users,
  BarChart3, Building2, ShieldCheck, History
} from 'lucide-react';
import DashboardLayout from '../../shared/DashboardLayout';
import ClinicManagerOverviewView from './ClinicManagerOverviewView';
import ApprovalManagementView from './ApprovalManagementView';
import AppointmentManagementView from './AppointmentManagementView';
import VeterinarianScheduleView from './VeterinarianScheduleView';
import StaffManagementView from './StaffManagementView';
import ClinicReportsView from './ClinicReportsView';
import OrganizationProfileView from './OrganizationProfileView';
import AuditTrailView from './AuditTrailView';
import useAuthStore from '../../../store/authStore';

export default function ClinicManagerDashboard() {
  const [activeSection, setActiveSection] = useState('overview');
  const { user } = useAuthStore();

  const navItems = [
    {
      label: 'Clinic Overview',
      icon: <LayoutDashboard size={18} />,
      active: activeSection === 'overview',
      onClick: () => setActiveSection('overview')
    },
    {
      label: 'AI Proposals & Approvals',
      icon: <Sparkles size={18} />,
      active: activeSection === 'approvals',
      onClick: () => setActiveSection('approvals')
    },
    {
      label: 'Appointments',
      icon: <Calendar size={18} />,
      active: activeSection === 'appointments',
      onClick: () => setActiveSection('appointments')
    },
    {
      label: 'Veterinarian Schedules',
      icon: <Clock size={18} />,
      active: activeSection === 'schedules',
      onClick: () => setActiveSection('schedules')
    },
    {
      label: 'Staff & Team',
      icon: <Users size={18} />,
      active: activeSection === 'staff',
      onClick: () => setActiveSection('staff')
    },
    {
      label: 'Reports & Analytics',
      icon: <BarChart3 size={18} />,
      active: activeSection === 'reports',
      onClick: () => setActiveSection('reports')
    },
    {
      label: 'Clinic Profile',
      icon: <Building2 size={18} />,
      active: activeSection === 'profile',
      onClick: () => setActiveSection('profile')
    },
    {
      label: 'Audit Trail',
      icon: <History size={18} />,
      active: activeSection === 'audit',
      onClick: () => setActiveSection('audit')
    },
  ];

  const getPageMeta = () => {
    switch (activeSection) {
      case 'overview':
        return {
          title: 'Clinic Manager Overview',
          subtitle: `Live operational command center for ${user?.organization?.name || 'your clinic'}`
        };
      case 'approvals':
        return {
          title: 'AI Consultation Proposals & Approvals',
          subtitle: 'Decision support, deterministic check verification, and atomic approval execution'
        };
      case 'appointments':
        return {
          title: 'Appointment Management',
          subtitle: 'Consultation scheduling, booking conflict prevention, and appointment lifecycle'
        };
      case 'schedules':
        return {
          title: 'Veterinarian Schedule & Slot Management',
          subtitle: 'Configure daily availability, consultation hours, and room allocation'
        };
      case 'staff':
        return {
          title: 'Staff & Team Management',
          subtitle: 'Manage Veterinarians and Inventory Officers strictly for your organization'
        };
      case 'reports':
        return {
          title: 'Clinic Analytics & Performance Reports',
          subtitle: 'Revenue trends, diagnosed health conditions, and veterinarian workload analytics'
        };
      case 'profile':
        return {
          title: 'Clinic Workspace Profile',
          subtitle: 'Organization details, license registration, and contact information'
        };
      case 'audit':
        return {
          title: 'Administrative Audit Trail',
          subtitle: 'Immutable historical record of managerial and system actions'
        };
      default:
        return {
          title: 'Clinic Manager Portal',
          subtitle: 'Oversee clinic operations and staff management'
        };
    }
  };

  const meta = getPageMeta();

  return (
    <DashboardLayout
      pageTitle={meta.title}
      pageSubtitle={meta.subtitle}
      navItems={navItems}
    >
      {activeSection === 'overview' && (
        <ClinicManagerOverviewView onNavigate={(section) => setActiveSection(section)} />
      )}
      {activeSection === 'approvals' && <ApprovalManagementView />}
      {activeSection === 'appointments' && <AppointmentManagementView />}
      {activeSection === 'schedules' && <VeterinarianScheduleView />}
      {activeSection === 'staff' && <StaffManagementView />}
      {activeSection === 'reports' && <ClinicReportsView />}
      {activeSection === 'profile' && <OrganizationProfileView />}
      {activeSection === 'audit' && <AuditTrailView />}
    </DashboardLayout>
  );
}
