import { describe, it, expect } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { api, frequencyKeys } from '../lib/api';
import type { FrequencyVisibilityDto } from '../lib/api';

const EVENT_ID = 'a1b2c3d4-0000-0000-0000-000000000001';
const SQUAD_ID = 'b1b2c3d4-0000-0000-0000-000000000001';
const PLATOON_ID = 'c1b2c3d4-0000-0000-0000-000000000001';
const FACTION_ID = 'd1b2c3d4-0000-0000-0000-000000000001';

describe('Frequency API', () => {
  describe('getFrequencies', () => {
    it('fetches frequencies for an event', async () => {
      const mockResponse: FrequencyVisibilityDto = {
        squad: { squadId: SQUAD_ID, squadName: 'Alpha Squad', primary: '43.325', backup: null },
        platoon: { platoonId: PLATOON_ID, platoonName: '1st Platoon', primary: '41.000', backup: '42.000' },
        command: { factionId: FACTION_ID, primary: '40.000', backup: null },
      };

      server.use(
        http.get(`/api/events/${EVENT_ID}/frequencies`, () => HttpResponse.json(mockResponse))
      );

      const result = await api.getFrequencies(EVENT_ID);
      expect(result.squad?.squadName).toBe('Alpha Squad');
      expect(result.platoon?.primary).toBe('41.000');
      expect(result.command?.factionId).toBe(FACTION_ID);
    });

    it('returns null sections for player role', async () => {
      server.use(
        http.get(`/api/events/${EVENT_ID}/frequencies`, () =>
          HttpResponse.json({
            squad: { squadId: SQUAD_ID, squadName: 'Alpha Squad', primary: null, backup: null },
            platoon: null,
            command: null,
          })
        )
      );

      const result = await api.getFrequencies(EVENT_ID);
      expect(result.squad).not.toBeNull();
      expect(result.platoon).toBeNull();
      expect(result.command).toBeNull();
    });

    it('throws error on 403 Forbidden', async () => {
      server.use(
        http.get(`/api/events/${EVENT_ID}/frequencies`, () =>
          HttpResponse.json({ error: 'Forbidden' }, { status: 403 })
        )
      );

      await expect(api.getFrequencies(EVENT_ID)).rejects.toThrow();
    });
  });

  describe('updateSquadFrequency', () => {
    it('sends PUT to squad frequencies endpoint', async () => {
      server.use(
        http.put(`/api/squads/${SQUAD_ID}/frequencies`, () =>
          new HttpResponse(null, { status: 204 })
        )
      );

      await expect(api.updateSquadFrequency(SQUAD_ID, { primary: '43.325', backup: null })).resolves.toBeUndefined();
    });

    it('throws error on 403', async () => {
      server.use(
        http.put(`/api/squads/${SQUAD_ID}/frequencies`, () =>
          HttpResponse.json({ error: 'Forbidden' }, { status: 403 })
        )
      );

      await expect(api.updateSquadFrequency(SQUAD_ID, { primary: '43.325', backup: null })).rejects.toThrow();
    });

    it('throws error on 404', async () => {
      server.use(
        http.put(`/api/squads/${SQUAD_ID}/frequencies`, () =>
          HttpResponse.json({ error: 'Not Found' }, { status: 404 })
        )
      );

      await expect(api.updateSquadFrequency(SQUAD_ID, { primary: '43.325', backup: null })).rejects.toThrow();
    });
  });

  describe('updatePlatoonFrequency', () => {
    it('sends PUT to platoon frequencies endpoint', async () => {
      server.use(
        http.put(`/api/platoons/${PLATOON_ID}/frequencies`, () =>
          new HttpResponse(null, { status: 204 })
        )
      );

      await expect(api.updatePlatoonFrequency(PLATOON_ID, { primary: '41.000', backup: '42.000' })).resolves.toBeUndefined();
    });
  });

  describe('updateFactionFrequency', () => {
    it('sends PUT to faction frequencies endpoint', async () => {
      server.use(
        http.put(`/api/factions/${FACTION_ID}/frequencies`, () =>
          new HttpResponse(null, { status: 204 })
        )
      );

      await expect(api.updateFactionFrequency(FACTION_ID, { primary: '40.000', backup: null })).resolves.toBeUndefined();
    });
  });

  describe('frequencyKeys (React Query)', () => {
    it('provides correct query key for all frequencies', () => {
      expect(frequencyKeys.all).toEqual(['frequencies']);
    });

    it('provides correct query key for frequencies by event', () => {
      expect(frequencyKeys.byEvent('event-1')).toEqual(['frequencies', 'event-1']);
    });
  });
});
