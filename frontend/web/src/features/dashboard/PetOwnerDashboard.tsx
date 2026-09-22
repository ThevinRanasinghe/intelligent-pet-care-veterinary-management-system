/**
 * Pet Owner landing/dashboard — pet owner's view of their pets and consultations.
 * Renders inside the shared DashboardLayout shell.
 */
import { Link } from 'react-router-dom';

export function PetOwnerDashboard() {
  return (
    <>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1.25rem', marginBottom: '2rem' }}>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>My Pets</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <Link to="/pets" style={{ fontSize: '0.8rem', color: '#0f6b5f', fontWeight: 700 }}>Manage my pets</Link>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Active Consultations</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <Link to="/consultations" style={{ fontSize: '0.8rem', color: '#0f6b5f', fontWeight: 700 }}>View my consultations</Link>
        </div>
        <div style={{ backgroundColor: '#ffffff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e5e7eb' }}>
          <div style={{ fontSize: '0.85rem', color: '#6b7280', fontWeight: '500' }}>Treatment History</div>
          <div style={{ fontSize: '1.75rem', fontWeight: '900', color: '#111827', marginTop: '0.25rem' }}>—</div>
          <Link to="/care-history" style={{ fontSize: '0.8rem', color: '#0f6b5f', fontWeight: 700 }}>View care history</Link>
        </div>
      </div>

      <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1.75rem' }}>
        <h3 style={{ fontSize: '1.125rem', fontWeight: '600', color: '#111827', marginBottom: '0.5rem' }}>Welcome to Beacon Pet Health</h3>
        <p style={{ color: '#6b7280', fontSize: '0.9rem', lineHeight: '1.6' }}>
          Register and manage your pets, submit consultation requests, track their progress, and review care history. Use your profile card to update your account or change your password.
        </p>
      </div>
    </>
  );
}

export default PetOwnerDashboard;
