import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api, UpdateFrequencyRequest } from '../lib/api';

export function useFrequencies(eventId: string) {
  return useQuery({
    queryKey: ['frequencies', eventId],
    queryFn: () => api.getFrequencies(eventId),
  });
}

export function useUpdateCommandFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (req: UpdateFrequencyRequest) => api.updateCommandFrequency(eventId, req),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] }),
  });
}

export function useUpdatePlatoonFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ platoonId, ...req }: UpdateFrequencyRequest & { platoonId: string }) =>
      api.updatePlatoonFrequency(eventId, platoonId, req),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] }),
  });
}

export function useUpdateSquadFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ squadId, ...req }: UpdateFrequencyRequest & { squadId: string }) =>
      api.updateSquadFrequency(eventId, squadId, req),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] }),
  });
}
