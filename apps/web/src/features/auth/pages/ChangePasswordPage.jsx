import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ShieldCheck, AlertCircle, CheckCircle, Key } from 'lucide-react';
import AuthLayout from '../components/AuthLayout';
import PasswordInput from '../components/PasswordInput';
import { changePassword } from '../../dashboard/services/adminApi';
import useAuthStore from '../../../store/authStore';

export default function ChangePasswordPage() {
  const navigate = useNavigate();
  const { user, updateUser, getHomeRoute } = useAuthStore();

  const [form, setForm] = useState({
    currentPassword: '',
    newPassword: '',
    confirmNewPassword: ''
  });
  const [errors, setErrors] = useState({});
  const [apiError, setApiError] = useState('');
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);

  const validate = () => {
    const errs = {};
    if (!form.currentPassword) {
      errs.currentPassword = 'Your temporary or current password is required.';
    }
    if (!form.newPassword) {
      errs.newPassword = 'New password is required.';
    } else if (form.newPassword.length < 8) {
      errs.newPassword = 'Password must be at least 8 characters long.';
    } else if (!/[0-9]/.test(form.newPassword)) {
      errs.newPassword = 'Password must include at least one number.';
    } else if (!/[^a-zA-Z0-9]/.test(form.newPassword)) {
      errs.newPassword = 'Password must include at least one special character.';
    } else if (form.newPassword === form.currentPassword) {
      errs.newPassword = 'New password cannot be the same as your temporary password.';
    }

    if (form.confirmNewPassword !== form.newPassword) {
      errs.confirmNewPassword = 'Passwords do not match.';
    }
    return errs;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setApiError('');
    const validationErrors = validate();
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      return;
    }

    setLoading(true);
    try {
      await changePassword({
        currentPassword: form.currentPassword,
        newPassword: form.newPassword,
        confirmNewPassword: form.confirmNewPassword
      });

      setSuccess(true);
      // Clear mustChangePassword on user in Zustand store
      updateUser({ mustChangePassword: false });

      setTimeout(() => {
        const destination = getHomeRoute();
        navigate(destination, { replace: true });
      }, 1500);
    } catch (err) {
      setApiError(err.response?.data?.message || err.message || 'Failed to change password. Please check your temporary password.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout
      title="Create New Password"
      subtitle="Your account requires a new password before you can access your dashboard."
    >
      <div style={{
        padding: '0.875rem',
        borderRadius: '0.5rem',
        backgroundColor: '#EFF6FF',
        border: '1px solid #BFDBFE',
        display: 'flex',
        gap: '0.75rem',
        alignItems: 'flex-start',
        marginBottom: '1.25rem',
        fontSize: '0.85rem',
        color: '#1E40AF'
      }}>
        <Key size={18} style={{ flexShrink: 0, marginTop: '2px', color: '#2563EB' }} />
        <div>
          <strong>First-time sign in:</strong> Your clinic administrator provided you with a temporary password. Enter it below and choose a private, permanent password.
        </div>
      </div>

      {apiError && (
        <div className="alert alert-error" style={{ marginBottom: '1rem' }}>
          <AlertCircle size={18} />
          <span>{apiError}</span>
        </div>
      )}

      {success && (
        <div className="alert alert-success" style={{ marginBottom: '1rem' }}>
          <CheckCircle size={18} />
          <span>Password changed successfully! Redirecting to your organization dashboard…</span>
        </div>
      )}

      <form onSubmit={handleSubmit} noValidate>
        <PasswordInput
          id="current-password"
          label="Temporary / Current Password *"
          value={form.currentPassword}
          onChange={(e) => {
            setForm((f) => ({ ...f, currentPassword: e.target.value }));
            if (errors.currentPassword) setErrors((e) => ({ ...e, currentPassword: '' }));
          }}
          placeholder="Enter the temporary password you received"
          error={errors.currentPassword}
          required
          disabled={loading || success}
        />

        <PasswordInput
          id="new-password"
          label="New Password *"
          value={form.newPassword}
          onChange={(e) => {
            setForm((f) => ({ ...f, newPassword: e.target.value }));
            if (errors.newPassword) setErrors((e) => ({ ...e, newPassword: '' }));
          }}
          placeholder="Min 8 characters, 1 digit, 1 special character"
          error={errors.newPassword}
          required
          disabled={loading || success}
        />

        <PasswordInput
          id="confirm-new-password"
          label="Confirm New Password *"
          value={form.confirmNewPassword}
          onChange={(e) => {
            setForm((f) => ({ ...f, confirmNewPassword: e.target.value }));
            if (errors.confirmNewPassword) setErrors((e) => ({ ...e, confirmNewPassword: '' }));
          }}
          placeholder="Re-type your new password"
          error={errors.confirmNewPassword}
          required
          disabled={loading || success}
        />

        <button
          type="submit"
          className="btn btn-primary"
          disabled={loading || success}
          style={{ marginTop: '0.5rem' }}
        >
          {loading ? (
            <>
              <span className="btn-spinner" aria-hidden="true" />
              Updating Password…
            </>
          ) : (
            <>
              <ShieldCheck size={18} />
              Set Password & Proceed
            </>
          )}
        </button>
      </form>
    </AuthLayout>
  );
}
