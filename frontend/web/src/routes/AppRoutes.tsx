import { Routes, Route, Navigate } from 'react-router-dom';
import { DashboardLayout } from '../features/shared/DashboardLayout';
import { DashboardPage } from '../pages/DashboardPage';
import { PlaceholderPage } from '../pages/PlaceholderPage';
import { SchedulingPage } from '../features/scheduling/SchedulingPage';
import { BillingPage } from '../features/billing/BillingPage';
import { ApprovalPage } from '../features/approvals/ApprovalPage';
import { AIWorkflowsPage } from '../features/ai-workflows/AIWorkflowsPage';
import { InventoryPage } from '../features/inventory/InventoryPage';
import { StockBatchesPage } from '../features/inventory/StockBatchesPage';
import { ReservationsPage } from '../features/inventory/ReservationsPage';
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
import { OwnerCareHistoryPage } from '../features/treatment/OwnerCareHistoryPage';
import { MedicalHistoryPage } from '../features/treatment/MedicalHistoryPage';
import { PrescriptionsPage } from '../features/treatment/PrescriptionsPage';
import { TreatmentPage } from '../features/treatment/TreatmentPage';
import { ExaminationsPage } from '../features/treatment/pages/ExaminationsPage';
import { AdminUsersPage } from '../features/admin/AdminUsersPage';
import { AdminOrganizationsPage } from '../features/admin/AdminOrganizationsPage';
import { AdminRolesPage } from '../features/admin/AdminRolesPage';
import { AdminSystemPage } from '../features/admin/AdminSystemPage';
import { useAuth } from '../features/auth/AuthContext';
import {
  CLINICAL_ROLES,
  CLINICAL_STAFF_ROLES,
  INVENTORY_ROLES,
  MANAGEMENT_ROLES,
  ROLE_HOMES,
} from '../features/auth/roleAccess';

/**
 * /prescriptions is shared: pet owners get their own care/prescription
 * history, while clinical staff get the clinic-wide prescription list.
 */
function PrescriptionsEntry() {
  const { user } = useAuth();
  return user?.role === 'PetOwner' ? <OwnerCareHistoryPage /> : <PrescriptionsPage />;
}

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
      <Route path="/dashboard" element={<RoleRoute allowedRoles={MANAGEMENT_ROLES}><DashboardPage /></RoleRoute>} />
      <Route path="/pets" element={<RoleRoute allowedRoles={CLINICAL_ROLES}><PetsPage /></RoleRoute>} />
      <Route path="/consultations" element={<RoleRoute allowedRoles={CLINICAL_ROLES}><ConsultationRequestsPage /></RoleRoute>} />
      <Route path="/care-history" element={<RoleRoute allowedRoles={['PetOwner', 'Administrator']}><OwnerCareHistoryPage /></RoleRoute>} />
      <Route path="/prescriptions" element={<RoleRoute allowedRoles={['PetOwner', 'Veterinarian', 'ClinicManager', 'Administrator']}><PrescriptionsEntry /></RoleRoute>} />
      <Route path="/medical-history" element={<RoleRoute allowedRoles={['Veterinarian', 'ClinicManager', 'Administrator']}><MedicalHistoryPage /></RoleRoute>} />
      <Route path="/treatment" element={<RoleRoute allowedRoles={CLINICAL_STAFF_ROLES}><TreatmentPage /></RoleRoute>} />
      <Route path="/examinations" element={<RoleRoute allowedRoles={CLINICAL_STAFF_ROLES}><ExaminationsPage /></RoleRoute>} />
      <Route path="/inventory" element={<RoleRoute allowedRoles={INVENTORY_ROLES}><InventoryPage /></RoleRoute>} />
      <Route path="/inventory/suppliers" element={<RoleRoute allowedRoles={INVENTORY_ROLES}><InventoryPage initialTab="suppliers" /></RoleRoute>} />
      <Route path="/inventory/batches" element={<RoleRoute allowedRoles={INVENTORY_ROLES}><StockBatchesPage /></RoleRoute>} />
      <Route path="/inventory/reservations" element={<RoleRoute allowedRoles={INVENTORY_ROLES}><ReservationsPage /></RoleRoute>} />
      <Route path="/inventory/alerts" element={<RoleRoute allowedRoles={INVENTORY_ROLES}><InventoryPage initialTab="lowStock" /></RoleRoute>} />
      <Route path="/scheduling" element={<RoleRoute allowedRoles={MANAGEMENT_ROLES}><SchedulingPage /></RoleRoute>} />
      <Route path="/billing" element={<RoleRoute allowedRoles={MANAGEMENT_ROLES}><BillingPage /></RoleRoute>} />
      <Route path="/approvals" element={<RoleRoute allowedRoles={MANAGEMENT_ROLES}><ApprovalPage /></RoleRoute>} />
      <Route path="/ai-workflows" element={<RoleRoute allowedRoles={MANAGEMENT_ROLES}><AIWorkflowsPage /></RoleRoute>} />
      <Route path="/reports" element={<RoleRoute allowedRoles={MANAGEMENT_ROLES}><PlaceholderPage title="Reports & Analytics" /></RoleRoute>} />
      <Route path="/settings" element={<RoleRoute allowedRoles={['Administrator']}><AdminUsersPage /></RoleRoute>} />
      <Route path="/admin/organizations" element={<RoleRoute allowedRoles={['Administrator']}><AdminOrganizationsPage /></RoleRoute>} />
      <Route path="/admin/roles" element={<RoleRoute allowedRoles={['Administrator']}><AdminRolesPage /></RoleRoute>} />
      <Route path="/admin/system" element={<RoleRoute allowedRoles={['Administrator']}><AdminSystemPage /></RoleRoute>} />
    </Route>

    {/* Fallback */}
    <Route path="*" element={<Navigate to="/" replace />} />
  </Routes>;
}
