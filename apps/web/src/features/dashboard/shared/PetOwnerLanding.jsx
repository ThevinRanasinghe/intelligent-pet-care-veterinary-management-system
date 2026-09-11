import { Smartphone, Heart, Calendar, LogOut } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import useAuthStore from '../../../store/authStore';

export default function PetOwnerLanding() {
  const navigate = useNavigate();
  const { user, logout } = useAuthStore();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <div style={{
      minHeight: '100vh',
      backgroundColor: 'var(--petcare-bg, #f8fafc)',
      display: 'flex',
      flexDirection: 'column',
    }}>
      {/* Top Navbar */}
      <header style={{
        backgroundColor: '#ffffff',
        borderBottom: '1px solid #e5e7eb',
        padding: '1rem 2rem',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <span style={{ fontSize: '1.5rem' }}>🐾</span>
          <strong style={{ fontSize: '1.25rem', color: 'var(--petcare-black, #111827)' }}>PetCare AI</strong>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <span style={{ fontSize: '0.9rem', color: '#6b7280' }}>
            Welcome, <strong>{user?.firstName} {user?.lastName}</strong>
          </span>
          <button
            onClick={handleLogout}
            id="pet-owner-logout-btn"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '0.4rem',
              padding: '0.5rem 0.9rem',
              backgroundColor: '#f3f4f6',
              border: 'none',
              borderRadius: '0.5rem',
              fontSize: '0.85rem',
              fontWeight: '500',
              color: '#374151',
              cursor: 'pointer',
            }}
          >
            <LogOut size={15} />
            Log Out
          </button>
        </div>
      </header>

      {/* Main Content */}
      <main style={{
        maxWidth: '800px',
        margin: '3rem auto',
        padding: '0 1.5rem',
        width: '100%',
        textAlign: 'center',
      }}>
        <div style={{
          backgroundColor: '#ffffff',
          borderRadius: '1rem',
          border: '1px solid #e5e7eb',
          padding: '3rem 2rem',
          boxShadow: '0 4px 6px -1px rgba(0,0,0,0.05)',
        }}>
          <div style={{
            width: '64px',
            height: '64px',
            borderRadius: '50%',
            backgroundColor: '#FEF3C7',
            color: '#D97706',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            margin: '0 auto 1.5rem',
          }}>
            <Smartphone size={32} />
          </div>

          <h1 style={{ fontSize: '1.75rem', fontWeight: '700', color: '#111827', marginBottom: '0.75rem' }}>
            Welcome to PetCare AI!
          </h1>
          <p style={{ fontSize: '1rem', color: '#4b5563', lineHeight: '1.6', maxWidth: '540px', margin: '0 auto 2rem' }}>
            As a <strong>Pet Owner</strong>, your pet profiles, appointment booking, AI symptom checker, and vaccination records are best experienced on the <strong>PetCare AI Mobile App</strong>.
          </p>

          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
            gap: '1.25rem',
            textAlign: 'left',
            marginBottom: '2rem',
          }}>
            <div style={{ padding: '1rem', borderRadius: '0.5rem', backgroundColor: '#f9fafb', border: '1px solid #f3f4f6' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontWeight: '600', color: '#1f2937', marginBottom: '0.35rem' }}>
                <Heart size={16} color="#ef4444" />
                <span>Pet Health Tracking</span>
              </div>
              <p style={{ fontSize: '0.85rem', color: '#6b7280', margin: 0 }}>
                Maintain medical records, vaccines, and diet logs seamlessly.
              </p>
            </div>

            <div style={{ padding: '1rem', borderRadius: '0.5rem', backgroundColor: '#f9fafb', border: '1px solid #f3f4f6' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontWeight: '600', color: '#1f2937', marginBottom: '0.35rem' }}>
                <Calendar size={16} color="#3b82f6" />
                <span>Smart Booking</span>
              </div>
              <p style={{ fontSize: '0.85rem', color: '#6b7280', margin: 0 }}>
                Book slots with top veterinarians and get reminder notifications.
              </p>
            </div>
          </div>

          <div style={{ fontSize: '0.875rem', color: '#9ca3af' }}>
            Staff and management portals on this web console are restricted to authorized personnel.
          </div>
        </div>
      </main>
    </div>
  );
}
