/**
 * API boundary for the ASP.NET Core backend.
 * The base URL is configured through VITE_API_BASE_URL (see .env).
 */

export const API_BASE_URL =
  ((import.meta as unknown as { env: Record<string, string> }).env.VITE_API_BASE_URL) ??
  'http://localhost:5000/api';

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
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: { 'Content-Type': 'application/json', ...(options?.headers ?? {}) },
  });

  if (!response.ok) {
    let body: unknown;
    const text = await response.text();
    try {
      body = text ? JSON.parse(text) : undefined;
    } catch {
      body = text;
    }
    throw new ApiError(response.status, body, `API request failed: ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as unknown as T;
  }

  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
