import axios from 'axios';

/**
 * Centralized Axios instance.
 * All API base URL and token injection is handled here.
 * Components/pages should NEVER construct raw fetch/axios calls.
 */
const apiClient = axios.create({
  baseURL: '/api', // Proxied to http://localhost:5000/api via Vite config
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 15000,
});

// Request interceptor — inject JWT from localStorage if present
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('petcare_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Response interceptor — handle 401 by clearing stale auth state
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Clear stored token on unauthorized — will trigger redirect via ProtectedRoute
      localStorage.removeItem('petcare_token');
    }
    return Promise.reject(error);
  }
);

export default apiClient;
