import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../../lib/api';
import { Button } from '../ui/button';

interface ChangeRequestFormProps {
  eventId: string;
  isUnassigned?: boolean;
}

export function ChangeRequestForm({ eventId, isUnassigned = false }: ChangeRequestFormProps) {
  const [note, setNote] = useState('');
  const queryClient = useQueryClient();

  const submitMutation = useMutation({
    mutationFn: () =>
      api.post(`/events/${eventId}/roster-change-requests`, { note }),
    onSuccess: () => {
      setNote('');
      void queryClient.invalidateQueries({ queryKey: ['events', eventId, 'roster-change-requests', 'mine'] });
    },
  });

  return (
    <div className="space-y-3">
      <textarea
        className="w-full min-h-[80px] rounded-md border border-input bg-background px-3 py-2 text-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring resize-none"
        placeholder="Describe your request..."
        value={note}
        onChange={(e) => setNote(e.target.value)}
      />
      <Button
        className="w-full min-h-[44px]"
        onClick={() => submitMutation.mutate()}
        disabled={!note.trim() || submitMutation.isPending}
      >
        {submitMutation.isPending
          ? 'Submitting...'
          : isUnassigned
          ? 'Request Assignment'
          : 'Submit Request'}
      </Button>
      {submitMutation.isError && (
        <p className="text-sm text-destructive">
          Failed to submit request. Please try again.
        </p>
      )}
    </div>
  );
}
