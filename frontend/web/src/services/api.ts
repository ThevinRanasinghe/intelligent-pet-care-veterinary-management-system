/**
 * Central API boundary for the ASP.NET Core backend.
 * Handles authenticated API requests and common HTTP responses.
 */


export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5152/api';


export async function apiRequest<T>(path: string, options?: RequestInit): Promise<T> {
    const response = await fetch(`${API_BASE_URL}${path}`, {
        ...options,
        headers: { 'Content-Type': 'application/json', ...(options?.headers ?? {}) },
    });

    if (!response.ok) {
        throw new Error(`API request failed: ${response.status}`);
    }

    if (response.status === 204) {
        return undefined as T;
    }

    return response.json() as Promise<T>;
}
