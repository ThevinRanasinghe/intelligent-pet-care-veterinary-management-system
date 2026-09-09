import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { AlertCircle } from 'lucide-react';
import AuthLayout from '../components/AuthLayout';
import AuthInput from '../components/AuthInput';
import PasswordInput from '../components/PasswordInput';
import useAuthStore from '../../../store/authStore';

const ROLE_ROUTES = {
  SuperAdmin:       '/super-admin',
  ClinicManager:    '/manager',
  Veterinarian:     '/vet',
  InventoryOfficer: '/inventory',
  PetOwner:         '/pet-owner',
};

function validateForm({ email, password }) {
  const errors = {};

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
 */
export default function LoginPage() {
  const navigate = useNavigate();
  const { login, isLoading } = useAuthStore();

  const [form,   setForm  ] = useState({ email: '', password: '' });
  const [errors, setErrors] = useState({});
  const [apiError, setApiError] = useState('');

  const handleChange = (field) => (e) => {
    setForm((f) => ({ ...f, [field]: e.target.value }));
    if (errors[field]) setErrors((err) => ({ ...err, [field]: '' }));
    if (apiError)      setApiError('');
  };

  const handleBlur = (field) => () => {
    const fieldErrors = validateForm(form);
    if (fieldErrors[field]) {
      setErrors((e) => ({ ...e, [field]: fieldErrors[field] }));
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setApiError('');

    const validationErrors = validateForm(form);
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      return;
    }

    try {
      const user = await login({ email: form.email, password: form.password });
      const route = ROLE_ROUTES[user.role] ?? '/';
      navigate(route, { replace: true });
    } catch (err) {
      setApiError(err.message ?? 'Invalid email or password.');
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
          disabled={isLoading}
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
          disabled={isLoading}
        />

        <button
          id="login-submit-btn"
          type="submit"
          className="btn btn-primary"
          disabled={isLoading}
          aria-busy={isLoading}
        >
          {isLoading
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
