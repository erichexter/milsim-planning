import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useOfflineQueue } from '../hooks/useOfflineQueue';
import * as apiModule from '../lib/api';

// Mock the api module
vi.mock('../lib/api', () => ({
  api: {
    post: vi.fn(),
  },
}));

describe('useOfflineQueue', () => {
  beforeEach(() => {
    // Mock IndexedDB
    const mockStore = new Map();
    (global.indexedDB as any) = {
      open: vi.fn(() => {
        const req = {
          result: null,
          onsuccess: null as any,
          onerror: null as any,
          onupgradeneeded: null as any,
        };
        setTimeout(() => {
          req.result = {
            transaction: (name: string, mode: string) => ({
              objectStore: (storeName: string) => ({
                add: (value: any) => ({
                  onsuccess: null,
                  onerror: null,
                  addEventListener: function(type: string, handler: any) {
                    if (type === 'success') {
                      setTimeout(() => { mockStore.set(value.id, value); handler(); }, 0);
                    }
                  }
                }),
                get: (id: string) => ({
                  result: mockStore.get(id),
                  onsuccess: null,
                  addEventListener: function(type: string, handler: any) {
                    if (type === 'success') setTimeout(() => handler(), 0);
                  }
                }),
                put: (value: any) => ({
                  addEventListener: function(type: string, handler: any) {
                    if (type === 'success') {
                      setTimeout(() => { mockStore.set(value.id, value); handler(); }, 0);
                    }
                  }
                }),
                index: () => ({
                  getAll: (value?: any) => ({
                    result: value === false ? Array.from(mockStore.values()).filter(r => !r.synced) : [],
                    addEventListener: function(type: string, handler: any) {
                      if (type === 'success') setTimeout(() => handler(), 0);
                    }
                  })
                }),
                createIndex: () => {}
              }),
            }),
            objectStoreNames: { contains: () => true }
          };
          req.onsuccess?.();
        }, 0);
        return req;
      })
    };
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('queueCheckIn adds record and returns immediately', async () => {
    const { result } = renderHook(() => useOfflineQueue());
    const qrCode = '550e8400-e29b-41d4-a716-446655440000';
    const eventId = '660e8400-e29b-41d4-a716-446655440001';

    const record = await result.current.queueCheckIn(qrCode, eventId);
    expect(record.qrCode).toBe(qrCode);
    expect(record.synced).toBe(false);
    expect(apiModule.api.post).not.toHaveBeenCalled();
  });

  it('getPendingQueue returns unsynced records', async () => {
    const { result } = renderHook(() => useOfflineQueue());
    const eventId = '770e8400-e29b-41d4-a716-446655440002';

    await result.current.queueCheckIn('550e8400-e29b-41d4-a716-446655440000', eventId);
    const pending = await result.current.getPendingQueue(eventId);
    expect(pending).toHaveLength(1);
    expect(pending[0].synced).toBe(false);
  });

  it('syncQueue sends batch to backend', async () => {
    const { result } = renderHook(() => useOfflineQueue());
    const eventId = '880e8400-e29b-41d4-a716-446655440003';

    await result.current.queueCheckIn('550e8400-e29b-41d4-a716-446655440002', eventId);
    
    vi.mocked(apiModule.api.post).mockResolvedValue({
      synced: 1,
      failed: 0,
      errors: [],
    });

    const result_sync = await result.current.syncQueue(eventId);
    expect(result_sync.synced).toBe(1);
    expect(apiModule.api.post).toHaveBeenCalled();
  });
});
