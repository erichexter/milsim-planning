import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api, type UpdateFrequencyRequest } from '../lib/api';

export function useSquadFrequency(squadId: string | null) {
  return useQuery({
    queryKey: ['squad-frequency', squadId],
    queryFn: () => api.getSquadFrequency(squadId!),
    enabled: !!squadId,
  });
}

export function usePlatoonFrequency(platoonId: string | null) {
  return useQuery({
    queryKey: ['platoon-frequency', platoonId],
    queryFn: () => api.getPlatoonFrequency(platoonId!),
    enabled: !!platoonId,
  });
}

export function useFactionFrequency(factionId: string | null) {
  return useQuery({
    queryKey: ['faction-frequency', factionId],
    queryFn: () => api.getFactionFrequency(factionId!),
    enabled: !!factionId,
  });
}

export function useUpdateSquadFrequency(squadId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (req: UpdateFrequencyRequest) => api.updateSquadFrequency(squadId, req),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['squad-frequency', squadId] });
    },
  });
}

export function useUpdatePlatoonFrequency(platoonId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (req: UpdateFrequencyRequest) => api.updatePlatoonFrequency(platoonId, req),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['platoon-frequency', platoonId] });
    },
  });
}

export function useUpdateFactionFrequency(factionId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (req: UpdateFrequencyRequest) => api.updateFactionFrequency(factionId, req),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['faction-frequency', factionId] });
    },
  });
}
