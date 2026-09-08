/**
 * AuthInput — styled labeled text input with error state.
 */
export default function AuthInput({
  id,
  label,
  type = 'text',
  value,
  onChange,
  onBlur,
  placeholder,
  error,
  autoComplete,
  required,
  disabled,
  ...props
}) {
  return (
    <div className="form-group">
      <label className="form-label" htmlFor={id}>
        {label}
        {required && <span aria-hidden="true" style={{ color: 'var(--color-error)', marginLeft: '3px' }}>*</span>}
      </label>
      <div className="form-input-wrap">
        <input
          id={id}
          type={type}
          value={value}
          onChange={onChange}
          onBlur={onBlur}
          placeholder={placeholder}
          autoComplete={autoComplete}
          required={required}
          disabled={disabled}
          aria-invalid={!!error}
          aria-describedby={error ? `${id}-error` : undefined}
          className={`form-input${error ? ' error' : ''}`}
          {...props}
        />
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
