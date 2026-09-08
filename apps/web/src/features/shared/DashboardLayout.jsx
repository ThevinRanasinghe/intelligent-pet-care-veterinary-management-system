import { useNavigate } from 'react-router-dom';
import { LogOut, LayoutDashboard } from 'lucide-react';
import useAuthStore from '../../store/authStore';

/**
 * Shared dashboard layout with sidebar navigation.
 * Each role dashboard provides its own navItems array.
 */
export default function DashboardLayout({ children, navItems = [], pageTitle, pageSubtitle }) {
  const navigate = useNavigate();
  const { user, logout } = useAuthStore();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  const initials = user
    ? `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase()
    : '?';

  return (
    <div className="dashboard-root">
      {/* ── SIDEBAR ── */}
      <aside className="sidebar" aria-label="Main navigation">
        {/* Logo */}
        <div className="sidebar-logo">
          <div className="sidebar-logo-icon">🐾</div>
          <div>
            <div className="sidebar-logo-text">PetCare AI</div>
            <div className="sidebar-logo-subtext">Management System</div>
          </div>
        </div>

        {/* Navigation */}
        <nav className="sidebar-nav" aria-label="Dashboard navigation">
          <span className="sidebar-section-label">Navigation</span>

          {navItems.map((item) => (
            <button
              key={item.label}
              className={`sidebar-nav-item${item.active ? ' active' : ''}`}
              onClick={() => item.onClick?.()}
              aria-current={item.active ? 'page' : undefined}
            >
              <span aria-hidden="true">{item.icon}</span>
              {item.label}
            </button>
          ))}
        </nav>

        {/* User card + Logout */}
        <div className="sidebar-footer">
          <div className="sidebar-user-card" aria-label={`Logged in as ${user?.firstName} ${user?.lastName}`}>
            <div className="sidebar-avatar" aria-hidden="true">{initials}</div>
            <div style={{ minWidth: 0 }}>
              <div className="sidebar-user-name">{user?.firstName} {user?.lastName}</div>
              <div className="sidebar-user-role">{user?.role}</div>
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

      {/* ── MAIN CONTENT ── */}
      <div className="dashboard-main">
        <header className="dashboard-header">
          <div>
            <h1 className="dashboard-header-title">{pageTitle ?? 'Dashboard'}</h1>
            {pageSubtitle && (
              <p className="dashboard-header-subtitle">{pageSubtitle}</p>
            )}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <span style={{ fontSize: '0.8rem', color: 'var(--petcare-muted)' }}>
              Welcome back, <strong style={{ color: 'var(--petcare-black)' }}>{user?.firstName}</strong>
            </span>
          </div>
        </header>

        <main className="dashboard-content" id="main-content">
          {children}
        </main>
      </div>
    </div>
  );
}
