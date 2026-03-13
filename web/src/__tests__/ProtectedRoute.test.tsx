import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router';
import { ProtectedRoute } from '../components/ProtectedRoute';
import { setToken, clearToken } from '../lib/auth';

// Helper: create a valid non-expired JWT
function makeValidJwt(): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const futureExp = Math.floor(Date.now() / 1000) + 3600;
  const payload = btoa(
    JSON.stringify({
      sub: 'user-1',
      email: 'test@example.com',
      role: 'Player',
      callsign: 'Alpha1',
      exp: futureExp,
    })
  );
  return `${header}.${payload}.fakesig`;
}

describe('ProtectedRoute', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('redirects to /auth/login when not authenticated', () => {
    clearToken();
    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <Routes>
          <Route path="/auth/login" element={<div>Login Page</div>} />
          <Route element={<ProtectedRoute />}>
            <Route path="/dashboard" element={<div>Dashboard</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );
    expect(screen.getByText('Login Page')).toBeDefined();
    expect(screen.queryByText('Dashboard')).toBeNull();
  });

  it('renders Outlet when authenticated', () => {
    setToken(makeValidJwt());
    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <Routes>
          <Route path="/auth/login" element={<div>Login Page</div>} />
          <Route element={<ProtectedRoute />}>
            <Route path="/dashboard" element={<div>Dashboard</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );
    expect(screen.getByText('Dashboard')).toBeDefined();
    expect(screen.queryByText('Login Page')).toBeNull();
  });
});
