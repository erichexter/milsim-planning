import { describe, it, expect, beforeEach } from 'vitest';
import { getToken, setToken, clearToken, parseJwt, isTokenExpired } from '../lib/auth';

// Helper: create a JWT with a given exp claim (without signing — for test only)
function makeTestJwt(exp: number): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payload = btoa(JSON.stringify({ sub: 'user-1', email: 'test@example.com', exp }));
  return `${header}.${payload}.fakesig`;
}

describe('auth helpers', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('getToken() returns null when localStorage is empty', () => {
    expect(getToken()).toBeNull();
  });

  it('setToken() + getToken() round-trips the token', () => {
    setToken('abc');
    expect(getToken()).toBe('abc');
  });

  it('clearToken() → getToken() returns null', () => {
    setToken('abc');
    clearToken();
    expect(getToken()).toBeNull();
  });

  it('isTokenExpired returns true for a token with past exp claim', () => {
    const pastExp = Math.floor(Date.now() / 1000) - 60; // 60s ago
    const token = makeTestJwt(pastExp);
    expect(isTokenExpired(token)).toBe(true);
  });

  it('isTokenExpired returns false for a token with future exp claim', () => {
    const futureExp = Math.floor(Date.now() / 1000) + 3600; // 1h from now
    const token = makeTestJwt(futureExp);
    expect(isTokenExpired(token)).toBe(false);
  });

  it('parseJwt returns null for an invalid token', () => {
    expect(parseJwt('not.a.jwt')).toBeNull();
  });
});
