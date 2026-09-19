import type { Role } from '../types/domain';

/**
 * localStorage-backed session storage for the signed-in user. Kept
 * separate from authService.ts (which calls the API) and api.ts (which
 * reads the token for the Authorization header) to avoid a circular
 * import between the two.
 */
export interface StoredAuth {
  token: string;
  expiresAt: string;
  userId: string;
  email: string;
  name: string;
  role: Role;
}

const STORAGE_KEY = 'petcare.auth';

export function getStoredAuth(): StoredAuth | null {
  const raw = localStorage.getItem(STORAGE_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as StoredAuth;
  } catch {
    return null;
  }
}

export function setStoredAuth(auth: StoredAuth): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(auth));
}

export function clearStoredAuth(): void {
  localStorage.removeItem(STORAGE_KEY);
}

export function isAuthValid(auth: StoredAuth | null): boolean {
  if (!auth) return false;
  return new Date(auth.expiresAt).getTime() > Date.now();
}
