import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api, UpdateFrequencyRequest } from '../lib/api';

export function useFrequencies(eventId: string) {
  return useQuery({
    queryKey: ['frequencies', eventId],
    queryFn: () => api.getFrequencies(eventId),
    enabled: !!eventId,
  });
}

export function useUpdateSquadFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ squadId, body }: { squadId: string; body: UpdateFrequencyRequest }) =>
      api.updateSquadFrequency(squadId, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] });
    },
  });
}

export function useUpdatePlatoonFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ platoonId, body }: { platoonId: string; body: UpdateFrequencyRequest }) =>
      api.updatePlatoonFrequency(platoonId, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] });
    },
  });
}

export function useUpdateCommandFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateFrequencyRequest) =>
      api.updateCommandFrequency(eventId, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] });
    },
  });
}
