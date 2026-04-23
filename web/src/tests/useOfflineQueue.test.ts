import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useOfflineQueue, OfflineCheckInRecord } from '../hooks/useOfflineQueue';
import * as apiModule from '../lib/api';

/**
 * AC-08: Vitest tests: ≥3 tests—queue add, queue retrieval, sync success
 */

// Mock the api module
vi.mock('../lib/api', () => ({
  api: {
    post: vi.fn(),
  },
}));

// Mock IndexedDB
class MockIndexedDB {
  private stores: Map<string, Map<string, any>> = new Map();
  private version = 1;

  open(dbName: string, version: number) {
    const request = new MockIDBOpenRequest();
    request.dbName = dbName;
    request.version = version;

    setTimeout(() => {
      if (!this.stores.has(dbName)) {
        this.stores.set(dbName, new Map());
      }
      const db = new MockIDBDatabase(this.stores.get(dbName)!);
      request.result = db;
      request.onsuccess?.(request as any);
    }, 0);

    return request;
  }
}

class MockIDBOpenRequest {
  dbName = '';
  version = 1;
  result: MockIDBDatabase | null = null;
  error: Error | null = null;
  onsuccess: ((event: any) => void) | null = null;
  onerror: ((event: any) => void) | null = null;
  onupgradeneeded: ((event: any) => void) | null = null;
}

class MockIDBDatabase {
  constructor(private store: Map<string, any>) {}

  transaction(storeName: string, mode: string) {
    return new MockIDBTransaction(this.store);
  }

  get objectStoreNames() {
    return new Proxy({}, {
      get: (target, prop) => {
        if (prop === 'contains') {
          return (name: string) => true; // Pretend all stores exist
        }
      }
    });
  }
}

class MockIDBTransaction {
  constructor(private store: Map<string, any>) {}

  objectStore(name: string) {
    return new MockIDBObjectStore(this.store);
  }
}

class MockIDBObjectStore {
  constructor(private store: Map<string, any>) {}

  add(value: any) {
    const request = new MockIDBRequest();
    setTimeout(() => {
      this.store.set(value.id, value);
      request.result = value.id;
      request.onsuccess?.(request as any);
    }, 0);
    return request;
  }

  get(key: string) {
    const request = new MockIDBRequest();
    setTimeout(() => {
      request.result = this.store.get(key);
      request.onsuccess?.(request as any);
    }, 0);
    return request;
  }

  put(value: any) {
    const request = new MockIDBRequest();
    setTimeout(() => {
      this.store.set(value.id, value);
      request.result = value.id;
      request.onsuccess?.(request as any);
    }, 0);
    return request;
  }

  createIndex(indexName: string, keyPath: string) {
    return new MockIDBIndex(indexName, keyPath, this.store);
  }

  index(indexName: string) {
    return new MockIDBIndex(indexName, indexName === 'synced' ? 'synced' : 'eventId', this.store);
  }

  get objectStoreNames() {
    return new Proxy({}, {
      get: (target, prop) => {
        if (prop === 'contains') {
          return (name: string) => true;
        }
      }
    });
  }
}

class MockIDBIndex {
  constructor(
    private indexName: string,
    private keyPath: string,
    private store: Map<string, any>
  ) {}

  getAll(value?: any) {
    const request = new MockIDBRequest();
    setTimeout(() => {
      const results = Array.from(this.store.values());
      if (this.indexName === 'synced') {
        request.result = results.filter((r) => r.synced === value);
      } else if (this.indexName === 'eventId') {
        request.result = results.filter((r) => r.eventId === value);
      } else {
        request.result = results;
      }
      request.onsuccess?.(request as any);
    }, 0);
    return request;
  }
}

class MockIDBRequest {
  result: any = null;
  error: Error | null = null;
  onsuccess: ((event: any) => void) | null = null;
  onerror: ((event: any) => void) | null = null;
}

describe('useOfflineQueue', () => {
  beforeEach(() => {
    // Mock IndexedDB
    (global.indexedDB as any) = new MockIndexedDB();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  /**
   * AC-03: `queueCheckIn()` adds record to IndexedDB immediately;
   * returns success; does not call API
   */
  it('AC-03: queueCheckIn adds record to IndexedDB and returns immediately', async () => {
    const { result } = renderHook(() => useOfflineQueue());

    const qrCode = '550e8400-e29b-41d4-a716-446655440000';
    const eventId = '660e8400-e29b-41d4-a716-446655440001';

    let addedRecord: OfflineCheckInRecord | undefined;
    await waitFor(async () => {
      addedRecord = await result.current.queueCheckIn(qrCode, eventId);
      expect(addedRecord).toBeDefined();
      expect(addedRecord?.qrCode).toBe(qrCode);
      expect(addedRecord?.eventId).toBe(eventId);
      expect(addedRecord?.synced).toBe(false);
      expect(addedRecord?.queuedAtUtc).toBeDefined();
    });

    // Verify API was NOT called
    expect(apiModule.api.post).not.toHaveBeenCalled();
  });

  /**
   * AC-04: `getPendingQueue()` returns array of unsynced records;
   * synced records remain in IndexedDB (for audit)
   */
  it('AC-04: getPendingQueue returns unsynced records only', async () => {
    const { result } = renderHook(() => useOfflineQueue());

    const eventId = '770e8400-e29b-41d4-a716-446655440002';
    const qrCode1 = '550e8400-e29b-41d4-a716-446655440000';
    const qrCode2 = '550e8400-e29b-41d4-a716-446655440001';

    // Queue two records
    let record1: OfflineCheckInRecord | undefined;
    let record2: OfflineCheckInRecord | undefined;

    await waitFor(async () => {
      record1 = await result.current.queueCheckIn(qrCode1, eventId);
      record2 = await result.current.queueCheckIn(qrCode2, eventId);
    });

    // Get pending queue
    let pending: OfflineCheckInRecord[] = [];
    await waitFor(async () => {
      pending = await result.current.getPendingQueue(eventId);
      expect(pending).toHaveLength(2);
      expect(pending[0]?.qrCode).toBe(qrCode1);
      expect(pending[1]?.qrCode).toBe(qrCode2);
    });

    // All should be unsynced
    expect(pending.every((r) => r.synced === false)).toBe(true);
  });

  /**
   * AC-05 + AC-06: `syncQueue()` batches unsynced records and sends to endpoint;
   * backend returns { synced, failed, errors }
   */
  it('AC-05: syncQueue sends batch to backend endpoint', async () => {
    const { result } = renderHook(() => useOfflineQueue());

    const eventId = '880e8400-e29b-41d4-a716-446655440003';
    const qrCode = '550e8400-e29b-41d4-a716-446655440002';

    // Queue a record
    await waitFor(async () => {
      await result.current.queueCheckIn(qrCode, eventId);
    });

    // Mock the sync endpoint response
    const mockResponse = {
      synced: 1,
      failed: 0,
      errors: [],
    };
    vi.mocked(apiModule.api.post).mockResolvedValue(mockResponse);

    // Sync queue
    let syncResult;
    await waitFor(async () => {
      syncResult = await result.current.syncQueue(eventId);
      expect(syncResult?.synced).toBe(1);
      expect(syncResult?.failed).toBe(0);
      expect(syncResult?.errors).toHaveLength(0);
    });

    // Verify API was called with correct endpoint and payload
    expect(apiModule.api.post).toHaveBeenCalledWith(
      `/events/${eventId}/check-in/sync-offline`,
      expect.objectContaining({
        records: expect.arrayContaining([
          expect.objectContaining({
            qrCode,
            queuedAtUtc: expect.any(String),
          }),
        ]),
      })
    );
  });

  /**
   * AC-06: Backend returns synced/failed counts and error list
   */
  it('AC-06: syncQueue handles backend response with errors', async () => {
    const { result } = renderHook(() => useOfflineQueue());

    const eventId = '990e8400-e29b-41d4-a716-446655440004';
    const validQr = '550e8400-e29b-41d4-a716-446655440003';
    const invalidQr = '550e8400-e29b-41d4-a716-446655440004';

    // Queue two records
    await waitFor(async () => {
      await result.current.queueCheckIn(validQr, eventId);
      await result.current.queueCheckIn(invalidQr, eventId);
    });

    // Mock the sync endpoint response with one failure
    const mockResponse = {
      synced: 1,
      failed: 1,
      errors: [
        {
          qrCode: invalidQr,
          error: 'Participant not found in event',
        },
      ],
    };
    vi.mocked(apiModule.api.post).mockResolvedValue(mockResponse);

    // Sync queue
    let syncResult;
    await waitFor(async () => {
      syncResult = await result.current.syncQueue(eventId);
    });

    expect(syncResult?.synced).toBe(1);
    expect(syncResult?.failed).toBe(1);
    expect(syncResult?.errors).toHaveLength(1);
    expect(syncResult?.errors[0].qrCode).toBe(invalidQr);
    expect(syncResult?.errors[0].error).toContain('Participant not found');
  });

  /**
   * Additional test: syncQueue with empty queue returns no sync
   */
  it('syncQueue with empty queue returns zero synced', async () => {
    const { result } = renderHook(() => useOfflineQueue());

    const eventId = 'a10e8400-e29b-41d4-a716-446655440005';

    let syncResult;
    await waitFor(async () => {
      syncResult = await result.current.syncQueue(eventId);
    });

    expect(syncResult?.synced).toBe(0);
    expect(syncResult?.failed).toBe(0);
    expect(apiModule.api.post).not.toHaveBeenCalled();
  });
});
