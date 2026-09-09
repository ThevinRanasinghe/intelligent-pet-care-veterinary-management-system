import { useState, useEffect } from 'react';
import {
  User, Lock, Building2, CheckCircle, AlertCircle, X, Shield, Phone, Mail
} from 'lucide-react';
import { getProfile, updateProfile, changePassword } from '../dashboard/services/adminApi';
import useAuthStore from '../../store/authStore';

export default function ProfileModal({ isOpen, onClose }) {
  const { user, setUser } = useAuthStore();
  const [activeTab, setActiveTab] = useState('details'); // 'details' | 'password'

  // Profile Form state
  const [profileForm, setProfileForm] = useState({
    firstName: '',
    lastName: '',
    email: '',
    phoneNumber: '',
    role: '',
    organization: null,
    status: ''
  });
  const [profileLoading, setProfileLoading] = useState(false);
  const [profileSaving, setProfileSaving] = useState(false);
  const [profileSuccess, setProfileSuccess] = useState('');
  const [profileError, setProfileError] = useState('');

  // Password Form state
  const [passwordForm, setPasswordForm] = useState({
    currentPassword: '',
    newPassword: '',
    confirmNewPassword: ''
  });
  const [passwordSaving, setPasswordSaving] = useState(false);
  const [passwordSuccess, setPasswordSuccess] = useState('');
  const [passwordError, setPasswordError] = useState('');

  useEffect(() => {
    if (isOpen) {
      loadProfileData();
    }
  }, [isOpen]);

  const loadProfileData = async () => {
    setProfileLoading(true);
    setProfileError('');
    try {
      const data = await getProfile();
      setProfileForm({
        firstName:    data.firstName || '',
        lastName:     data.lastName || '',
        email:        data.email || '',
        phoneNumber:  data.phoneNumber || '',
        role:         data.role || '',
        organization: data.organization || null,
        status:       data.status || 'Active'
      });
    } catch (err) {
      setProfileError(err.response?.data?.message || err.message || 'Failed to load profile.');
    } finally {
      setProfileLoading(false);
    }
  };

  const handleUpdateProfile = async (e) => {
    e.preventDefault();
    setProfileSaving(true);
    setProfileError('');
    setProfileSuccess('');

    try {
      const updated = await updateProfile({
        firstName:   profileForm.firstName.trim(),
        lastName:    profileForm.lastName.trim(),
        phoneNumber: profileForm.phoneNumber.trim() || null
      });

      setProfileSuccess('Profile updated successfully!');
      // Update global auth store
      if (user) {
        setUser({
          ...user,
          firstName: updated.firstName,
          lastName:  updated.lastName,
          fullName:  updated.fullName
        });
      }
    } catch (err) {
      setProfileError(err.response?.data?.message || err.message || 'Failed to update profile.');
    } finally {
      setProfileSaving(false);
    }
  };

  const handleChangePassword = async (e) => {
    e.preventDefault();
    setPasswordError('');
    setPasswordSuccess('');

    if (passwordForm.newPassword.length < 8) {
      setPasswordError('New password must be at least 8 characters long.');
      return;
    }

    if (passwordForm.newPassword !== passwordForm.confirmNewPassword) {
      setPasswordError('New passwords do not match.');
      return;
    }

    setPasswordSaving(true);
    try {
      await changePassword({
        currentPassword:    passwordForm.currentPassword,
        newPassword:        passwordForm.newPassword,
        confirmNewPassword: passwordForm.confirmNewPassword
      });

      setPasswordSuccess('Password changed successfully!');
      setPasswordForm({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
    } catch (err) {
      setPasswordError(err.response?.data?.message || err.message || 'Failed to change password.');
    } finally {
      setPasswordSaving(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div style={{
      position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)',
      display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1100, padding: '1rem'
    }}>
      <div style={{
        backgroundColor: '#FFFFFF', borderRadius: '0.75rem', maxWidth: '520px', width: '100%',
        padding: '1.75rem', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.15)', maxHeight: '90vh', overflowY: 'auto'
      }}>
        {/* Header */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
          <h3 style={{ fontSize: '1.25rem', fontWeight: '700', color: '#111827', margin: 0 }}>
            Account Settings
          </h3>
          <button
            onClick={onClose}
            style={{ border: 'none', background: 'transparent', cursor: 'pointer', color: '#9CA3AF', padding: '0.25rem' }}
          >
            <X size={20} />
          </button>
        </div>

        {/* Tabs */}
        <div style={{ display: 'flex', gap: '0.5rem', borderBottom: '1px solid #E5E7EB', marginBottom: '1.25rem' }}>
          <button
            type="button"
            onClick={() => setActiveTab('details')}
            style={{
              padding: '0.5rem 1rem', fontSize: '0.875rem', fontWeight: '600',
              border: 'none', borderBottom: activeTab === 'details' ? '2px solid var(--petcare-black, #111111)' : '2px solid transparent',
              background: 'transparent', color: activeTab === 'details' ? '#111827' : '#6B7280', cursor: 'pointer'
            }}
          >
            Profile Information
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('password')}
            style={{
              padding: '0.5rem 1rem', fontSize: '0.875rem', fontWeight: '600',
              border: 'none', borderBottom: activeTab === 'password' ? '2px solid var(--petcare-black, #111111)' : '2px solid transparent',
              background: 'transparent', color: activeTab === 'password' ? '#111827' : '#6B7280', cursor: 'pointer'
            }}
          >
            Security & Password
          </button>
        </div>

        {/* Tab 1: Profile Details */}
        {activeTab === 'details' && (
          <div>
            {profileSuccess && (
              <div className="alert alert-success" style={{ marginBottom: '1rem', fontSize: '0.85rem' }}>
                <CheckCircle size={16} />
                <span>{profileSuccess}</span>
              </div>
            )}
            {profileError && (
              <div className="alert alert-error" style={{ marginBottom: '1rem', fontSize: '0.85rem' }}>
                <AlertCircle size={16} />
                <span>{profileError}</span>
              </div>
            )}

            {/* Readonly Identity Context */}
            <div style={{ backgroundColor: '#F9FAFB', padding: '0.85rem', borderRadius: '0.5rem', border: '1px solid #F3F4F6', marginBottom: '1rem', fontSize: '0.825rem' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', color: '#374151', marginBottom: '0.35rem' }}>
                <Shield size={14} style={{ color: '#4F46E5' }} />
                <span><strong>System Role:</strong> {profileForm.role}</span>
                <span style={{ marginLeft: 'auto', backgroundColor: '#D1FAE5', color: '#065F46', padding: '0.15rem 0.45rem', borderRadius: '4px', fontSize: '0.75rem', fontWeight: '600' }}>
                  {profileForm.status}
                </span>
              </div>
              {profileForm.organization && (
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', color: '#374151' }}>
                  <Building2 size={14} style={{ color: '#F59E0B' }} />
                  <span><strong>Organization:</strong> {profileForm.organization.name}</span>
                </div>
              )}
            </div>

            <form onSubmit={handleUpdateProfile}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginBottom: '0.75rem' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                    First Name
                  </label>
                  <input
                    type="text"
                    required
                    value={profileForm.firstName}
                    onChange={(e) => setProfileForm({ ...profileForm, firstName: e.target.value })}
                    style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                    Last Name
                  </label>
                  <input
                    type="text"
                    required
                    value={profileForm.lastName}
                    onChange={(e) => setProfileForm({ ...profileForm, lastName: e.target.value })}
                    style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                  />
                </div>
              </div>

              <div style={{ marginBottom: '0.75rem' }}>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                  Email Address
                </label>
                <input
                  type="email"
                  disabled
                  value={profileForm.email}
                  style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #E5E7EB', backgroundColor: '#F3F4F6', color: '#6B7280' }}
                />
                <span style={{ fontSize: '0.75rem', color: '#9CA3AF' }}>Login email is managed by your system administrator.</span>
              </div>

              <div style={{ marginBottom: '1.25rem' }}>
                <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                  Phone Number
                </label>
                <input
                  type="tel"
                  placeholder="+1 555-0100"
                  value={profileForm.phoneNumber}
                  onChange={(e) => setProfileForm({ ...profileForm, phoneNumber: e.target.value })}
                  style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
                <button
                  type="button"
                  onClick={onClose}
                  style={{ padding: '0.5rem 1rem', backgroundColor: '#FFFFFF', border: '1px solid #D1D5DB', borderRadius: '0.375rem', fontSize: '0.875rem', cursor: 'pointer' }}
                >
                  Close
                </button>
                <button
                  type="submit"
                  disabled={profileSaving || profileLoading}
                  style={{
                    padding: '0.5rem 1.25rem', backgroundColor: 'var(--petcare-black, #111111)', color: '#FFFFFF',
                    border: 'none', borderRadius: '0.375rem', fontWeight: '600', fontSize: '0.875rem', cursor: 'pointer'
                  }}
                >
                  {profileSaving ? 'Saving…' : 'Save Changes'}
                </button>
              </div>
            </form>
          </div>
        )}

        {/* Tab 2: Security & Password */}
        {activeTab === 'password' && (
          <form onSubmit={handleChangePassword}>
            {passwordSuccess && (
              <div className="alert alert-success" style={{ marginBottom: '1rem', fontSize: '0.85rem' }}>
                <CheckCircle size={16} />
                <span>{passwordSuccess}</span>
              </div>
            )}
            {passwordError && (
              <div className="alert alert-error" style={{ marginBottom: '1rem', fontSize: '0.85rem' }}>
                <AlertCircle size={16} />
                <span>{passwordError}</span>
              </div>
            )}

            <div style={{ marginBottom: '0.75rem' }}>
              <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                Current Password *
              </label>
              <input
                type="password"
                required
                value={passwordForm.currentPassword}
                onChange={(e) => setPasswordForm({ ...passwordForm, currentPassword: e.target.value })}
                style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
              />
            </div>

            <div style={{ marginBottom: '0.75rem' }}>
              <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                New Password *
              </label>
              <input
                type="password"
                required
                placeholder="Min 8 characters, uppercase, number, symbol"
                value={passwordForm.newPassword}
                onChange={(e) => setPasswordForm({ ...passwordForm, newPassword: e.target.value })}
                style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
              />
            </div>

            <div style={{ marginBottom: '1.25rem' }}>
              <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: '600', color: '#374151', marginBottom: '0.25rem' }}>
                Confirm New Password *
              </label>
              <input
                type="password"
                required
                placeholder="Re-enter new password"
                value={passwordForm.confirmNewPassword}
                onChange={(e) => setPasswordForm({ ...passwordForm, confirmNewPassword: e.target.value })}
                style={{ width: '100%', padding: '0.5rem', fontSize: '0.875rem', borderRadius: '0.375rem', border: '1px solid #D1D5DB' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
              <button
                type="button"
                onClick={onClose}
                style={{ padding: '0.5rem 1rem', backgroundColor: '#FFFFFF', border: '1px solid #D1D5DB', borderRadius: '0.375rem', fontSize: '0.875rem', cursor: 'pointer' }}
              >
                Close
              </button>
              <button
                type="submit"
                disabled={passwordSaving}
                style={{
                  padding: '0.5rem 1.25rem', backgroundColor: 'var(--petcare-black, #111111)', color: '#FFFFFF',
                  border: 'none', borderRadius: '0.375rem', fontWeight: '600', fontSize: '0.875rem', cursor: 'pointer'
                }}
              >
                {passwordSaving ? 'Updating…' : 'Change Password'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
