import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import {
  useSquadFrequency,
  usePlatoonFrequency,
  useFactionFrequency,
} from '../../hooks/useFrequencies';

const SQUAD_ID = 'aaaaaaaa-0000-0000-0000-000000000001';
const PLATOON_ID = 'bbbbbbbb-0000-0000-0000-000000000002';
const FACTION_ID = 'cccccccc-0000-0000-0000-000000000003';

function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return React.createElement(QueryClientProvider, { client: qc }, children);
  };
}

describe('useSquadFrequency', () => {
  beforeEach(() => {
    server.use(
      http.get(`/api/squads/${SQUAD_ID}/frequencies`, () =>
        HttpResponse.json({ squadId: SQUAD_ID, primary: '45.500 MHz', backup: null })
      )
    );
  });

  it('fetches squad frequency data', async () => {
    const { result } = renderHook(() => useSquadFrequency(SQUAD_ID), { wrapper: makeWrapper() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.primary).toBe('45.500 MHz');
    expect(result.current.data?.backup).toBeNull();
    expect(result.current.data?.squadId).toBe(SQUAD_ID);
  });

  it('does not fetch when squadId is null', () => {
    const { result } = renderHook(() => useSquadFrequency(null), { wrapper: makeWrapper() });
    expect(result.current.fetchStatus).toBe('idle');
  });
});

describe('usePlatoonFrequency', () => {
  beforeEach(() => {
    server.use(
      http.get(`/api/platoons/${PLATOON_ID}/frequencies`, () =>
        HttpResponse.json({ platoonId: PLATOON_ID, primary: '46.750 MHz', backup: '47.000 MHz' })
      )
    );
  });

  it('fetches platoon frequency data', async () => {
    const { result } = renderHook(() => usePlatoonFrequency(PLATOON_ID), { wrapper: makeWrapper() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.primary).toBe('46.750 MHz');
    expect(result.current.data?.backup).toBe('47.000 MHz');
    expect(result.current.data?.platoonId).toBe(PLATOON_ID);
  });

  it('does not fetch when platoonId is null', () => {
    const { result } = renderHook(() => usePlatoonFrequency(null), { wrapper: makeWrapper() });
    expect(result.current.fetchStatus).toBe('idle');
  });
});

describe('useFactionFrequency', () => {
  beforeEach(() => {
    server.use(
      http.get(`/api/factions/${FACTION_ID}/frequencies`, () =>
        HttpResponse.json({ factionId: FACTION_ID, primary: '47.000 MHz', backup: '48.250 MHz' })
      )
    );
  });

  it('fetches faction frequency data', async () => {
    const { result } = renderHook(() => useFactionFrequency(FACTION_ID), { wrapper: makeWrapper() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.primary).toBe('47.000 MHz');
    expect(result.current.data?.backup).toBe('48.250 MHz');
    expect(result.current.data?.factionId).toBe(FACTION_ID);
  });

  it('does not fetch when factionId is null', () => {
    const { result } = renderHook(() => useFactionFrequency(null), { wrapper: makeWrapper() });
    expect(result.current.fetchStatus).toBe('idle');
  });
});
