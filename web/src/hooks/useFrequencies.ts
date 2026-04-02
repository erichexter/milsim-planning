import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api, frequencyKeys } from '../lib/api';
import type { UpdateFrequencyRequest } from '../lib/api';

export function useFrequencies(eventId: string) {
  return useQuery({
    queryKey: frequencyKeys.byEvent(eventId),
    queryFn: () => api.getFrequencies(eventId),
    enabled: !!eventId,
  });
}

export function useUpdateSquadFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ squadId, req }: { squadId: string; req: UpdateFrequencyRequest }) =>
      api.updateSquadFrequency(squadId, req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: frequencyKeys.byEvent(eventId) });
    },
  });
}

export function useUpdatePlatoonFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ platoonId, req }: { platoonId: string; req: UpdateFrequencyRequest }) =>
      api.updatePlatoonFrequency(platoonId, req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: frequencyKeys.byEvent(eventId) });
    },
  });
}

export function useUpdateCommandFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ factionId, req }: { factionId: string; req: UpdateFrequencyRequest }) =>
      api.updateFactionFrequency(factionId, req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: frequencyKeys.byEvent(eventId) });
    },
  });
}
