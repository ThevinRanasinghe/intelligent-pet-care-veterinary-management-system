/**
 * API boundary for the ASP.NET Core backend.
 * The base URL is configured through VITE_API_BASE_URL (see .env).
 */
import { clearStoredAuth, getStoredAuth } from '../utils/authStorage';

export const API_BASE_URL =
  ((import.meta as unknown as { env: Record<string, string> }).env.VITE_API_BASE_URL) ??
  'http://localhost:5080/api';

export class ApiError extends Error {
  status: number;
  body: unknown;

  constructor(status: number, body: unknown, message: string) {
    super(message);
    this.status = status;
    this.body = body;
    this.name = 'ApiError';
  }
}

export async function apiRequest<T>(path: string, options?: RequestInit): Promise<T> {
  const token = getStoredAuth()?.token;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(options?.headers ?? {}),
    },
  });

  if (!response.ok) {
    let body: unknown;
    const text = await response.text();
    try {
      body = text ? JSON.parse(text) : undefined;
    } catch {
      body = text;
    }

    // The session is no longer valid (expired/invalid token): clear it so
    // the next protected-route check redirects to /login. 403 is left
    // alone since it means "authenticated but not permitted", not "please
    // log in again".
    if (response.status === 401) {
      clearStoredAuth();
    }

    throw new ApiError(response.status, body, `API request failed: ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as unknown as T;
  }

  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
