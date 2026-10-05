import { screen } from '@testing-library/react';
import { Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import DashboardLayout from '../../features/shared/DashboardLayout';
import { VeterinarianDashboard } from '../../features/dashboard/VeterinarianDashboard';
import { ConsultationRequestsPage } from '../../features/consultations/ConsultationRequestsPage';
import RoleRoute from '../../features/auth/components/RoleRoute';
import UnauthorizedPage from '../../features/shared/UnauthorizedPage';
import { renderWithAuth } from '../testUtils';

function emptyJson() {
  return { ok: true, status: 200, text: async () => '[]' } as Response;
}

/** RoleRoute mirror of the /consultations declaration in AppRoutes. */
function consultationRoute() {
  return (
    <Routes>
      <Route
        path="/consultations"
        element={
          <RoleRoute allowedRoles={['PetOwner', 'ClinicManager', 'Administrator']}>
            <ConsultationRequestsPage />
          </RoleRoute>
        }
      />
      <Route path="/unauthorized" element={<UnauthorizedPage />} />
    </Routes>
  );
}

describe('Veterinarian consultation-request access removed', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async () => emptyJson()));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it('does NOT show Consultation Requests in the Veterinarian sidebar', async () => {
    renderWithAuth(<DashboardLayout />, { role: 'Veterinarian' });

    expect(await screen.findByRole('link', { name: /My Appointments/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Consultation Requests/i })).not.toBeInTheDocument();
  });

  it('still shows Consultation Requests to Pet Owner and Clinic Manager', async () => {
    const owner = renderWithAuth(<DashboardLayout />, { role: 'PetOwner' });
    expect(await screen.findByRole('link', { name: /Consultation Requests/i })).toBeInTheDocument();
    owner.unmount();
    localStorage.clear();

    renderWithAuth(<DashboardLayout />, { role: 'ClinicManager' });
    expect(await screen.findByRole('link', { name: /Consultation Requests/i })).toBeInTheDocument();
  });

  it('redirects a Veterinarian away from /consultations', async () => {
    renderWithAuth(consultationRoute(), { role: 'Veterinarian', route: '/consultations' });

    expect(await screen.findByText('Access Denied (403)')).toBeInTheDocument();
    expect(screen.queryByText('Consultation Requests')).not.toBeInTheDocument();
  });

  it('Veterinarian dashboard has no consultation-request queue or link', async () => {
    renderWithAuth(<VeterinarianDashboard />, { role: 'Veterinarian' });

    expect(await screen.findByText('Upcoming Appointments')).toBeInTheDocument();
    expect(screen.queryByText(/Consultations Needing Attention/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Requests Awaiting Review/)).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /consultation/i })).not.toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: /My Appointments|appointments/i }).length).toBeGreaterThan(0);
  });
});
