import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api, type UpdateFrequencyRequest } from '../lib/api';

export function useFrequencies(eventId: string) {
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: ['frequencies', eventId],
    queryFn: () => api.getFrequencies(eventId),
    enabled: !!eventId,
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] });

  const updateSquad = useMutation({
    mutationFn: ({ squadId, req }: { squadId: string; req: UpdateFrequencyRequest }) =>
      api.updateSquadFrequency(squadId, req),
    onSuccess: invalidate,
  });

  const updatePlatoon = useMutation({
    mutationFn: ({ platoonId, req }: { platoonId: string; req: UpdateFrequencyRequest }) =>
      api.updatePlatoonFrequency(platoonId, req),
    onSuccess: invalidate,
  });

  const updateCommand = useMutation({
    mutationFn: (req: UpdateFrequencyRequest) =>
      api.updateCommandFrequency(eventId, req),
    onSuccess: invalidate,
  });

  return {
    data: query.data,
    isLoading: query.isLoading,
    error: query.error,
    refetch: query.refetch,
    updateSquad,
    updatePlatoon,
    updateCommand,
  };
}
