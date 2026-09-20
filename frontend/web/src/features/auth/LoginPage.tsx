import { useState } from 'react';
import type { ChangeEvent, FocusEvent, FormEvent } from 'react';
import { Navigate, Link, useLocation, useNavigate } from 'react-router-dom';
import { AlertCircle } from 'lucide-react';
import AuthLayout from './components/AuthLayout';
import AuthInput from './components/AuthInput';
import PasswordInput from './components/PasswordInput';
import { useAuth } from './AuthContext';
import { getCurrentUser } from '../../services/authService';
import { messageFrom } from '../../utils/errors';
import type { Role } from '../../types/domain';

const ROLE_ROUTES: Record<Role, string> = {
  Administrator: '/super-admin',
  ClinicManager: '/manager',
  Veterinarian: '/vet',
  InventoryOfficer: '/inventory-dashboard',
  PetOwner: '/pet-owner',
};

interface FormState {
  email: string;
  password: string;
}

function validateForm({ email, password }: FormState) {
  const errors: Partial<Record<keyof FormState, string>> = {};

  if (!email.trim()) {
    errors.email = 'Email is required.';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    errors.email = 'Please enter a valid email address.';
  }

  if (!password) {
    errors.password = 'Password is required.';
  }

  return errors;
}

/**
 * Login page — split-screen PetCare branded layout.
 * Handles email/password auth, loading/error states, role-based navigation.
 * Connects to the existing Merge_1 AuthContext (not the source Zustand store).
 */
export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { isAuthenticated, login } = useAuth();

  const [form, setForm] = useState<FormState>({ email: '', password: '' });
  const [errors, setErrors] = useState<Partial<Record<keyof FormState, string>>>({});
  const [apiError, setApiError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  if (isAuthenticated) {
    const redirectTo = (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? '/';
    return <Navigate to={redirectTo} replace />;
  }

  const handleChange = (field: keyof FormState) => (e: ChangeEvent<HTMLInputElement>) => {
    setForm((f) => ({ ...f, [field]: e.target.value }));
    if (errors[field]) setErrors((err) => ({ ...err, [field]: undefined }));
    if (apiError) setApiError('');
  };

  const handleBlur = (field: keyof FormState) => () => {
    const fieldErrors = validateForm(form);
    if (fieldErrors[field]) {
      setErrors((e) => ({ ...e, [field]: fieldErrors[field] }));
    }
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setApiError('');

    const validationErrors = validateForm(form);
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      return;
    }

    setSubmitting(true);
    try {
      await login(form.email, form.password);
      const redirectTo = (location.state as { from?: { pathname: string } } | null)?.from?.pathname;
      if (redirectTo) {
        navigate(redirectTo, { replace: true });
      } else {
        // Read the user from auth state via a fresh getCurrentUser call,
        // since login() returns void in the existing Merge_1 AuthContext.
        const currentUser = getCurrentUser();
        const route = currentUser ? (ROLE_ROUTES[currentUser.role] ?? '/') : '/';
        navigate(route, { replace: true });
      }
    } catch (err) {
      setApiError(messageFrom(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthLayout
      title="Welcome Back"
      subtitle="Sign in to your Beacon Pet Health account."
    >
      <form onSubmit={handleSubmit} noValidate aria-label="Login form">

        {/* API / server error */}
        {apiError && (
          <div className="alert alert-error" role="alert">
            <AlertCircle size={18} aria-hidden="true" />
            <span>{apiError}</span>
          </div>
        )}

        <AuthInput
          id="login-email"
          label="Email address"
          type="email"
          value={form.email}
          onChange={handleChange('email')}
          onBlur={handleBlur('email')}
          placeholder="you@example.com"
          error={errors.email}
          autoComplete="email"
          required
          disabled={submitting}
        />

        <PasswordInput
          id="login-password"
          label="Password"
          value={form.password}
          onChange={handleChange('password')}
          onBlur={handleBlur('password')}
          placeholder="Enter your password"
          error={errors.password}
          autoComplete="current-password"
          required
          disabled={submitting}
        />

        <button
          id="login-submit-btn"
          type="submit"
          className="btn btn-primary"
          disabled={submitting}
          aria-busy={submitting}
        >
          {submitting
            ? <><span className="btn-spinner" aria-hidden="true" /> Signing in…</>
            : 'Sign In'
          }
        </button>

        <div className="auth-footer" style={{ marginTop: '1.5rem' }}>
          Don't have an account?{' '}
          <Link to="/register" className="auth-footer-link">
            Create an account
          </Link>
        </div>
      </form>
    </AuthLayout>
  );
}

export default LoginPage;
