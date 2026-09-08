import apiClient from '../../../lib/apiClient';

/**
 * Authentication API service.
 * All auth-related HTTP calls are centralized here.
 */

/**
 * POST /api/auth/login
 * @param {{ email: string, password: string }} credentials
 * @returns {Promise<{ accessToken, expiresAt, user: { id, firstName, lastName, email, role } }>}
 */
export async function login(credentials) {
  const { data } = await apiClient.post('/auth/login', credentials);
  return data.data; // Unwrap ApiResponse<T>
}

/**
 * POST /api/auth/register
 * @param {{ firstName, lastName, email, password, confirmPassword }} userData
 * @returns {Promise<{ id, firstName, lastName, email, role }>}
 */
export async function registerPetOwner(userData) {
  const { data } = await apiClient.post('/auth/register', userData);
  return data.data;
}

/**
 * GET /api/auth/me
 * Requires a valid Bearer token in the request (injected by apiClient interceptor).
 * @returns {Promise<{ id, firstName, lastName, email, role }>}
 */
export async function getCurrentUser() {
  const { data } = await apiClient.get('/auth/me');
  return data.data;
}
