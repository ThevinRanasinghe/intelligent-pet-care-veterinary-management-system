import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { CheckCircle, AlertCircle, ArrowLeft, Lock } from 'lucide-react';
import { useAuth } from './AuthContext';
import { changePassword } from '../../services/authService';
import { messageFrom } from '../../utils/errors';

/**
 * Standalone Change Password page.
 * Calls PUT /api/auth/change-password to update the authenticated user's password.
 */
export function ChangePasswordPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [form, setForm] = useState({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
  const [submitting, setSubmitting] = useState(false);
  const [success, setSuccess] = useState('');
  const [error, setError] = useState('');

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setSuccess('');
    setError('');

    if (form.newPassword !== form.confirmNewPassword) {
      setError('New passwords do not match.');
      setSubmitting(false);
      return;
    }

    try {
      await changePassword({
        currentPassword: form.currentPassword,
        newPassword: form.newPassword,
        confirmNewPassword: form.confirmNewPassword,
      });
      setSuccess('Password changed successfully.');
      setForm({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div style={{ minHeight: '100vh', backgroundColor: 'var(--petcare-cream, #FAFAE9)', padding: '2rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
      <div style={{ maxWidth: '480px', width: '100%', backgroundColor: '#ffffff', borderRadius: '1rem', padding: '2rem', boxShadow: '0 2px 16px rgba(17,17,17,0.07)' }}>
        <button
          onClick={() => navigate(-1)}
          style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', background: 'none', border: 'none', cursor: 'pointer', color: 'var(--petcare-muted, #55554A)', fontSize: '0.875rem', marginBottom: '1rem' }}
        >
          <ArrowLeft size={16} /> Back
        </button>

        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '1.5rem' }}>
          <div style={{ width: '48px', height: '48px', borderRadius: '0.75rem', background: 'var(--petcare-yellow, #FFBE00)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Lock size={24} />
          </div>
          <div>
            <h1 style={{ fontSize: '1.25rem', fontWeight: '800', color: 'var(--petcare-black, #111111)', margin: 0 }}>Change Password</h1>
            <p style={{ fontSize: '0.8rem', color: 'var(--petcare-muted, #55554A)', margin: 0 }}>Account: {user?.email}</p>
          </div>
        </div>

        {success && (
          <div className="alert alert-success" role="status">
            <CheckCircle size={18} />
            <span>{success}</span>
          </div>
        )}
        {error && (
          <div className="alert alert-error" role="alert">
            <AlertCircle size={18} />
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label className="form-label" htmlFor="current-password">Current Password</label>
            <input
              id="current-password"
              type="password"
              required
              value={form.currentPassword}
              onChange={(e) => setForm({ ...form, currentPassword: e.target.value })}
              className="form-input"
              placeholder="Enter current password"
            />
          </div>
          <div className="form-group">
            <label className="form-label" htmlFor="new-password">New Password</label>
            <input
              id="new-password"
              type="password"
              required
              value={form.newPassword}
              onChange={(e) => setForm({ ...form, newPassword: e.target.value })}
              className="form-input"
              placeholder="Min 8 characters, uppercase, number, symbol"
            />
          </div>
          <div className="form-group">
            <label className="form-label" htmlFor="confirm-new-password">Confirm New Password</label>
            <input
              id="confirm-new-password"
              type="password"
              required
              value={form.confirmNewPassword}
              onChange={(e) => setForm({ ...form, confirmNewPassword: e.target.value })}
              className="form-input"
              placeholder="Re-enter new password"
            />
          </div>
          <button type="submit" className="btn btn-primary" disabled={submitting}>
            {submitting ? 'Updating…' : 'Change Password'}
          </button>
        </form>
      </div>
    </div>
  );
}

export default ChangePasswordPage;
