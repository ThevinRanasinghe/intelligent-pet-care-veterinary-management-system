import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../AuthContext';
import type { Role } from '../../../types/domain';

/**
 * RoleRoute ensures that the logged-in user possesses one of the allowed
 * roles. If unauthorized, redirects to /unauthorized.
 */
export default function RoleRoute({ children, allowedRoles }: { children: ReactNode; allowedRoles: Role[] }) {
  const { user } = useAuth();

  if (!user || !allowedRoles.includes(user.role)) {
    return <Navigate to="/unauthorized" replace />;
  }

  return children;
}
