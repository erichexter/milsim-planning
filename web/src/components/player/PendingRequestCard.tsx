import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../../lib/api';
import { Badge } from '../ui/badge';
import { Button } from '../ui/button';
import { Card, CardContent } from '../ui/card';

interface PendingRequest {
  id: string;
  note: string;
  status: string;
  commanderNote?: string | null;
  createdAt: string;
}

interface PendingRequestCardProps {
  eventId: string;
  request: PendingRequest;
}

export function PendingRequestCard({ eventId, request }: PendingRequestCardProps) {
  const queryClient = useQueryClient();

  const cancelMutation = useMutation({
    mutationFn: () =>
      api.delete(`/events/${eventId}/roster-change-requests/${request.id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['events', eventId, 'roster-change-requests', 'mine'] });
    },
  });

  return (
    <Card>
      <CardContent className="pt-4 space-y-3">
        <div className="flex items-center gap-2">
          <Badge variant="secondary">{request.status}</Badge>
          <span className="text-xs text-muted-foreground">
            {new Date(request.createdAt).toLocaleDateString()}
          </span>
        </div>
        <p className="text-sm">{request.note}</p>
        {request.commanderNote && (
          <div className="rounded-md bg-muted p-3">
            <p className="text-xs font-medium text-muted-foreground mb-1">Commander's note:</p>
            <p className="text-sm">{request.commanderNote}</p>
          </div>
        )}
        {request.status === 'Pending' && (
          <Button
            variant="outline"
            className="w-full min-h-[44px]"
            onClick={() => cancelMutation.mutate()}
            disabled={cancelMutation.isPending}
          >
            {cancelMutation.isPending ? 'Cancelling...' : 'Cancel Request'}
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
