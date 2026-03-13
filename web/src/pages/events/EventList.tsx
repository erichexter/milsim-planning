import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';
import { api } from '../../lib/api';
import { CreateEventDialog } from './CreateEventDialog';
import { DuplicateEventDialog } from '../../components/events/DuplicateEventDialog';
import { Badge } from '../../components/ui/badge';

export function EventList() {
  const { data: events = [], isLoading } = useQuery({
    queryKey: ['events'],
    queryFn: () => api.getEvents(),
  });

  if (isLoading) return <div>Loading events...</div>;

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">My Events</h1>
        <CreateEventDialog />
      </div>

      {events.length === 0 ? (
        <p className="text-muted-foreground">No events yet. Create your first event.</p>
      ) : (
        <div className="space-y-3">
          {events.map((event) => (
            <div key={event.id} className="border rounded-lg p-4 flex items-center justify-between">
              <div>
                <div className="flex items-center gap-2">
                  <Link
                    to={`/events/${event.id}`}
                    className="font-semibold hover:underline"
                  >
                    {event.name}
                  </Link>
                  <Badge variant={event.status === 'Published' ? 'default' : 'secondary'}>
                    {event.status}
                  </Badge>
                </div>
                {event.location && (
                  <p className="text-sm text-muted-foreground">{event.location}</p>
                )}
                {event.startDate && (
                  <p className="text-sm text-muted-foreground">{event.startDate}</p>
                )}
              </div>
              <DuplicateEventDialog eventId={event.id} infoSections={[]} />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
