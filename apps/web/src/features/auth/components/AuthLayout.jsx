import PetCareIllustration from './PetCareIllustration';

/**
 * AuthLayout — split-screen wrapper for Login and Register pages.
 * Left: Yellow PetCare branding panel
 * Right: Form content (children)
 */
export default function AuthLayout({ children, title, subtitle }) {
  return (
    <div className="auth-root">
      {/* ── LEFT: BRANDING PANEL ── */}
      <aside className="auth-brand-panel" aria-hidden="true">
        <div className="auth-brand-logo">
          <div className="auth-brand-logo-icon">🐾</div>
          <div>
            <div className="auth-brand-logo-text">PetCare AI</div>
          </div>
        </div>

        <div className="auth-brand-illustration">
          <PetCareIllustration size={200} />
        </div>

        <h1 className="auth-brand-heading">
          Your pet's health<br />journey starts here.
        </h1>

        <p className="auth-brand-subtext">
          Manage veterinary care, appointments, and pet health in one caring place.
        </p>

        <div className="auth-brand-pills">
          <span className="auth-brand-pill">🏥 Appointments</span>
          <span className="auth-brand-pill">💊 Inventory</span>
          <span className="auth-brand-pill">📋 Records</span>
          <span className="auth-brand-pill">🤝 Care Teams</span>
        </div>
      </aside>

      {/* ── RIGHT: FORM PANEL ── */}
      <main className="auth-form-panel">
        <div className="auth-form-card">
          <div className="auth-form-header">
            {title && (
              <>
                <p className="auth-form-eyebrow">PetCare AI</p>
                <h2 className="auth-form-title">{title}</h2>
                {subtitle && <p className="auth-form-subtitle">{subtitle}</p>}
              </>
            )}
          </div>
          {children}
        </div>
      </main>
    </div>
  );
}
