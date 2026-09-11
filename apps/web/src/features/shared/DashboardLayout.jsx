import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { LogOut, LayoutDashboard, Building2 } from 'lucide-react';
import useAuthStore from '../../store/authStore';
import ProfileModal from './ProfileModal';

/**
 * Shared dashboard layout with sidebar navigation.
 * Each role dashboard provides its own navItems array.
 */
export default function DashboardLayout({ children, navItems = [], pageTitle, pageSubtitle }) {
  const navigate = useNavigate();
  const { user, logout } = useAuthStore();
  const [profileOpen, setProfileOpen] = useState(false);


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
            <div className="sidebar-logo-text">Beacon Pet Health</div>
            <div className="sidebar-logo-subtext">
              {user?.organization ? user.organization.name : 'Veterinary Platform'}
            </div>
          </div>
        </div>

        {/* Tenant Organization Scoping Badge */}
        {user?.organization && (
          <div style={{
            margin: '0 0.85rem 1rem 0.85rem',
            padding: '0.6rem 0.75rem',
            backgroundColor: 'rgba(255,255,255,0.06)',
            borderRadius: '0.5rem',
            border: '1px solid rgba(255,255,255,0.1)',
            display: 'flex',
            alignItems: 'center',
            gap: '0.6rem'
          }}>
            <Building2 size={16} style={{ color: 'var(--petcare-primary, #FFBE00)', flexShrink: 0 }} />
            <div style={{ minWidth: 0, flex: 1 }}>
              <div style={{ fontSize: '0.7rem', color: '#9CA3AF', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                Active Clinic
              </div>
              <div style={{ fontSize: '0.825rem', fontWeight: '600', color: '#FFFFFF', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                {user.organization.name}
              </div>
            </div>
          </div>
        )}

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

        {/* User card + Profile + Logout */}
        <div className="sidebar-footer">
          <div
            className="sidebar-user-card"
            onClick={() => setProfileOpen(true)}
            style={{ cursor: 'pointer' }}
            title="Click to manage account settings"
            aria-label={`Logged in as ${user?.firstName} ${user?.lastName}. Click to manage profile.`}
          >
            <div className="sidebar-avatar" aria-hidden="true">{initials}</div>
            <div style={{ minWidth: 0, flex: 1 }}>
              <div className="sidebar-user-name">{user?.firstName} {user?.lastName}</div>
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

      {/* ── MAIN CONTENT ── */}
      <div className="dashboard-main">
        <header className="dashboard-header">
          <div>
            <h1 className="dashboard-header-title">{pageTitle ?? 'Dashboard'}</h1>
            {pageSubtitle && (
              <p className="dashboard-header-subtitle">{pageSubtitle}</p>
            )}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
            {user?.organization && (
              <div style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '0.4rem',
                padding: '0.3rem 0.65rem',
                backgroundColor: '#EFF6FF',
                border: '1px solid #BFDBFE',
                borderRadius: '9999px',
                fontSize: '0.8rem',
                fontWeight: '600',
                color: '#1D4ED8'
              }}>
                <Building2 size={13} />
                <span>{user.organization.name}</span>
                <span style={{
                  fontSize: '0.65rem',
                  backgroundColor: '#D1FAE5',
                  color: '#065F46',
                  padding: '0.1rem 0.35rem',
                  borderRadius: '9999px'
                }}>
                  {user.organization.status || 'Active'}
                </span>
              </div>
            )}
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
