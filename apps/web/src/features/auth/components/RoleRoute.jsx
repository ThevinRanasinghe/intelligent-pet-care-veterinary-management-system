import { Navigate } from 'react-router-dom';
import useAuthStore from '../../../store/authStore';

/**
 * RoleRoute ensures that the logged-in user possesses one of the allowed roles.
 * If unauthorized, redirects to /unauthorized.
 */
export default function RoleRoute({ children, allowedRoles }) {
  const { user } = useAuthStore();

  if (!user || !allowedRoles.includes(user.role)) {
    return <Navigate to="/unauthorized" replace />;
  }

  return children;
}
