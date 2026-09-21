import { Routes, Route, Navigate } from 'react-router-dom';
import { DashboardLayout } from '../features/shared/DashboardLayout';
import { DashboardPage } from '../pages/DashboardPage';
import { PlaceholderPage } from '../pages/PlaceholderPage';
import { SchedulingPage } from '../features/scheduling/SchedulingPage';
import { BillingPage } from '../features/billing/BillingPage';
import { ApprovalPage } from '../features/approvals/ApprovalPage';
import { AIWorkflowsPage } from '../features/ai-workflows/AIWorkflowsPage';
import { InventoryPage } from '../features/inventory/InventoryPage';
import { LoginPage } from '../features/auth/LoginPage';
import { RegisterPage } from '../features/auth/RegisterPage';
import { ChangePasswordPage } from '../features/auth/ChangePasswordPage';
import { ProtectedRoute } from '../features/auth/ProtectedRoute';
import RoleRoute from '../features/auth/components/RoleRoute';
import { UnauthorizedPage } from '../features/shared/UnauthorizedPage';
import { SuperAdminDashboard } from '../features/dashboard/SuperAdminDashboard';
import { ClinicManagerDashboard } from '../features/dashboard/ClinicManagerDashboard';
import { VeterinarianDashboard } from '../features/dashboard/VeterinarianDashboard';
import { InventoryDashboard } from '../features/dashboard/InventoryDashboard';
import { PetOwnerDashboard } from '../features/dashboard/PetOwnerDashboard';
import PetsPage from '../features/pets/PetsPage';
import { ConsultationRequestsPage } from '../features/consultations/ConsultationRequestsPage';
import { TreatmentPage } from '../features/treatment/TreatmentPage';
import { ExaminationsPage } from '../features/treatment/pages/ExaminationsPage';
import { useAuth } from '../features/auth/AuthContext';
import type { Role } from '../types/domain';

const ROLE_HOMES: Record<Role, string> = {
  Administrator: '/super-admin',
  ClinicManager: '/manager',
  Veterinarian: '/vet',
  InventoryOfficer: '/inventory-dashboard',
  PetOwner: '/pet-owner',
};

function HomeRedirect() {
  const { user, isAuthenticated } = useAuth();
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  const home = user ? (ROLE_HOMES[user.role] ?? '/') : '/';
  return <Navigate to={home} replace />;
}

export function AppRoutes() {
  return <Routes>
    {/* Public routes */}
    <Route path="/" element={<HomeRedirect />} />
    <Route path="/login" element={<LoginPage />} />
    <Route path="/register" element={<RegisterPage />} />
    <Route path="/change-password" element={
      <ProtectedRoute><ChangePasswordPage /></ProtectedRoute>
    } />
    <Route path="/unauthorized" element={<UnauthorizedPage />} />

    {/* All authenticated pages share the same DashboardLayout shell */}
    <Route element={<ProtectedRoute><DashboardLayout /></ProtectedRoute>}>

      {/* Role-based dashboards */}
      <Route path="/super-admin" element={
        <RoleRoute allowedRoles={['Administrator']}><SuperAdminDashboard /></RoleRoute>
      } />
      <Route path="/manager" element={
        <RoleRoute allowedRoles={['ClinicManager', 'Administrator']}><ClinicManagerDashboard /></RoleRoute>
      } />
      <Route path="/vet" element={
        <RoleRoute allowedRoles={['Veterinarian', 'Administrator']}><VeterinarianDashboard /></RoleRoute>
      } />
      <Route path="/inventory-dashboard" element={
        <RoleRoute allowedRoles={['InventoryOfficer', 'Administrator']}><InventoryDashboard /></RoleRoute>
      } />
      <Route path="/pet-owner" element={
        <RoleRoute allowedRoles={['PetOwner', 'Administrator']}><PetOwnerDashboard /></RoleRoute>
      } />

      {/* Feature pages */}
      <Route path="/dashboard" element={<DashboardPage />} />
      <Route path="/pets" element={<PetsPage />} />
      <Route path="/consultations" element={<ConsultationRequestsPage />} />
      <Route path="/treatment" element={<TreatmentPage />} />
      <Route path="/examinations" element={<ExaminationsPage />} />
      <Route path="/inventory" element={<InventoryPage />} />
      <Route path="/scheduling" element={<SchedulingPage />} />
      <Route path="/billing" element={<BillingPage />} />
      <Route path="/approvals" element={<ApprovalPage />} />
      <Route path="/ai-workflows" element={<AIWorkflowsPage />} />
      <Route path="/reports" element={<PlaceholderPage title="Reports & Analytics" />} />
      <Route path="/settings" element={<PlaceholderPage title="Users & Settings" />} />
    </Route>

    {/* Fallback */}
    <Route path="*" element={<Navigate to="/" replace />} />
  </Routes>;
}
