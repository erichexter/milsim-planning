import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api, UpdateFrequencyRequest } from '../lib/api';

export function useFrequencies(eventId: string) {
  return useQuery({
    queryKey: ['frequencies', eventId],
    queryFn: () => api.getFrequencies(eventId),
  });
}

export function useUpdateSquadFrequencies(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ squadId, body }: { squadId: string; body: UpdateFrequencyRequest }) =>
      api.updateSquadFrequencies(squadId, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] }),
  });
}

export function useUpdatePlatoonFrequencies(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ platoonId, body }: { platoonId: string; body: UpdateFrequencyRequest }) =>
      api.updatePlatoonFrequencies(platoonId, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] }),
  });
}

export function useUpdateCommandFrequencies(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ factionId, body }: { factionId: string; body: UpdateFrequencyRequest }) =>
      api.updateCommandFrequencies(factionId, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] }),
  });
}
