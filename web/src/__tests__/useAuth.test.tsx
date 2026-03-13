import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useAuth } from '../hooks/useAuth';
import { setToken } from '../lib/auth';

// Helper: create a JWT with a given exp claim (without signing — for test only)
function makeTestJwt(exp: number, overrides: Record<string, unknown> = {}): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payload = btoa(
    JSON.stringify({
      sub: 'user-1',
      email: 'test@example.com',
      role: 'Player',
      callsign: 'Alpha1',
      exp,
      ...overrides,
    })
  );
  return `${header}.${payload}.fakesig`;
}

describe('useAuth hook', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('user is null on mount when localStorage is empty', () => {
    const { result } = renderHook(() => useAuth());
    expect(result.current.user).toBeNull();
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('user is null on mount when token is expired', () => {
    const expiredToken = makeTestJwt(Math.floor(Date.now() / 1000) - 60);
    setToken(expiredToken);
    const { result } = renderHook(() => useAuth());
    expect(result.current.user).toBeNull();
  });

  it('user is populated from localStorage on mount when a valid non-expired token exists', () => {
    const futureExp = Math.floor(Date.now() / 1000) + 3600;
    const token = makeTestJwt(futureExp);
    setToken(token);

    const { result } = renderHook(() => useAuth());
    expect(result.current.user).not.toBeNull();
    expect(result.current.user?.id).toBe('user-1');
    expect(result.current.user?.email).toBe('test@example.com');
    expect(result.current.user?.role).toBe('Player');
    expect(result.current.user?.callsign).toBe('Alpha1');
    expect(result.current.isAuthenticated).toBe(true);
  });

  it('login() sets user from token', () => {
    const { result } = renderHook(() => useAuth());
    const futureExp = Math.floor(Date.now() / 1000) + 3600;
    const token = makeTestJwt(futureExp);

    act(() => {
      result.current.login(token);
    });

    expect(result.current.user?.email).toBe('test@example.com');
    expect(result.current.isAuthenticated).toBe(true);
  });
});
