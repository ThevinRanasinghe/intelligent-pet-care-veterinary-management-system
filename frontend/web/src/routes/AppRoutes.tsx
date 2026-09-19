import { Routes, Route } from 'react-router-dom';
import { AppLayout } from '../layouts/AppLayout';
import { DashboardPage } from '../pages/DashboardPage';
import { PlaceholderPage } from '../pages/PlaceholderPage';
import { SchedulingPage } from '../features/scheduling/SchedulingPage';
import { BillingPage } from '../features/billing/BillingPage';
import { ApprovalPage } from '../features/approvals/ApprovalPage';
import { AIWorkflowsPage } from '../features/ai-workflows/AIWorkflowsPage';
import { InventoryPage } from '../features/inventory/InventoryPage';
import { LoginPage } from '../features/auth/LoginPage';
import { ProtectedRoute } from '../features/auth/ProtectedRoute';
import PetsPage from '../features/pets/PetsPage';
import { ConsultationRequestsPage } from '../features/consultations/ConsultationRequestsPage';
import { TreatmentPage } from '../features/treatment/TreatmentPage';
import { ExaminationsPage } from '../features/treatment/pages/ExaminationsPage';

export function AppRoutes() {
  return <Routes>
    <Route path="/login" element={<LoginPage />} />
    <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
      <Route path="/" element={<DashboardPage />} />
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
  </Routes>;
}
