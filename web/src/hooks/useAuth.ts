import { useState, useCallback } from 'react';
import { getToken, setToken, clearToken, parseJwt, isTokenExpired } from '../lib/auth';
import { api } from '../lib/api';

export interface AuthUser {
  id: string;
  email: string;
  role: string;
  callsign: string;
}

function userFromToken(token: string): AuthUser | null {
  const payload = parseJwt(token);
  if (!payload) return null;
  return {
    id: payload.sub as string,
    email: payload.email as string,
    // .NET ClaimTypes.Role maps to this long URI in JWT
    role: (payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] as string)
          ?? (payload.role as string),
    callsign: (payload.callsign as string) ?? '',
  };
}

export function useAuth() {
  // Initialize from localStorage on mount — AUTH-04 session persistence
  const [user, setUser] = useState<AuthUser | null>(() => {
    const token = getToken();
    if (!token || isTokenExpired(token)) return null;
    return userFromToken(token);
  });

  const login = useCallback((token: string) => {
    setToken(token);
    setUser(userFromToken(token));
  }, []);

  const logout = useCallback(async () => {
    try { await api.post('/auth/logout'); } catch { /* ignore network errors on logout */ }
    clearToken();
    setUser(null);
  }, []);

  return {
    user,
    isAuthenticated: user !== null,
    login,
    logout,
  };
}
