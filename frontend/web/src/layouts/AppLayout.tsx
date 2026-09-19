import { useState } from 'react';
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { Bell, CalendarDays, ClipboardCheck, FileText, Home, LayoutDashboard, LogOut, Menu, Package, PawPrint, Settings, Sparkles, Stethoscope, X } from 'lucide-react';
import { Badge } from '../components/ui/Badge';
import { useAuth } from '../features/auth/AuthContext';

const navigation = [
  { label: 'Dashboard', to: '/', icon: LayoutDashboard, scope: 'dashboard' },
  { label: 'Consultation Requests', to: '/consultations', icon: PawPrint, scope: 'placeholder' },
  { label: 'Diagnosis & Treatment', to: '/treatment', icon: Stethoscope, scope: 'placeholder' },
  { label: 'Medicine & Inventory', to: '/inventory', icon: Package, scope: 'placeholder' },
  { label: 'Scheduling', to: '/scheduling', icon: CalendarDays, scope: 'complete' },
  { label: 'Quotations & Billing', to: '/billing', icon: FileText, scope: 'complete' },
  { label: 'Approval Center', to: '/approvals', icon: ClipboardCheck, scope: 'complete' },
  { label: 'AI Workflows', to: '/ai-workflows', icon: Sparkles, scope: 'ai-ui' },
  { label: 'Reports', to: '/reports', icon: Home, scope: 'placeholder' },
  { label: 'Users & Settings', to: '/settings', icon: Settings, scope: 'placeholder' },
];

function initialsOf(name: string): string {
  return name.split(' ').filter(Boolean).slice(0, 2).map((part) => part[0]?.toUpperCase()).join('') || '?';
}

export function AppLayout() {
  const [mobileOpen, setMobileOpen] = useState(false);
  const close = () => setMobileOpen(false);
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const initials = user ? initialsOf(user.name) : '?';

  const onLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return <div className="app-shell">
    <aside className={`sidebar ${mobileOpen ? 'sidebar-open' : ''}`}>
      <div className="brand-block">
        <Link to="/" className="brand" onClick={close}><span className="brand-mark"><PawPrint size={23} /></span><span><strong>PetCare</strong><small>AI Clinic Operations</small></span></Link>
        <button className="mobile-close" onClick={close} aria-label="Close navigation"><X size={20} /></button>
      </div>
      <div className="workspace-pill"><span className="status-dot" />Colombo Clinic<span className="workspace-arrow">⌄</span></div>
      <nav className="nav-list" aria-label="Main navigation">
        {navigation.map(({ label, to, icon: Icon, scope }) => <NavLink key={to} to={to} onClick={close} className={({ isActive }: { isActive: boolean }) => `nav-item ${isActive ? 'active' : ''}`}><Icon size={18} /><span>{label}</span>{scope === 'complete' && <span className="nav-dot" />}{scope === 'ai-ui' && <Badge tone="info">UI</Badge>}</NavLink>)}
      </nav>
      <div className="sidebar-footer"><div className="user-card"><div className="avatar">{initials}</div><div><strong>{user?.name ?? 'Unknown user'}</strong><span>{user?.email ?? ''}</span></div><button className="icon-button" onClick={onLogout} aria-label="Log out" title="Log out"><LogOut size={16} /></button></div></div>
    </aside>

    <div className="page-shell">
      <header className="topbar"><button className="mobile-menu" onClick={() => setMobileOpen(true)} aria-label="Open navigation"><Menu size={21} /></button><div><p className="topbar-kicker">Staff workspace</p><h1>PetCare AI</h1></div><div className="topbar-actions"><button className="icon-button"><Bell size={19} /><span className="notification-dot" /></button><div className="top-avatar">{initials}</div></div></header>
      <main className="main-content"><Outlet /></main>
      <footer className="app-footer"><span>PetCare AI · SE3090 Assignment 1</span><span>Web staff console · React</span></footer>
    </div>
  </div>;
}
