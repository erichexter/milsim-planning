import { useQuery, useQueries } from '@tanstack/react-query';
import { api, FrequencyLevelDto, FrequencyViewDto } from '../lib/api';

export function useFrequencies(eventId: string) {
  return useQuery<FrequencyViewDto>({
    queryKey: ['frequencies', eventId],
    queryFn: () => api.getFrequencies(eventId),
    enabled: !!eventId,
  });
}

/**
 * For platoon leaders: fetch frequencies for the squads in their platoon(s).
 * The main getFrequencies endpoint returns squads: null for platoon leaders
 * (read access is limited), but platoon leaders have write access to their squads.
 * This hook resolves the roster → squads → frequencies chain so the UI can
 * render editable squad rows.
 */
export function usePlatoonLeaderSquadFrequencies(
  eventId: string,
  role: string,
  platoonIds: string[]
) {
  const isPlatoonLeader = role === 'platoon_leader';

  const { data: rosterData } = useQuery({
    queryKey: ['roster', eventId],
    queryFn: () => api.getRoster(eventId),
    enabled: isPlatoonLeader && !!eventId && platoonIds.length > 0,
  });

  const squadInfos =
    isPlatoonLeader && rosterData
      ? rosterData.platoons
          .filter(p => platoonIds.includes(p.id))
          .flatMap(p => p.squads.map(s => ({ id: s.id, name: s.name })))
      : [];

  const squadFreqResults = useQueries({
    queries: squadInfos.map(sq => ({
      queryKey: ['squad-frequencies', sq.id] as const,
      queryFn: () => api.getSquadFrequencies(sq.id),
    })),
  });

  const editableSquads: FrequencyLevelDto[] = squadFreqResults.map((q, i) =>
    q.data ?? {
      id: squadInfos[i].id,
      name: squadInfos[i].name,
      primary: null,
      backup: null,
    }
  );

  return editableSquads;
}
