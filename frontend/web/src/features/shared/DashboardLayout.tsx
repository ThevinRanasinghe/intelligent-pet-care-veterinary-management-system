import { useState } from 'react';
import { Outlet, useNavigate, useLocation, NavLink } from 'react-router-dom';
import {
  LayoutDashboard, PawPrint, Stethoscope, Package, CalendarDays,
  FileText, ClipboardCheck, Sparkles, BarChart3, Settings,
  LogOut, Menu, X,
} from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import type { Role } from '../../types/domain';
import ProfileModal from './ProfileModal';

const ROLE_HOMES: Record<Role, string> = {
  Administrator: '/super-admin',
  ClinicManager: '/manager',
  Veterinarian: '/vet',
  InventoryOfficer: '/inventory-dashboard',
  PetOwner: '/pet-owner',
};

const PAGE_META: Record<string, { title: string; subtitle: string }> = {
  '/manager': { title: 'Clinic Manager Overview', subtitle: 'Live operational command center for your clinic' },
  '/vet': { title: 'Veterinarian Dashboard', subtitle: 'Patient consultations, examinations, and treatment records' },
  '/inventory-dashboard': { title: 'Inventory Officer Dashboard', subtitle: 'Pharmaceutical stock tracking, FEFO batch dispensing, and supplier directory' },
  '/super-admin': { title: 'Platform Overview', subtitle: 'Review registrations, verify clinics, manage lifecycle states' },
  '/pet-owner': { title: 'My Pet Care', subtitle: 'Manage your pets, consultation requests, and treatment history' },
  '/dashboard': { title: 'Dashboard', subtitle: 'Clinic operations overview' },
  '/pets': { title: 'Pets', subtitle: 'Manage registered pets' },
  '/consultations': { title: 'Consultation Requests', subtitle: 'Pet consultation requests and status' },
  '/treatment': { title: 'Diagnosis & Treatment', subtitle: 'Treatment records and care plans' },
  '/examinations': { title: 'Examinations', subtitle: 'Patient examination records' },
  '/inventory': { title: 'Medicine & Inventory', subtitle: 'Pharmaceutical stock management' },
  '/scheduling': { title: 'Scheduling', subtitle: 'Appointment scheduling and slot management' },
  '/billing': { title: 'Quotations & Billing', subtitle: 'Quotations, invoices, and payment tracking' },
  '/approvals': { title: 'Approval Center', subtitle: 'Review and approve pending proposals' },
  '/ai-workflows': { title: 'AI Workflows', subtitle: 'AI-powered clinical workflows' },
  '/reports': { title: 'Reports & Analytics', subtitle: 'Business intelligence and reporting' },
  '/settings': { title: 'Users & Settings', subtitle: 'Account and system configuration' },
};

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
  const meta = PAGE_META[location.pathname] ?? { title: 'Dashboard', subtitle: '' };

  const initials = user
    ? user.name.split(' ').filter(Boolean).slice(0, 2).map((part) => part[0]?.toUpperCase()).join('') || '?'
    : '?';

  const navItems = [
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
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <button
              className="dashboard-menu-btn"
              onClick={() => setSidebarOpen(true)}
              aria-label="Open navigation"
            >
              <Menu size={20} />
            </button>
            <div>
              <h1 className="dashboard-header-title">{meta.title}</h1>
              {meta.subtitle && (
                <p className="dashboard-header-subtitle">{meta.subtitle}</p>
              )}
            </div>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
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
