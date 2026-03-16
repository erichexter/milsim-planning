import { useParams, Link } from 'react-router';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../../lib/api';
import { useAuth } from '../../hooks/useAuth';
import { Badge } from '../../components/ui/badge';
import { Button } from '../../components/ui/button';

export function EventDetail() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const isCommander = user?.role === 'faction_commander';

  const { data: events } = useQuery({
    queryKey: ['events'],
    queryFn: () => api.getEvents(),
  });
  const event = events?.find((e) => e.id === id);

  const publishMutation = useMutation({
    mutationFn: () => api.publishEvent(id!),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['events'] }),
  });

  if (!event) return <div className="p-6">Event not found.</div>;

  return (
    <div className="p-6 max-w-4xl mx-auto space-y-4">
      <div className="flex items-center gap-3">
        <h1 className="text-2xl font-bold">{event.name}</h1>
        <Badge variant={event.status === 'Published' ? 'default' : 'secondary'}>
          {event.status}
        </Badge>
      </div>

      {event.location && <p className="text-muted-foreground">{event.location}</p>}
      {event.startDate && <p className="text-sm">Start: {event.startDate}</p>}
      {event.endDate && <p className="text-sm">End: {event.endDate}</p>}

      <div className="flex gap-3 pt-4">
        {event.status === 'Draft' && (
          <Button onClick={() => publishMutation.mutate()} disabled={publishMutation.isPending}>
            {publishMutation.isPending ? 'Publishing...' : 'Publish Event'}
          </Button>
        )}
        <Button variant="outline" asChild>
          <Link to={`/events/${id}/roster/import`}>Import Roster</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link to={`/events/${id}/hierarchy`}>Manage Hierarchy</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link to={`/events/${id}/roster`}>View Roster</Link>
        </Button>
        {isCommander && (
          <Button variant="outline" asChild>
            <Link to={`/events/${id}/change-requests`}>Change Requests</Link>
          </Button>
        )}
      </div>
    </div>
  );
}
