import { useNavigate } from 'react-router-dom';
import { ShieldAlert, ArrowLeft } from 'lucide-react';
import useAuthStore from '../../store/authStore';

export default function UnauthorizedPage() {
  const navigate = useNavigate();
  const { user, getHomeRoute } = useAuthStore();

  const handleReturn = () => {
    navigate(getHomeRoute(), { replace: true });
  };

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      backgroundColor: 'var(--petcare-bg, #f8fafc)',
      padding: '2rem',
    }}>
      <div style={{
        maxWidth: '480px',
        width: '100%',
        backgroundColor: '#ffffff',
        borderRadius: '1rem',
        padding: '2.5rem',
        boxShadow: '0 10px 25px -5px rgba(0, 0, 0, 0.05), 0 8px 10px -6px rgba(0, 0, 0, 0.01)',
        textAlign: 'center',
      }}>
        <div style={{
          width: '64px',
          height: '64px',
          borderRadius: '50%',
          backgroundColor: '#FEE2E2',
          color: '#DC2626',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          margin: '0 auto 1.5rem',
        }}>
          <ShieldAlert size={32} />
        </div>

        <h1 style={{ fontSize: '1.5rem', fontWeight: '700', color: '#111827', marginBottom: '0.5rem' }}>
          Access Denied (403)
        </h1>
        <p style={{ fontSize: '0.925rem', color: '#6B7280', marginBottom: '1.75rem', lineHeight: '1.5' }}>
          You do not have the required permissions to access this page with your current role{' '}
          {user?.role && <span style={{ fontWeight: '600', color: '#374151' }}>({user.role})</span>}.
        </p>

        <button
          onClick={handleReturn}
          id="unauthorized-return-btn"
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: '0.5rem',
            padding: '0.75rem 1.5rem',
            backgroundColor: 'var(--petcare-black, #111827)',
            color: '#ffffff',
            borderRadius: '0.5rem',
            border: 'none',
            fontSize: '0.9rem',
            fontWeight: '600',
            cursor: 'pointer',
            width: '100%',
          }}
        >
          <ArrowLeft size={16} />
          Return to Dashboard
        </button>
      </div>
    </div>
  );
}
