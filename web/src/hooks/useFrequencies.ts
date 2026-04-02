import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../lib/api';
import type { UpdateFrequencyRequest, FrequencyResponseDto } from '../lib/api';

export function useFrequencies(eventId: string) {
  return useQuery<FrequencyResponseDto>({
    queryKey: ['frequencies', eventId],
    queryFn: () => api.getFrequencies(eventId),
    enabled: !!eventId,
  });
}

export function useUpdateSquadFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ squadId, request }: { squadId: string; request: UpdateFrequencyRequest }) =>
      api.updateSquadFrequency(squadId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] });
    },
  });
}

export function useUpdatePlatoonFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ platoonId, request }: { platoonId: string; request: UpdateFrequencyRequest }) =>
      api.updatePlatoonFrequency(platoonId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] });
    },
  });
}

export function useUpdateCommandFrequency(eventId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateFrequencyRequest) =>
      api.updateCommandFrequency(eventId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] });
    },
  });
}
