import { useState } from 'react';
import type { ChangeEvent, FocusEvent, FormEvent } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { AlertCircle, CheckCircle, Building2, User, ArrowRight, ArrowLeft } from 'lucide-react';
import AuthLayout from './components/AuthLayout';
import AuthInput from './components/AuthInput';
import PasswordInput from './components/PasswordInput';
import { registerPetOwner, registerOrganization, type RegisterPetOwnerRequest, type RegisterOrganizationRequest } from '../../services/authService';
import { messageFrom } from '../../utils/errors';

type RegType = 'pet-owner' | 'organization';

interface PetOwnerForm {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

interface OrgForm {
  organizationName: string;
  registrationNumber: string;
  organizationEmail: string;
  organizationPhone: string;
  address: string;
  city: string;
  country: string;
  managerFirstName: string;
  managerLastName: string;
  managerEmail: string;
  password: string;
  confirmPassword: string;
}

type PetOwnerErrors = Partial<Record<keyof PetOwnerForm, string>>;
type OrgErrors = Partial<Record<keyof OrgForm, string>>;

function validatePassword(password: string): string | null {
  if (!password) return 'Password is required.';
  if (password.length < 8) return 'Password must be at least 8 characters.';
  if (!/[A-Z]/.test(password)) return 'Password must contain at least one uppercase letter.';
  if (!/[a-z]/.test(password)) return 'Password must contain at least one lowercase letter.';
  if (!/[0-9]/.test(password)) return 'Password must contain at least one digit.';
  if (!/[^a-zA-Z0-9]/.test(password)) return 'Password must contain at least one special character.';
  return null;
}

function validatePetOwnerForm(form: PetOwnerForm): PetOwnerErrors {
  const errors: PetOwnerErrors = {};

  if (!form.firstName.trim()) errors.firstName = 'First name is required.';
  if (!form.lastName.trim()) errors.lastName = 'Last name is required.';

  if (!form.email.trim()) {
    errors.email = 'Email is required.';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
    errors.email = 'Please enter a valid email address.';
  }

  const pwdError = validatePassword(form.password);
  if (pwdError) errors.password = pwdError;

  if (!form.confirmPassword) {
    errors.confirmPassword = 'Please confirm your password.';
  } else if (form.confirmPassword !== form.password) {
    errors.confirmPassword = 'Passwords do not match.';
  }

  return errors;
}

function validateOrgStep1(form: OrgForm): OrgErrors {
  const errors: OrgErrors = {};
  if (!form.organizationName.trim()) errors.organizationName = 'Organization name is required.';
  if (!form.organizationEmail.trim()) {
    errors.organizationEmail = 'Organization email is required.';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.organizationEmail)) {
    errors.organizationEmail = 'Please enter a valid organization email address.';
  }
  if (!form.organizationPhone.trim()) errors.organizationPhone = 'Phone number is required.';
  if (!form.address.trim()) errors.address = 'Clinic address is required.';
  if (!form.city.trim()) errors.city = 'City is required.';
  if (!form.country.trim()) errors.country = 'Country is required.';
  return errors;
}

function validateOrgStep2(form: OrgForm): OrgErrors {
  const errors: OrgErrors = {};
  if (!form.managerFirstName.trim()) errors.managerFirstName = 'Manager first name is required.';
  if (!form.managerLastName.trim()) errors.managerLastName = 'Manager last name is required.';
  if (!form.managerEmail.trim()) {
    errors.managerEmail = 'Manager email is required.';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.managerEmail)) {
    errors.managerEmail = 'Please enter a valid manager email address.';
  }

  const pwdError = validatePassword(form.password);
  if (pwdError) errors.password = pwdError;

  if (!form.confirmPassword) {
    errors.confirmPassword = 'Please confirm your password.';
  } else if (form.confirmPassword !== form.password) {
    errors.confirmPassword = 'Passwords do not match.';
  }
  return errors;
}

/**
 * Registration page for Beacon Pet Health.
 * Provides two distinct registration paths:
 * 1. Pet Owner Registration
 * 2. Veterinary Organization Registration (2-step wizard: Clinic Details -> Clinic Manager Account)
 */
export function RegisterPage() {
  const navigate = useNavigate();

  const [regType, setRegType] = useState<RegType>('pet-owner');
  const [orgStep, setOrgStep] = useState(1);

  const [petOwnerForm, setPetOwnerForm] = useState<PetOwnerForm>({
    firstName: '',
    lastName: '',
    email: '',
    password: '',
    confirmPassword: '',
  });

  const [orgForm, setOrgForm] = useState<OrgForm>({
    organizationName: '',
    registrationNumber: '',
    organizationEmail: '',
    organizationPhone: '',
    address: '',
    city: '',
    country: '',
    managerFirstName: '',
    managerLastName: '',
    managerEmail: '',
    password: '',
    confirmPassword: '',
  });

  const [errors, setErrors] = useState<PetOwnerErrors | OrgErrors>({});
  const [apiError, setApiError] = useState('');
  const [success, setSuccess] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  // Helper to read an error message for any field regardless of which form is active
  const fieldError = (field: string): string | undefined => {
    const all = errors as Record<string, string | undefined>;
    return all[field];
  };

  const handleTypeChange = (type: RegType) => {
    setRegType(type);
    setOrgStep(1);
    setErrors({});
    setApiError('');
    setSuccess(false);
  };

  const handlePetOwnerChange = (field: keyof PetOwnerForm) => (e: ChangeEvent<HTMLInputElement>) => {
    setPetOwnerForm((prev) => ({ ...prev, [field]: e.target.value }));
    if (fieldError(field)) setErrors((err) => ({ ...err, [field]: undefined }));
    if (apiError) setApiError('');
  };

  const handlePetOwnerBlur = (field: keyof PetOwnerForm) => () => {
    const fieldErrors = validatePetOwnerForm(petOwnerForm);
    if (fieldErrors[field]) {
      setErrors((e) => ({ ...e, [field]: fieldErrors[field] }));
    }
  };

  const handleOrgChange = (field: keyof OrgForm) => (e: ChangeEvent<HTMLInputElement>) => {
    setOrgForm((prev) => ({ ...prev, [field]: e.target.value }));
    if (fieldError(field)) setErrors((err) => ({ ...err, [field]: undefined }));
    if (apiError) setApiError('');
  };

  const handleOrgBlur = (field: keyof OrgForm) => () => {
    const stepErrors = orgStep === 1 ? validateOrgStep1(orgForm) : validateOrgStep2(orgForm);
    if (stepErrors[field]) {
      setErrors((e) => ({ ...e, [field]: stepErrors[field] }));
    }
  };

  const handleOrgNextStep = (e: FormEvent) => {
    e.preventDefault();
    setApiError('');

    const step1Errors = validateOrgStep1(orgForm);
    if (Object.keys(step1Errors).length > 0) {
      setErrors(step1Errors);
      return;
    }

    setErrors({});
    setOrgStep(2);
  };

  const handleOrgPrevStep = () => {
    setApiError('');
    setErrors({});
    setOrgStep(1);
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setApiError('');

    if (regType === 'pet-owner') {
      const validationErrors = validatePetOwnerForm(petOwnerForm);
      if (Object.keys(validationErrors).length > 0) {
        setErrors(validationErrors);
        return;
      }

      setSubmitting(true);
      try {
        await registerPetOwner({
          firstName: petOwnerForm.firstName.trim(),
          lastName: petOwnerForm.lastName.trim(),
          email: petOwnerForm.email.trim(),
          password: petOwnerForm.password,
          confirmPassword: petOwnerForm.confirmPassword,
        } as RegisterPetOwnerRequest);
        setSuccess(true);
        setTimeout(() => navigate('/login', { replace: true }), 2500);
      } catch (err) {
        setApiError(messageFrom(err));
      } finally {
        setSubmitting(false);
      }
    } else {
      const step1Errors = validateOrgStep1(orgForm);
      if (Object.keys(step1Errors).length > 0) {
        setOrgStep(1);
        setErrors(step1Errors);
        return;
      }

      const step2Errors = validateOrgStep2(orgForm);
      if (Object.keys(step2Errors).length > 0) {
        setErrors(step2Errors);
        return;
      }

      setSubmitting(true);
      try {
        await registerOrganization({
          organizationName: orgForm.organizationName.trim(),
          registrationNumber: orgForm.registrationNumber.trim() || null,
          organizationEmail: orgForm.organizationEmail.trim(),
          organizationPhone: orgForm.organizationPhone.trim(),
          address: orgForm.address.trim(),
          city: orgForm.city.trim(),
          country: orgForm.country.trim(),
          managerFirstName: orgForm.managerFirstName.trim(),
          managerLastName: orgForm.managerLastName.trim(),
          managerEmail: orgForm.managerEmail.trim(),
          password: orgForm.password,
          confirmPassword: orgForm.confirmPassword,
        } as RegisterOrganizationRequest);
        setSuccess(true);
        setTimeout(() => navigate('/login', { replace: true }), 4000);
      } catch (err) {
        setApiError(messageFrom(err));
      } finally {
        setSubmitting(false);
      }
    }
  };

  const handleBlurWrapper = (field: string) => (e: FocusEvent<HTMLInputElement>) => {
    if (regType === 'pet-owner') {
      handlePetOwnerBlur(field as keyof PetOwnerForm)();
    } else {
      handleOrgBlur(field as keyof OrgForm)();
    }
  };

  return (
    <AuthLayout
      title="Create an Account"
      subtitle="Join Beacon Pet Health — the unified platform for veterinary care."
    >
      {/* REGISTRATION TYPE SWITCHER */}
      <div className="reg-type-switcher" role="tablist" aria-label="Registration type">
        <button
          type="button"
          role="tab"
          aria-selected={regType === 'pet-owner'}
          className={`reg-type-btn ${regType === 'pet-owner' ? 'active' : ''}`}
          onClick={() => handleTypeChange('pet-owner')}
          disabled={submitting || success}
        >
          <User size={16} aria-hidden="true" />
          <span>Pet Owner</span>
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={regType === 'organization'}
          className={`reg-type-btn ${regType === 'organization' ? 'active' : ''}`}
          onClick={() => handleTypeChange('organization')}
          disabled={submitting || success}
        >
          <Building2 size={16} aria-hidden="true" />
          <span>Veterinary Organization</span>
        </button>
      </div>

      {/* SUCCESS / ERROR NOTICES */}
      {success && (
        <div className="alert alert-success" role="status">
          <CheckCircle size={18} aria-hidden="true" />
          <span>
            {regType === 'pet-owner'
              ? 'Account created successfully! Redirecting to login…'
              : 'Your veterinary organization registration has been submitted successfully and is awaiting Beacon administrator verification.'}
          </span>
        </div>
      )}

      {apiError && (
        <div className="alert alert-error" role="alert">
          <AlertCircle size={18} aria-hidden="true" />
          <span>{apiError}</span>
        </div>
      )}

      {/* PATH A: PET OWNER REGISTRATION */}
      {regType === 'pet-owner' && (
        <form onSubmit={handleSubmit} noValidate aria-label="Pet Owner Registration form">
          <div className="org-info-callout">
            🐾 Register as an individual Pet Owner to book appointments, review treatments, and manage your pet's medical records.
          </div>

          <div className="form-row">
            <AuthInput
              id="petowner-firstName"
              label="First Name"
              type="text"
              value={petOwnerForm.firstName}
              onChange={handlePetOwnerChange('firstName')}
              onBlur={handlePetOwnerBlur('firstName')}
              placeholder="John"
              error={fieldError('firstName')}
              autoComplete="given-name"
              required
              disabled={submitting || success}
            />
            <AuthInput
              id="petowner-lastName"
              label="Last Name"
              type="text"
              value={petOwnerForm.lastName}
              onChange={handlePetOwnerChange('lastName')}
              onBlur={handlePetOwnerBlur('lastName')}
              placeholder="Doe"
              error={fieldError('lastName')}
              autoComplete="family-name"
              required
              disabled={submitting || success}
            />
          </div>

          <AuthInput
            id="petowner-email"
            label="Email Address"
            type="email"
            value={petOwnerForm.email}
            onChange={handlePetOwnerChange('email')}
            onBlur={handlePetOwnerBlur('email')}
            placeholder="you@example.com"
            error={fieldError('email')}
            autoComplete="email"
            required
            disabled={submitting || success}
          />

          <PasswordInput
            id="petowner-password"
            label="Password"
            value={petOwnerForm.password}
            onChange={handlePetOwnerChange('password')}
            onBlur={handlePetOwnerBlur('password')}
            placeholder="Min 8 characters (mixed case, digit, symbol)"
            error={fieldError('password')}
            autoComplete="new-password"
            required
            disabled={submitting || success}
          />

          <PasswordInput
            id="petowner-confirmPassword"
            label="Confirm Password"
            value={petOwnerForm.confirmPassword}
            onChange={handlePetOwnerChange('confirmPassword')}
            onBlur={handlePetOwnerBlur('confirmPassword')}
            placeholder="Repeat your password"
            error={fieldError('confirmPassword')}
            autoComplete="new-password"
            required
            disabled={submitting || success}
          />

          <button
            id="register-submit-btn"
            type="submit"
            className="btn btn-primary"
            disabled={submitting || success}
            aria-busy={submitting}
          >
            {submitting
              ? <><span className="btn-spinner" aria-hidden="true" /> Creating account…</>
              : 'Create Pet Owner Account'
            }
          </button>
        </form>
      )}

      {/* PATH B: VETERINARY ORGANIZATION (2-STEP WIZARD) */}
      {regType === 'organization' && (
        <div>
          {/* Wizard Step Indicator */}
          <div className="wizard-progress" aria-label="Organization registration steps">
            <div className={`wizard-step-pill ${orgStep === 1 ? 'active' : 'completed'}`}>
              <span className="wizard-step-num">{orgStep > 1 ? '✓' : '1'}</span>
              <span>1. Clinic Details</span>
            </div>
            <div className="wizard-step-divider" />
            <div className={`wizard-step-pill ${orgStep === 2 ? 'active' : ''}`}>
              <span className="wizard-step-num">2</span>
              <span>2. Clinic Manager</span>
            </div>
          </div>

          {/* STEP 1: ORGANIZATION DETAILS */}
          {orgStep === 1 && (
            <form onSubmit={handleOrgNextStep} noValidate aria-label="Organization Details Form">
              <div className="org-info-callout">
                🏥 <strong>Step 1 of 2: Register your veterinary organization.</strong><br />
                Enter your official clinic information. In the next step, you will set up your primary Clinic Manager administrator account.
              </div>

              <AuthInput
                id="org-name"
                label="Organization / Clinic Name"
                type="text"
                value={orgForm.organizationName}
                onChange={handleOrgChange('organizationName')}
                onBlur={handleOrgBlur('organizationName')}
                placeholder="e.g. Happy Paws Veterinary Hospital"
                error={fieldError('organizationName')}
                required
                disabled={submitting || success}
              />

              <div className="form-row">
                <AuthInput
                  id="org-regNumber"
                  label="Registration No. (Optional)"
                  type="text"
                  value={orgForm.registrationNumber}
                  onChange={handleOrgChange('registrationNumber')}
                  placeholder="e.g. VET-2026-081"
                  error={fieldError('registrationNumber')}
                  disabled={submitting || success}
                />
                <AuthInput
                  id="org-phone"
                  label="Clinic Phone Number"
                  type="tel"
                  value={orgForm.organizationPhone}
                  onChange={handleOrgChange('organizationPhone')}
                  onBlur={handleOrgBlur('organizationPhone')}
                  placeholder="+1 555-0199"
                  error={fieldError('organizationPhone')}
                  required
                  disabled={submitting || success}
                />
              </div>

              <AuthInput
                id="org-email"
                label="Organization Contact Email"
                type="email"
                value={orgForm.organizationEmail}
                onChange={handleOrgChange('organizationEmail')}
                onBlur={handleOrgBlur('organizationEmail')}
                placeholder="contact@happypaws.com"
                error={fieldError('organizationEmail')}
                required
                disabled={submitting || success}
              />

              <AuthInput
                id="org-address"
                label="Street Address"
                type="text"
                value={orgForm.address}
                onChange={handleOrgChange('address')}
                onBlur={handleOrgBlur('address')}
                placeholder="123 Veterinary Blvd"
                error={fieldError('address')}
                required
                disabled={submitting || success}
              />

              <div className="form-row">
                <AuthInput
                  id="org-city"
                  label="City"
                  type="text"
                  value={orgForm.city}
                  onChange={handleOrgChange('city')}
                  onBlur={handleOrgBlur('city')}
                  placeholder="Metropolis"
                  error={fieldError('city')}
                  required
                  disabled={submitting || success}
                />
                <AuthInput
                  id="org-country"
                  label="Country"
                  type="text"
                  value={orgForm.country}
                  onChange={handleOrgChange('country')}
                  onBlur={handleOrgBlur('country')}
                  placeholder="United States"
                  error={fieldError('country')}
                  required
                  disabled={submitting || success}
                />
              </div>

              <button
                id="org-next-step-btn"
                type="submit"
                className="btn btn-primary"
                style={{ marginTop: '1rem' }}
              >
                <span>Continue to Manager Account</span>
                <ArrowRight size={18} aria-hidden="true" />
              </button>
            </form>
          )}

          {/* STEP 2: PRIMARY CLINIC MANAGER ACCOUNT */}
          {orgStep === 2 && (
            <form onSubmit={handleSubmit} noValidate aria-label="Clinic Manager Account Form">
              <div className="org-info-callout">
                👤 <strong>Step 2 of 2: Primary Clinic Manager Account</strong><br />
                Create the administrator account for <strong>{orgForm.organizationName || 'your organization'}</strong>. You will automatically receive the <strong>ClinicManager</strong> role.
              </div>

              <div className="form-row">
                <AuthInput
                  id="manager-firstName"
                  label="Manager First Name"
                  type="text"
                  value={orgForm.managerFirstName}
                  onChange={handleOrgChange('managerFirstName')}
                  onBlur={handleOrgBlur('managerFirstName')}
                  placeholder="Jane"
                  error={fieldError('managerFirstName')}
                  autoComplete="given-name"
                  required
                  disabled={submitting || success}
                />
                <AuthInput
                  id="manager-lastName"
                  label="Manager Last Name"
                  type="text"
                  value={orgForm.managerLastName}
                  onChange={handleOrgChange('managerLastName')}
                  onBlur={handleOrgBlur('managerLastName')}
                  placeholder="Smith"
                  error={fieldError('managerLastName')}
                  autoComplete="family-name"
                  required
                  disabled={submitting || success}
                />
              </div>

              <AuthInput
                id="manager-email"
                label="Manager Login Email"
                type="email"
                value={orgForm.managerEmail}
                onChange={handleOrgChange('managerEmail')}
                onBlur={handleOrgBlur('managerEmail')}
                placeholder="manager@happypaws.com"
                error={fieldError('managerEmail')}
                autoComplete="email"
                required
                disabled={submitting || success}
              />

              <PasswordInput
                id="manager-password"
                label="Manager Password"
                value={orgForm.password}
                onChange={handleOrgChange('password')}
                onBlur={handleOrgBlur('password')}
                placeholder="Min 8 characters (mixed case, digit, symbol)"
                error={fieldError('password')}
                autoComplete="new-password"
                required
                disabled={submitting || success}
              />

              <PasswordInput
                id="manager-confirmPassword"
                label="Confirm Password"
                value={orgForm.confirmPassword}
                onChange={handleOrgChange('confirmPassword')}
                onBlur={handleOrgBlur('confirmPassword')}
                placeholder="Repeat password"
                error={fieldError('confirmPassword')}
                autoComplete="new-password"
                required
                disabled={submitting || success}
              />

              <div className="wizard-actions">
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={handleOrgPrevStep}
                  disabled={submitting || success}
                >
                  <ArrowLeft size={16} aria-hidden="true" />
                  <span>Back</span>
                </button>

                <button
                  id="register-org-submit-btn"
                  type="submit"
                  className="btn btn-primary"
                  disabled={submitting || success}
                  aria-busy={submitting}
                >
                  {submitting
                    ? <><span className="btn-spinner" aria-hidden="true" /> Registering Organization…</>
                    : 'Complete Registration'
                  }
                </button>
              </div>
            </form>
          )}
        </div>
      )}

      <div className="auth-footer" style={{ marginTop: '1.5rem' }}>
        Already have an account?{' '}
        <Link to="/login" className="auth-footer-link">
          Sign In
        </Link>
      </div>
    </AuthLayout>
  );
}

export default RegisterPage;
