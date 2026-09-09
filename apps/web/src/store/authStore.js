import { create } from 'zustand';
import {
  login as apiLogin,
  registerPetOwner as apiRegister,
  registerOrganization as apiRegisterOrg,
  getCurrentUser
} from '../features/auth/services/authApi';

const TOKEN_KEY = 'petcare_token';

/**
 * Global authentication state store (Zustand).
 *
 * Tracks:
 * - user: current user profile (id, firstName, lastName, email, role, organization)
 * - isAuthenticated: boolean
 * - isLoading: global loading state
 * - error: last error message
 *
 * Token is stored in localStorage (keyed by TOKEN_KEY).
 * Passwords are NEVER stored — only the access token.
 */
const useAuthStore = create((set, get) => ({
  // ── State ──────────────────────────────────────────────────────────────────
  user:            null,
  isAuthenticated: false,
  isLoading:       false,
  isInitializing:  true, // true while checking stored session on mount
  error:           null,

  // ── Actions ────────────────────────────────────────────────────────────────

  /**
   * Login with email and password.
   */
  login: async (credentials) => {
    set({ isLoading: true, error: null });
    try {
      const response = await apiLogin(credentials);
      // Store token — never log or expose the token value
      localStorage.setItem(TOKEN_KEY, response.accessToken);
      set({
        user:            response.user,
        isAuthenticated: true,
        isLoading:       false,
        error:           null,
      });
      return response.user;
    } catch (err) {
      const message = extractErrorMessage(err);
      set({ isLoading: false, error: message });
      throw new Error(message);
    }
  },

  /**
   * Register a new PetOwner account.
   */
  register: async (userData) => {
    set({ isLoading: true, error: null });
    try {
      const user = await apiRegister(userData);
      set({ isLoading: false, error: null });
      return user;
    } catch (err) {
      const message = extractErrorMessage(err);
      set({ isLoading: false, error: message });
      throw new Error(message);
    }
  },

  /**
   * Register a new Veterinary Organization and primary ClinicManager.
   */
  registerOrganization: async (orgData) => {
    set({ isLoading: true, error: null });
    try {
      const user = await apiRegisterOrg(orgData);
      set({ isLoading: false, error: null });
      return user;
    } catch (err) {
      const message = extractErrorMessage(err);
      set({ isLoading: false, error: message });
      throw new Error(message);
    }
  },

  /**
   * Restore session from stored token.
   * Called once on app mount.
   */
  initializeAuth: async () => {
    const token = localStorage.getItem(TOKEN_KEY);
    if (!token) {
      set({ isInitializing: false });
      return;
    }
    try {
      const user = await getCurrentUser();
      set({
        user,
        isAuthenticated: true,
        isInitializing:  false,
      });
    } catch {
      // Token expired or invalid — clear it
      localStorage.removeItem(TOKEN_KEY);
      set({ user: null, isAuthenticated: false, isInitializing: false });
    }
  },

  /**
   * Logout — clears all auth state and stored token.
   */
  logout: () => {
    localStorage.removeItem(TOKEN_KEY);
    set({ user: null, isAuthenticated: false, error: null });
  },

  /**
   * Clear any stored error.
   */
  clearError: () => set({ error: null }),

  /**
   * Returns the appropriate dashboard route for the currently logged-in user.
   */
  getHomeRoute: () => {
    const role = get().user?.role;
    switch (role) {
      case 'SuperAdmin':       return '/super-admin';
      case 'ClinicManager':    return '/manager';
      case 'Veterinarian':     return '/vet';
      case 'InventoryOfficer': return '/inventory';
      case 'PetOwner':         return '/pet-owner';
      default:                 return '/login';
    }
  },
}));

// ── Helper ──────────────────────────────────────────────────────────────────

function extractErrorMessage(err) {
  // Axios error with backend response
  if (err?.response?.data?.message) {
    return err.response.data.message;
  }
  // Endpoint 404 Not Found (e.g. backend needs restart after adding new routes)
  if (err?.response?.status === 404) {
    return 'The requested API endpoint was not found (404). Please restart your backend server (dotnet run) to apply latest route changes.';
  }
  // Network / timeout
  if (err?.code === 'ECONNABORTED' || err?.message?.includes('Network Error')) {
    return 'Unable to connect to the backend server. Please verify it is running on http://localhost:5000.';
  }
  // Generic
  return err?.message ?? 'An unexpected error occurred. Please try again.';
}

export default useAuthStore;
