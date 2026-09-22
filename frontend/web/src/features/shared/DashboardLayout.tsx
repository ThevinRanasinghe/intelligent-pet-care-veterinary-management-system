import { useState } from 'react';
import type { ReactNode } from 'react';
import { Outlet, useNavigate, useLocation, NavLink } from 'react-router-dom';
import {
  LayoutDashboard, PawPrint, Stethoscope, Package, CalendarDays,
  FileText, ClipboardCheck, Sparkles, BarChart3, Settings,
  LogOut, Menu, X, Pill, Activity, History,
  Truck, Layers, ClipboardList, AlertTriangle,
  Users, Building2, ShieldCheck,
} from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { ROLE_HOMES } from '../auth/roleAccess';
import type { Role } from '../../types/domain';
import ProfileModal from './ProfileModal';

const PAGE_META: Record<string, { title: string; subtitle: string }> = {
  '/manager': { title: 'Clinic Manager Overview', subtitle: 'Live operational command center for your clinic' },
  '/vet': { title: 'Veterinarian Dashboard', subtitle: 'Patient consultations, examinations, and treatment records' },
  '/inventory-dashboard': { title: 'Inventory Officer Dashboard', subtitle: 'Pharmaceutical stock tracking, FEFO batch dispensing, and supplier directory' },
  '/super-admin': { title: 'Platform Overview', subtitle: 'Review registrations, verify clinics, manage lifecycle states' },
  '/pet-owner': { title: 'My Pet Care', subtitle: 'Manage your pets, consultation requests, and treatment history' },
  '/dashboard': { title: 'Dashboard', subtitle: 'Clinic operations overview' },
  '/pets': { title: 'Pets', subtitle: 'Manage registered pets' },
  '/consultations': { title: 'Consultation Requests', subtitle: 'Pet consultation requests and status' },
  '/care-history': { title: 'Care History', subtitle: 'Treatments and prescriptions for your pets' },
  '/prescriptions': { title: 'Prescriptions', subtitle: 'Prescribed medications and treatment context' },
  '/treatment': { title: 'Diagnosis & Treatment', subtitle: 'Treatment records and care plans' },
  '/examinations': { title: 'Examinations', subtitle: 'Patient examination records' },
  '/medical-history': { title: 'Medical History', subtitle: 'Patient medical records across examinations, diagnoses, and treatments' },
  '/inventory': { title: 'Medicine & Inventory', subtitle: 'Pharmaceutical stock management' },
  '/inventory/suppliers': { title: 'Suppliers', subtitle: 'Pharmaceutical supply partners' },
  '/inventory/batches': { title: 'Stock & Batches', subtitle: 'Batch quantities, received dates, and expiry' },
  '/inventory/reservations': { title: 'Reservations', subtitle: 'Medicine stock reservations' },
  '/inventory/alerts': { title: 'Low Stock & Expiry', subtitle: 'Reorder alerts and expiring batches' },
  '/scheduling': { title: 'Scheduling', subtitle: 'Appointment scheduling and slot management' },
  '/billing': { title: 'Quotations & Billing', subtitle: 'Quotations, invoices, and payment tracking' },
  '/approvals': { title: 'Approval Center', subtitle: 'Review and approve pending proposals' },
  '/ai-workflows': { title: 'AI Workflows', subtitle: 'AI-powered clinical workflows' },
  '/reports': { title: 'Reports & Analytics', subtitle: 'Business intelligence and reporting' },
  '/settings': { title: 'Users & Settings', subtitle: 'Platform user accounts and access' },
  '/admin/organizations': { title: 'Organization Management', subtitle: 'Verify registrations and manage clinic lifecycle states' },
  '/admin/roles': { title: 'Roles & Permissions', subtitle: 'Platform role catalog and role assignments' },
  '/admin/system': { title: 'System Settings', subtitle: 'Platform runtime status and configuration' },
};

type NavItem = { label: string; to: string; icon: ReactNode };

/** Header copy shown to pet owners — friendlier labels than the staff terms. */
const PET_OWNER_PAGE_META: Record<string, { title: string; subtitle: string }> = {
  '/pets': { title: 'My Pets', subtitle: 'Register and manage your own pets' },
};

/**
 * Sidebar navigation is role-based: pet owners see only their own
 * pet-care items, veterinarians see clinical items, and the remaining
 * staff roles see the full clinic navigation.
 * NOTE: hiding nav items is presentation only — the backend enforces
 * ownership and role rules on every request.
 */
function getNavigationForRole(role: Role | undefined, roleHome: string): NavItem[] {
  if (role === 'PetOwner') {
    return [
      { label: 'Dashboard', to: roleHome, icon: <LayoutDashboard size={18} /> },
      { label: 'My Pets', to: '/pets', icon: <PawPrint size={18} /> },
      { label: 'Consultation Requests', to: '/consultations', icon: <CalendarDays size={18} /> },
      { label: 'Care History', to: '/care-history', icon: <Stethoscope size={18} /> },
      { label: 'Prescriptions', to: '/prescriptions', icon: <Pill size={18} /> },
    ];
  }

  if (role === 'Veterinarian') {
    return [
      { label: 'Dashboard', to: roleHome, icon: <LayoutDashboard size={18} /> },
      { label: 'Consultation Requests', to: '/consultations', icon: <PawPrint size={18} /> },
      { label: 'Examinations', to: '/examinations', icon: <Activity size={18} /> },
      { label: 'Diagnosis & Treatment', to: '/treatment', icon: <Stethoscope size={18} /> },
      { label: 'Medical History', to: '/medical-history', icon: <History size={18} /> },
      { label: 'Prescriptions', to: '/prescriptions', icon: <Pill size={18} /> },
    ];
  }

  if (role === 'InventoryOfficer') {
    return [
      { label: 'Dashboard', to: roleHome, icon: <LayoutDashboard size={18} /> },
      { label: 'Medicine & Inventory', to: '/inventory', icon: <Package size={18} /> },
      { label: 'Suppliers', to: '/inventory/suppliers', icon: <Truck size={18} /> },
      { label: 'Stock & Batches', to: '/inventory/batches', icon: <Layers size={18} /> },
      { label: 'Reservations', to: '/inventory/reservations', icon: <ClipboardList size={18} /> },
      { label: 'Low Stock & Expiry', to: '/inventory/alerts', icon: <AlertTriangle size={18} /> },
    ];
  }

  if (role === 'ClinicManager') {
    return [
      { label: 'Dashboard', to: roleHome, icon: <LayoutDashboard size={18} /> },
      { label: 'Consultation Requests', to: '/consultations', icon: <PawPrint size={18} /> },
      { label: 'Clinical Records', to: '/medical-history', icon: <History size={18} /> },
      { label: 'Medicine & Inventory', to: '/inventory', icon: <Package size={18} /> },
      { label: 'Scheduling', to: '/scheduling', icon: <CalendarDays size={18} /> },
      { label: 'Quotations & Billing', to: '/billing', icon: <FileText size={18} /> },
      { label: 'Approval Center', to: '/approvals', icon: <ClipboardCheck size={18} /> },
      { label: 'AI Workflows', to: '/ai-workflows', icon: <Sparkles size={18} /> },
      { label: 'Reports', to: '/reports', icon: <BarChart3 size={18} /> },
    ];
  }

  if (role === 'Administrator') {
    return [
      { label: 'Dashboard', to: roleHome, icon: <LayoutDashboard size={18} /> },
      { label: 'Users & Settings', to: '/settings', icon: <Users size={18} /> },
      { label: 'Organization Management', to: '/admin/organizations', icon: <Building2 size={18} /> },
      { label: 'Roles & Permissions', to: '/admin/roles', icon: <ShieldCheck size={18} /> },
      { label: 'System Settings', to: '/admin/system', icon: <Settings size={18} /> },
    ];
  }

  return [
    { label: 'Dashboard', to: roleHome, icon: <LayoutDashboard size={18} /> },
    { label: 'Consultation Requests', to: '/consultations', icon: <PawPrint size={18} /> },
    { label: 'Diagnosis & Treatment', to: '/treatment', icon: <Stethoscope size={18} /> },
    { label: 'Medicine & Inventory', to: '/inventory', icon: <Package size={18} /> },
    { label: 'Scheduling', to: '/scheduling', icon: <CalendarDays size={18} /> },
    { label: 'Quotations & Billing', to: '/billing', icon: <FileText size={18} /> },
    { label: 'Approval Center', to: '/approvals', icon: <ClipboardCheck size={18} /> },
    { label: 'AI Workflows', to: '/ai-workflows', icon: <Sparkles size={18} /> },
    { label: 'Reports', to: '/reports', icon: <BarChart3 size={18} /> },
    { label: 'Users & Settings', to: '/settings', icon: <Settings size={18} /> },
  ];
}

/**
 * Shared authenticated application shell.
 * Renders the persistent sidebar, header, and <Outlet /> for child routes.
 * All authenticated pages (role dashboards and feature pages) render inside this layout.
 */
export function DashboardLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuth();
  const [profileOpen, setProfileOpen] = useState(false);
  const [sidebarOpen, setSidebarOpen] = useState(false);

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  const roleHome = user ? (ROLE_HOMES[user.role] ?? '/manager') : '/manager';
  const meta =
    (user?.role === 'PetOwner' ? PET_OWNER_PAGE_META[location.pathname] : undefined)
    ?? PAGE_META[location.pathname]
    ?? { title: 'Dashboard', subtitle: '' };

  const initials = user
    ? user.name.split(' ').filter(Boolean).slice(0, 2).map((part) => part[0]?.toUpperCase()).join('') || '?'
    : '?';

  const navItems = getNavigationForRole(user?.role, roleHome);

  return (
    <div className="dashboard-root">
      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          className="dashboard-overlay"
          onClick={() => setSidebarOpen(false)}
          aria-hidden="true"
        />
      )}

      {/* SIDEBAR */}
      <aside className={`sidebar${sidebarOpen ? ' sidebar-open' : ''}`} aria-label="Main navigation">
        {/* Logo */}
        <div className="sidebar-logo">
          <div className="sidebar-logo-icon">🐾</div>
          <div>
            <div className="sidebar-logo-text">Beacon Pet Health</div>
            <div className="sidebar-logo-subtext">Veterinary Platform</div>
          </div>
          <button
            className="sidebar-close-btn"
            onClick={() => setSidebarOpen(false)}
            aria-label="Close navigation"
          >
            <X size={18} />
          </button>
        </div>

        {/* Navigation */}
        <nav className="sidebar-nav" aria-label="Dashboard navigation">
          <span className="sidebar-section-label">Navigation</span>
          {navItems.map(item => (
            <NavLink
              key={item.to}
              to={item.to}
              end
              className={({ isActive }: { isActive: boolean }) =>
                `sidebar-nav-item${isActive ? ' active' : ''}`
              }
              onClick={() => setSidebarOpen(false)}
            >
              <span aria-hidden="true">{item.icon}</span>
              {item.label}
            </NavLink>
          ))}
        </nav>

        {/* User card + Profile + Logout */}
        <div className="sidebar-footer">
          <div
            className="sidebar-user-card"
            onClick={() => setProfileOpen(true)}
            style={{ cursor: 'pointer' }}
            title="Click to manage account settings"
            aria-label={`Logged in as ${user?.name}. Click to manage profile.`}
          >
            <div className="sidebar-avatar" aria-hidden="true">{initials}</div>
            <div style={{ minWidth: 0, flex: 1 }}>
              <div className="sidebar-user-name">{user?.name}</div>
              <div className="sidebar-user-role" style={{ display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                <span>{user?.role}</span>
                <span style={{ fontSize: '0.65rem', opacity: 0.6 }}>(Edit)</span>
              </div>
            </div>
          </div>

          <button
            id="logout-btn"
            className="sidebar-logout-btn"
            onClick={handleLogout}
            aria-label="Log out of PetCare AI"
          >
            <LogOut size={16} aria-hidden="true" />
            Log Out
          </button>
        </div>
      </aside>

      <ProfileModal isOpen={profileOpen} onClose={() => setProfileOpen(false)} />

      {/* MAIN CONTENT */}
      <div className="dashboard-main">
        <header className="dashboard-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', minWidth: 0 }}>
            <button
              className="dashboard-menu-btn"
              onClick={() => setSidebarOpen(true)}
              aria-label="Open navigation"
            >
              <Menu size={20} />
            </button>
            <div style={{ minWidth: 0 }}>
              <h1 className="dashboard-header-title">{meta.title}</h1>
              {meta.subtitle && (
                <p className="dashboard-header-subtitle">{meta.subtitle}</p>
              )}
            </div>
          </div>
          <div className="dashboard-header-welcome" style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--petcare-muted, #55554A)' }}>
              Welcome back, <strong style={{ color: 'var(--petcare-black, #111111)' }}>{user?.name}</strong>
            </span>
          </div>
        </header>

        <main className="dashboard-content" id="main-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

export default DashboardLayout;
