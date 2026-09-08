import { Navigate, useLocation } from 'react-router-dom';
import useAuthStore from '../../../store/authStore';

/**
 * ProtectedRoute ensures that only authenticated users can access the nested routes.
 * If authentication is still being initialized (checking localStorage token), it displays a loading spinner.
 * If unauthenticated, it redirects to /login.
 */
export default function ProtectedRoute({ children }) {
  const { isAuthenticated, isInitializing } = useAuthStore();
  const location = useLocation();

  if (isInitializing) {
    return (
      <div style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: 'var(--petcare-bg, #f8fafc)',
      }}>
        <div style={{
          width: '40px',
          height: '40px',
          border: '3px solid #e5e7eb',
          borderTopColor: 'var(--petcare-primary, #FFBE00)',
          borderRadius: '50%',
          animation: 'spin 0.8s linear infinite',
        }} />
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return children;
}
