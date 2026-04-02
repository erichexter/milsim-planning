import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  updateSquadFrequencies,
  updatePlatoonFrequencies,
  updateFactionFrequencies,
} from '../lib/api';
import type { UpdateFrequencyRequest } from '../lib/api';

export function useUpdateSquadFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ squadId, req }: { squadId: string; req: UpdateFrequencyRequest }) =>
      updateSquadFrequencies(squadId, req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] });
    },
  });
}

export function useUpdatePlatoonFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ platoonId, req }: { platoonId: string; req: UpdateFrequencyRequest }) =>
      updatePlatoonFrequencies(platoonId, req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] });
    },
  });
}

export function useUpdateFactionFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ factionId, req }: { factionId: string; req: UpdateFrequencyRequest }) =>
      updateFactionFrequencies(factionId, req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] });
    },
  });
}
