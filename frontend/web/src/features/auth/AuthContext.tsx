import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';
import type { Role } from '../../types/domain';
import * as authService from '../../services/authService';
import type { StoredAuth } from '../../utils/authStorage';

interface AuthContextValue {
  user: StoredAuth | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  hasRole: (role: Role) => boolean;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<StoredAuth | null>(() => authService.getCurrentUser());

  const value = useMemo<AuthContextValue>(() => ({
    user,
    isAuthenticated: user !== null,
    login: async (email: string, password: string) => {
      const auth = await authService.login(email, password);
      setUser(auth);
    },
    logout: () => {
      authService.logout();
      setUser(null);
    },
    hasRole: (role: Role) => user?.role === role,
  }), [user]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
