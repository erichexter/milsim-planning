import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { setToken, clearToken } from '../lib/auth';
import { api } from '../lib/api';

describe('api client', () => {
  const mockFetch = vi.fn();

  beforeEach(() => {
    localStorage.clear();
    mockFetch.mockReset();
    vi.stubGlobal('fetch', mockFetch);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('adds Authorization: Bearer header when token is present', async () => {
    setToken('test-token-123');
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({ data: 'ok' }),
    });

    await api.get('/test');

    const [, options] = mockFetch.mock.calls[0];
    expect((options as RequestInit).headers).toMatchObject({
      Authorization: 'Bearer test-token-123',
    });
  });

  it('does NOT add Authorization header when token is null', async () => {
    clearToken();
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({ data: 'ok' }),
    });

    await api.get('/test');

    const [, options] = mockFetch.mock.calls[0];
    const headers = (options as RequestInit).headers as Record<string, string>;
    expect(headers['Authorization']).toBeUndefined();
  });

  it('throws on non-ok response', async () => {
    clearToken();
    // Use 403 Forbidden — 401 is handled specially (redirects, returns undefined, does not throw)
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 403,
      statusText: 'Forbidden',
      json: async () => ({ error: 'Forbidden' }),
    });

    await expect(api.get('/test')).rejects.toThrow('Forbidden');
  });
});
