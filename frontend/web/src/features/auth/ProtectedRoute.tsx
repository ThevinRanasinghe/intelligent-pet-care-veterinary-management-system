import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';

/**
 * Redirects unauthenticated users to /login, preserving the original
 * destination in location state so LoginPage can send them back after a
 * successful login. Role-specific gating (e.g. Clinic Manager-only
 * actions) is handled at the component level (see ApprovalPage), not here,
 * so every authenticated staff member can still view every page.
 */
export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return children;
}
