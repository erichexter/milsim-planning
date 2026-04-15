import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api, type RsvpStatus } from '../lib/api';

export function useRsvp(eventId: string) {
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: ['rsvp', eventId],
    queryFn: () => api.getRsvp(eventId),
    enabled: !!eventId,
  });

  const mutation = useMutation({
    mutationFn: (status: RsvpStatus) => api.setRsvp(eventId, { status }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rsvp', eventId] }),
  });

  return {
    rsvp: query.data,
    isLoading: query.isLoading,
    error: query.error,
    setRsvp: mutation.mutate,
    isUpdating: mutation.isPending,
    updateError: mutation.error,
  };
}
