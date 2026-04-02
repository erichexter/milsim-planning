import { useQuery } from '@tanstack/react-query';
import { api, FrequencyViewDto } from '../lib/api';

export function useFrequencies(eventId: string) {
  return useQuery<FrequencyViewDto>({
    queryKey: ['frequencies', eventId],
    queryFn: () => api.getFrequencies(eventId),
    enabled: !!eventId,
  });
}
