import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { MagicLinkConfirmPage } from '../pages/auth/MagicLinkConfirmPage';

// Mock the api module to track calls
const mockApiPost = vi.fn();
vi.mock('../lib/api', () => ({
  api: {
    post: (...args: unknown[]) => mockApiPost(...args),
    get: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

describe('MagicLinkConfirmPage', () => {
  beforeEach(() => {
    localStorage.clear();
    mockApiPost.mockReset();
  });

  it('renders a button on mount — does NOT call api.post on mount (email scanner protection)', () => {
    render(
      <MemoryRouter initialEntries={['/auth/magic-link/confirm?token=abc&userId=123']}>
        <Routes>
          <Route path="/auth/magic-link/confirm" element={<MagicLinkConfirmPage />} />
        </Routes>
      </MemoryRouter>
    );

    // Button should be present
    expect(screen.getByRole('button')).toBeDefined();
    // api.post must NOT have been called on mount
    expect(mockApiPost).not.toHaveBeenCalled();
  });

  it('calls api.post only when button is clicked', async () => {
    const user = userEvent.setup();
    mockApiPost.mockResolvedValueOnce({ token: 'new-token', expiresIn: 604800 });

    render(
      <MemoryRouter initialEntries={['/auth/magic-link/confirm?token=abc&userId=123']}>
        <Routes>
          <Route path="/auth/magic-link/confirm" element={<MagicLinkConfirmPage />} />
          <Route path="/dashboard" element={<div>Dashboard</div>} />
        </Routes>
      </MemoryRouter>
    );

    // Before click: no API call
    expect(mockApiPost).not.toHaveBeenCalled();

    // Click the button
    await user.click(screen.getByRole('button'));

    // After click: API called once with correct args
    expect(mockApiPost).toHaveBeenCalledTimes(1);
    expect(mockApiPost).toHaveBeenCalledWith('/auth/magic-link/confirm', {
      token: 'abc',
      userId: '123',
    });
  });
});
