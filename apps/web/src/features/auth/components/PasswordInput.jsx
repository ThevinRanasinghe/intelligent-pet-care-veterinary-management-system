import { useState } from 'react';
import { Eye, EyeOff } from 'lucide-react';

/**
 * PasswordInput — text input with show/hide toggle.
 */
export default function PasswordInput({
  id,
  label,
  value,
  onChange,
  onBlur,
  placeholder = 'Enter your password',
  error,
  autoComplete = 'current-password',
  required,
  disabled,
}) {
  const [visible, setVisible] = useState(false);

  return (
    <div className="form-group">
      <label className="form-label" htmlFor={id}>
        {label}
        {required && <span aria-hidden="true" style={{ color: 'var(--color-error)', marginLeft: '3px' }}>*</span>}
      </label>
      <div className="form-input-wrap">
        <input
          id={id}
          type={visible ? 'text' : 'password'}
          value={value}
          onChange={onChange}
          onBlur={onBlur}
          placeholder={placeholder}
          autoComplete={autoComplete}
          required={required}
          disabled={disabled}
          aria-invalid={!!error}
          aria-describedby={error ? `${id}-error` : undefined}
          className={`form-input has-icon${error ? ' error' : ''}`}
        />
        <button
          type="button"
          className="form-input-icon-btn"
          onClick={() => setVisible((v) => !v)}
          aria-label={visible ? 'Hide password' : 'Show password'}
          tabIndex={0}
        >
          {visible
            ? <EyeOff size={18} aria-hidden="true" />
            : <Eye    size={18} aria-hidden="true" />
          }
        </button>
      </div>
      {error && (
        <p id={`${id}-error`} className="form-error" role="alert">
          <svg width="14" height="14" viewBox="0 0 14 14" fill="none" aria-hidden="true">
            <circle cx="7" cy="7" r="6.5" stroke="currentColor"/>
            <path d="M7 4v3.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
            <circle cx="7" cy="10" r="0.75" fill="currentColor"/>
          </svg>
          {error}
        </p>
      )}
    </div>
  );
}
