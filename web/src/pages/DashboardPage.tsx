import { useNavigate } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '../hooks/useAuth';
import { api } from '../lib/api';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';

export function DashboardPage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const { data: events = [], isLoading } = useQuery({
    queryKey: ['events'],
    queryFn: () => api.getEvents(),
  });

  const isCommander = user?.role === 'faction_commander';

  function handleEventClick(eventId: string) {
    if (isCommander) {
      void navigate(`/events/${eventId}`);
    } else {
      void navigate(`/events/${eventId}/player`);
    }
  }

  return (
    <div className="p-6 max-w-4xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Dashboard</h1>
          <p className="text-muted-foreground">
            Welcome, {user?.callsign || user?.email}
          </p>
          {user?.role && (
            <Badge variant="secondary" className="mt-1">
              {user.role}
            </Badge>
          )}
        </div>
        <Button variant="outline" onClick={() => void logout()}>
          Logout
        </Button>
      </div>

      <div>
        <h2 className="text-lg font-semibold mb-3">Your Events</h2>
        {isLoading && <p className="text-muted-foreground">Loading events...</p>}
        {!isLoading && events.length === 0 && (
          <p className="text-muted-foreground">You are not enrolled in any events.</p>
        )}
        <div className="grid gap-3">
          {events.map((event) => (
            <Card
              key={event.id}
              className="cursor-pointer hover:shadow-md transition-shadow"
              onClick={() => handleEventClick(event.id)}
            >
              <CardHeader className="pb-2">
                <div className="flex items-center gap-2">
                  <CardTitle className="text-base">{event.name}</CardTitle>
                  <Badge variant={event.status === 'Published' ? 'default' : 'secondary'}>
                    {event.status}
                  </Badge>
                </div>
              </CardHeader>
              {(event.location || event.startDate) && (
                <CardContent className="pt-0">
                  {event.location && (
                    <p className="text-sm text-muted-foreground">{event.location}</p>
                  )}
                  {event.startDate && (
                    <p className="text-sm text-muted-foreground">
                      {event.startDate}
                      {event.endDate ? ` — ${event.endDate}` : ''}
                    </p>
                  )}
                </CardContent>
              )}
            </Card>
          ))}
        </div>
      </div>
    </div>
  );
}
