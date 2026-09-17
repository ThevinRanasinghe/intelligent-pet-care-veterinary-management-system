import { useState } from "react";
import { Link, NavLink, Outlet } from "react-router-dom";
import {
  Bell,
  CalendarDays,
  ClipboardCheck,
  FileText,
  LayoutDashboard,
  Menu,
  Package,
  PawPrint,
  Settings,
  Sparkles,
  Stethoscope,
  Users,
  X,
  LogOut,
  Building2,
  ChevronDown,
} from "lucide-react";

const navigation = [
  {
    label: "Dashboard",
    to: "/",
    icon: LayoutDashboard,
  },
  {
    label: "Pets & Owners",
    to: "/pets",
    icon: PawPrint,
  },
  {
    label: "Consultation Requests",
    to: "/consultations",
    icon: PawPrint,
  },
  {
    label: "Diagnosis & Treatment",
    to: "/treatment",
    icon: Stethoscope,
  },
  {
    label: "Medicine & Inventory",
    to: "/inventory",
    icon: Package,
  },
  {
    label: "Scheduling",
    to: "/scheduling",
    icon: CalendarDays,
  },
  {
    label: "Quotations & Billing",
    to: "/billing",
    icon: FileText,
  },
  {
    label: "Approval Center",
    to: "/approvals",
    icon: ClipboardCheck,
  },
  {
    label: "AI Workflows",
    to: "/ai-workflows",
    icon: Sparkles,
  },
  {
    label: "Reports",
    to: "/reports",
    icon: FileText,
  },
  {
    label: "Users & Settings",
    to: "/settings",
    icon: Settings,
  },
];

export function AppLayout() {
  const [mobileOpen, setMobileOpen] = useState(false);

  const closeMobileMenu = () => {
    setMobileOpen(false);
  };

  return (
    <div className="app-shell">
      {/* ================= SIDEBAR ================= */}
      <aside className={`sidebar ${mobileOpen ? "sidebar-open" : ""}`}>
        {/* Brand */}
        <div className="brand-section">
          <Link to="/" className="brand" onClick={closeMobileMenu}>
            <div className="brand-logo">
              <PawPrint size={22} strokeWidth={2.5} />
            </div>

            <div className="brand-text">
              <strong>Beacon Pet Health</strong>
              <span>Happy Paws Veterinary</span>
            </div>
          </Link>

          <button
            className="mobile-close"
            onClick={closeMobileMenu}
            aria-label="Close navigation"
          >
            <X size={20} />
          </button>
        </div>

        {/* Clinic */}
        <div className="clinic-card">
          <div className="clinic-icon">
            <Building2 size={17} />
          </div>

          <div className="clinic-info">
            <span>ACTIVE CLINIC</span>
            <strong>Happy Paws Veterinary Hospital</strong>
          </div>

          <ChevronDown size={16} className="clinic-arrow" />
        </div>

        {/* Navigation */}
        <div className="navigation-section">
          <p className="navigation-title">NAVIGATION</p>

          <nav className="nav-list">
            {navigation.map(({ label, to, icon: Icon }) => (
              <NavLink
                key={to}
                to={to}
                onClick={closeMobileMenu}
                className={({ isActive }) =>
                  `nav-item ${isActive ? "active" : ""}`
                }
              >
                <Icon size={18} />

                <span>{label}</span>
              </NavLink>
            ))}
          </nav>
        </div>

        {/* Sidebar Footer */}
        <div className="sidebar-footer">
          <div className="user-card">
            <div className="user-avatar">LM</div>

            <div className="user-details">
              <strong>Lahiru Madushan</strong>
              <span>InventoryOfficer</span>
            </div>

            <Users size={16} />
          </div>

          <button className="logout-button">
            <LogOut size={16} />
            <span>Log Out</span>
          </button>
        </div>
      </aside>

      {/* ================= MAIN AREA ================= */}

      <div className="page-shell">
        {/* Top Header */}
        <header className="topbar">
          <button
            className="mobile-menu"
            onClick={() => setMobileOpen(true)}
            aria-label="Open navigation"
          >
            <Menu size={21} />
          </button>

          <div className="topbar-left">
            <p className="topbar-kicker">Clinic workspace</p>

            <h1>PetCare AI</h1>
          </div>

          <div className="topbar-right">
            {/* Clinic selector */}
            <div className="top-clinic">
              <Building2 size={16} />

              <span>Happy Paws Veterinary Hospital</span>

              <span className="clinic-status">Active</span>
            </div>

            {/* Notification */}
            <button className="notification-button" aria-label="Notifications">
              <Bell size={19} />

              <span className="notification-dot" />
            </button>

            {/* User */}
            <div className="top-user">
              <div className="top-user-avatar">LM</div>

              <div className="top-user-text">
                <span>Welcome back,</span>

                <strong>Lahiru</strong>
              </div>
            </div>
          </div>
        </header>

        {/* Page Content */}
        <main className="main-content">
          <Outlet />
        </main>

        {/* Footer */}
        <footer className="app-footer">
          <span>Beacon Pet Health · PetCare AI</span>

          <span>Veterinary Management System</span>
        </footer>
      </div>
    </div>
  );
}
