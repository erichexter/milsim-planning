import { useQuery } from '@tanstack/react-query';
import { getEventFrequencies } from '../lib/api';

export function useFrequencies(eventId: string) {
  return useQuery({
    queryKey: ['frequencies', eventId],
    queryFn: () => getEventFrequencies(eventId),
  });
}
