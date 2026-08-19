import { Routes, Route } from 'react-router-dom';
import { AppLayout } from '../layouts/AppLayout';
import { DashboardPage } from '../pages/DashboardPage';
import { PlaceholderPage } from '../pages/PlaceholderPage';
import { ConsultationsPage } from '../features/consultations/ConsultationsPage';
import { SchedulingPage } from '../features/scheduling/SchedulingPage';
import { BillingPage } from '../features/billing/BillingPage';
import { ApprovalPage } from '../features/approvals/ApprovalPage';
import { AIWorkflowsPage } from '../features/ai-workflows/AIWorkflowsPage';

export function AppRoutes() {
  return <Routes>
    <Route element={<AppLayout />}>
      <Route path="/" element={<DashboardPage />} />
      <Route path="/consultations" element={<ConsultationsPage />} />
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
