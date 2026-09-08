import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { AlertCircle, CheckCircle } from 'lucide-react';
import AuthLayout from '../components/AuthLayout';
import AuthInput from '../components/AuthInput';
import PasswordInput from '../components/PasswordInput';
import useAuthStore from '../../../store/authStore';

function validateForm({ firstName, lastName, email, password, confirmPassword }) {
  const errors = {};

  if (!firstName.trim()) errors.firstName = 'First name is required.';
  if (!lastName.trim())  errors.lastName  = 'Last name is required.';

  if (!email.trim()) {
    errors.email = 'Email is required.';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    errors.email = 'Please enter a valid email address.';
  }

  if (!password) {
    errors.password = 'Password is required.';
  } else if (password.length < 8) {
    errors.password = 'Password must be at least 8 characters.';
  } else if (!/[A-Z]/.test(password)) {
    errors.password = 'Password must contain at least one uppercase letter.';
  } else if (!/[a-z]/.test(password)) {
    errors.password = 'Password must contain at least one lowercase letter.';
  } else if (!/[0-9]/.test(password)) {
    errors.password = 'Password must contain at least one digit.';
  } else if (!/[^a-zA-Z0-9]/.test(password)) {
    errors.password = 'Password must contain at least one special character.';
  }

  if (!confirmPassword) {
    errors.confirmPassword = 'Please confirm your password.';
  } else if (confirmPassword !== password) {
    errors.confirmPassword = 'Passwords do not match.';
  }

  return errors;
}

/**
 * Registration page for PetOwner accounts.
 * Role is ALWAYS PetOwner — there is no role selector.
 */
export default function RegisterPage() {
  const navigate = useNavigate();
  const { register, isLoading } = useAuthStore();

  const [form, setForm] = useState({
    firstName: '', lastName: '', email: '', password: '', confirmPassword: ''
  });
  const [errors,     setErrors    ] = useState({});
  const [apiError,   setApiError  ] = useState('');
  const [success,    setSuccess   ] = useState(false);

  const handleChange = (field) => (e) => {
    setForm((f) => ({ ...f, [field]: e.target.value }));
    if (errors[field]) setErrors((err) => ({ ...err, [field]: '' }));
    if (apiError) setApiError('');
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
      await register({
        firstName:       form.firstName.trim(),
        lastName:        form.lastName.trim(),
        email:           form.email.trim(),
        password:        form.password,
        confirmPassword: form.confirmPassword,
      });
      setSuccess(true);
      // Navigate to login after short delay
      setTimeout(() => navigate('/login', { replace: true }), 2500);
    } catch (err) {
      setApiError(err.message ?? 'Registration failed. Please try again.');
    }
  };

  return (
    <AuthLayout
      title="Create Account"
      subtitle="Join PetCare AI — your pet deserves the best care."
    >
      <form onSubmit={handleSubmit} noValidate aria-label="Registration form">

        {success && (
          <div className="alert alert-success" role="status">
            <CheckCircle size={18} aria-hidden="true" />
            <span>Account created! Redirecting to login…</span>
          </div>
        )}

        {apiError && (
          <div className="alert alert-error" role="alert">
            <AlertCircle size={18} aria-hidden="true" />
            <span>{apiError}</span>
          </div>
        )}

        <div className="form-row">
          <AuthInput
            id="register-firstName"
            label="First Name"
            type="text"
            value={form.firstName}
            onChange={handleChange('firstName')}
            onBlur={handleBlur('firstName')}
            placeholder="John"
            error={errors.firstName}
            autoComplete="given-name"
            required
            disabled={isLoading || success}
          />
          <AuthInput
            id="register-lastName"
            label="Last Name"
            type="text"
            value={form.lastName}
            onChange={handleChange('lastName')}
            onBlur={handleBlur('lastName')}
            placeholder="Doe"
            error={errors.lastName}
            autoComplete="family-name"
            required
            disabled={isLoading || success}
          />
        </div>

        <AuthInput
          id="register-email"
          label="Email address"
          type="email"
          value={form.email}
          onChange={handleChange('email')}
          onBlur={handleBlur('email')}
          placeholder="you@example.com"
          error={errors.email}
          autoComplete="email"
          required
          disabled={isLoading || success}
        />

        <PasswordInput
          id="register-password"
          label="Password"
          value={form.password}
          onChange={handleChange('password')}
          onBlur={handleBlur('password')}
          placeholder="Min 8 characters"
          error={errors.password}
          autoComplete="new-password"
          required
          disabled={isLoading || success}
        />

        <PasswordInput
          id="register-confirmPassword"
          label="Confirm Password"
          value={form.confirmPassword}
          onChange={handleChange('confirmPassword')}
          onBlur={handleBlur('confirmPassword')}
          placeholder="Repeat your password"
          error={errors.confirmPassword}
          autoComplete="new-password"
          required
          disabled={isLoading || success}
        />

        {/* Security notice — no role selector */}
        <p style={{ fontSize: '0.78rem', color: 'var(--petcare-neutral)', marginBottom: '1.25rem', lineHeight: '1.5' }}>
          🐾 Public registration creates a <strong>Pet Owner</strong> account.
        </p>

        <button
          id="register-submit-btn"
          type="submit"
          className="btn btn-primary"
          disabled={isLoading || success}
          aria-busy={isLoading}
        >
          {isLoading
            ? <><span className="btn-spinner" aria-hidden="true" /> Creating account…</>
            : 'Create Account'
          }
        </button>

        <div className="auth-footer" style={{ marginTop: '1.5rem' }}>
          Already have an account?{' '}
          <Link to="/login" className="auth-footer-link">
            Sign in
          </Link>
        </div>
      </form>
    </AuthLayout>
  );
}
