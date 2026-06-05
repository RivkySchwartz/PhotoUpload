import React, { createContext, useContext, useState, useCallback } from 'react';
import { login as apiLogin } from '../api/galleries';
import type { LoginRequest, UserRole } from '../types';

interface AuthContextValue {
  username: string | null;
  role: UserRole | null;
  mustChangePassword: boolean;
  isAuthenticated: boolean;
  login: (data: LoginRequest) => Promise<void>;
  logout: () => void;
  clearMustChangePassword: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  // If a session exists but has no role (pre-role-system cache), wipe it so the user is forced to log in again.
  const storedUsername = sessionStorage.getItem('username');
  const storedRole = sessionStorage.getItem('role') as UserRole | null;
  if (storedUsername && !storedRole) {
    sessionStorage.removeItem('token');
    sessionStorage.removeItem('username');
    sessionStorage.removeItem('mustChangePassword');
  }

  const [username, setUsername] = useState<string | null>(
    () => storedRole ? storedUsername : null
  );
  const [role, setRole] = useState<UserRole | null>(
    () => storedRole
  );
  const [mustChangePassword, setMustChangePassword] = useState<boolean>(
    () => sessionStorage.getItem('mustChangePassword') === 'true'
  );

  const login = useCallback(async (data: LoginRequest) => {
    const res = await apiLogin(data);
    sessionStorage.setItem('token', res.token);
    sessionStorage.setItem('username', res.username);
    sessionStorage.setItem('role', res.role);
    sessionStorage.setItem('mustChangePassword', String(res.mustChangePassword));
    setUsername(res.username);
    setRole(res.role);
    setMustChangePassword(res.mustChangePassword);
  }, []);

  const logout = useCallback(() => {
    sessionStorage.removeItem('token');
    sessionStorage.removeItem('username');
    sessionStorage.removeItem('role');
    sessionStorage.removeItem('mustChangePassword');
    setUsername(null);
    setRole(null);
    setMustChangePassword(false);
  }, []);

  const clearMustChangePassword = useCallback(() => {
    sessionStorage.setItem('mustChangePassword', 'false');
    setMustChangePassword(false);
  }, []);

  return (
    <AuthContext.Provider value={{
      username,
      role,
      mustChangePassword,
      isAuthenticated: !!username,
      login,
      logout,
      clearMustChangePassword,
    }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
