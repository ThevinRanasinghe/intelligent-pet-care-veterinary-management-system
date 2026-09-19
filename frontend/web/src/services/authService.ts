import type { Role } from '../types/domain';
import { apiRequest } from './api';
import { clearStoredAuth, getStoredAuth, isAuthValid, setStoredAuth, type StoredAuth } from '../utils/authStorage';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  userId: string;
  email: string;
  name: string;
  role: Role;
}

export async function login(email: string, password: string): Promise<StoredAuth> {
  const response = await apiRequest<LoginResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password } as LoginRequest),
  });

  const auth: StoredAuth = {
    token: response.token,
    expiresAt: response.expiresAt,
    userId: response.userId,
    email: response.email,
    name: response.name,
    role: response.role,
  };

  setStoredAuth(auth);
  return auth;
}

export function logout(): void {
  clearStoredAuth();
}

export function getCurrentUser(): StoredAuth | null {
  const auth = getStoredAuth();
  return isAuthValid(auth) ? auth : null;
}

export function isAuthenticated(): boolean {
  return getCurrentUser() !== null;
}

export function hasRole(role: Role): boolean {
  return getCurrentUser()?.role === role;
}
