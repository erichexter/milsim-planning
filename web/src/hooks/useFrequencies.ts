import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import type { FrequenciesDto } from '../lib/api';

export function useFrequencies(eventId: string) {
  return useQuery<FrequenciesDto>({
    queryKey: ['frequencies', eventId],
    queryFn: () => api.getFrequencies(eventId),
    enabled: Boolean(eventId),
  });
}
