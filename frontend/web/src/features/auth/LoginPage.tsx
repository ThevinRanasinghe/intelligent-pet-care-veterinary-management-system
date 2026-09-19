import { useState } from 'react';
import type { FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { LogIn, PawPrint } from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { messageFrom } from '../../utils/errors';
import { useAuth } from './AuthContext';

export function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  if (isAuthenticated) {
    const redirectTo = (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? '/';
    return <Navigate to={redirectTo} replace />;
  }

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError('');
    setSubmitting(true);
    try {
      await login(email, password);
      const redirectTo = (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? '/';
      navigate(redirectTo, { replace: true });
    } catch (err) {
      setError(messageFrom(err));
    } finally {
      setSubmitting(false);
    }
  };

  return <div className="login-page">
    <Card className="login-card">
      <div className="login-brand"><span className="brand-mark"><PawPrint size={23} /></span><div><strong>PetCare AI</strong><small>Clinic operations console</small></div></div>
      <h2>Sign in</h2>
      <p className="login-subtitle">Use your staff account to access scheduling, billing and approvals.</p>
      {error && <div className="error-banner">{error}</div>}
      <form onSubmit={onSubmit} className="login-form">
        <label>Email
          <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="you@petcare.lk" autoComplete="username" />
        </label>
        <label>Password
          <input type="password" required value={password} onChange={(e) => setPassword(e.target.value)} placeholder="••••••••" autoComplete="current-password" />
        </label>
        <Button type="submit" disabled={submitting} icon={<LogIn size={16} />}>{submitting ? 'Signing in...' : 'Sign in'}</Button>
      </form>
    </Card>
  </div>;
}
