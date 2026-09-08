import { Routes, Route } from 'react-router-dom';
import { AppLayout } from '../layouts/AppLayout';
import { DashboardPage } from '../pages/DashboardPage';
import { PlaceholderPage } from '../pages/PlaceholderPage';
import { SchedulingPage } from '../features/scheduling/SchedulingPage';
import { BillingPage } from '../features/billing/BillingPage';
import { ApprovalPage } from '../features/approvals/ApprovalPage';
import { AIWorkflowsPage } from '../features/ai-workflows/AIWorkflowsPage';
import { LoginPage } from '../features/auth/LoginPage';
import { ProtectedRoute } from '../features/auth/ProtectedRoute';

export function AppRoutes() {
  return <Routes>
    <Route path="/login" element={<LoginPage />} />
    <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
      <Route path="/" element={<DashboardPage />} />
      <Route path="/consultations" element={<PlaceholderPage title="Consultation Requests" />} />
      <Route path="/treatment" element={<PlaceholderPage title="Diagnosis & Treatment" />} />
      <Route path="/inventory" element={<PlaceholderPage title="Medicine & Inventory" />} />
      <Route path="/scheduling" element={<SchedulingPage />} />
      <Route path="/billing" element={<BillingPage />} />
      <Route path="/approvals" element={<ApprovalPage />} />
      <Route path="/ai-workflows" element={<AIWorkflowsPage />} />
      <Route path="/reports" element={<PlaceholderPage title="Reports & Analytics" />} />
      <Route path="/settings" element={<PlaceholderPage title="Users & Settings" />} />
    </Route>
  </Routes>;
}
