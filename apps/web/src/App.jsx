import { useEffect } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import useAuthStore from './store/authStore';

// Auth Pages & Components
import LoginPage from './features/auth/pages/LoginPage';
import RegisterPage from './features/auth/pages/RegisterPage';
import ProtectedRoute from './features/auth/components/ProtectedRoute';
import RoleRoute from './features/auth/components/RoleRoute';

// Shared
import UnauthorizedPage from './features/shared/UnauthorizedPage';

// Dashboards
import SuperAdminDashboard from './features/dashboard/super-admin/SuperAdminDashboard';
import ClinicManagerDashboard from './features/dashboard/manager/ClinicManagerDashboard';
import VeterinarianDashboard from './features/dashboard/veterinarian/VeterinarianDashboard';
import InventoryDashboard from './features/dashboard/inventory/InventoryDashboard';
import PetOwnerLanding from './features/dashboard/shared/PetOwnerLanding';

function HomeRedirect() {
  const { isAuthenticated, isInitializing, getHomeRoute } = useAuthStore();

  if (isInitializing) {
    return null;
  }

  if (isAuthenticated) {
    return <Navigate to={getHomeRoute()} replace />;
  }

  return <Navigate to="/login" replace />;
}

export default function App() {
  const { initializeAuth } = useAuthStore();

  useEffect(() => {
    initializeAuth();
  }, [initializeAuth]);

  return (
    <Routes>
      {/* Root redirect */}
      <Route path="/" element={<HomeRedirect />} />

      {/* Public routes */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/unauthorized" element={<UnauthorizedPage />} />

      {/* Protected Role-Based Routes */}
      <Route
        path="/super-admin"
        element={
          <ProtectedRoute>
            <RoleRoute allowedRoles={['SuperAdmin']}>
              <SuperAdminDashboard />
            </RoleRoute>
          </ProtectedRoute>
        }
      />

      <Route
        path="/manager"
        element={
          <ProtectedRoute>
            <RoleRoute allowedRoles={['ClinicManager', 'SuperAdmin']}>
              <ClinicManagerDashboard />
            </RoleRoute>
          </ProtectedRoute>
        }
      />

      <Route
        path="/vet"
        element={
          <ProtectedRoute>
            <RoleRoute allowedRoles={['Veterinarian', 'SuperAdmin']}>
              <VeterinarianDashboard />
            </RoleRoute>
          </ProtectedRoute>
        }
      />

      <Route
        path="/inventory"
        element={
          <ProtectedRoute>
            <RoleRoute allowedRoles={['InventoryOfficer', 'SuperAdmin']}>
              <InventoryDashboard />
            </RoleRoute>
          </ProtectedRoute>
        }
      />

      <Route
        path="/pet-owner"
        element={
          <ProtectedRoute>
            <RoleRoute allowedRoles={['PetOwner', 'SuperAdmin']}>
              <PetOwnerLanding />
            </RoleRoute>
          </ProtectedRoute>
        }
      />

      {/* Fallback */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
