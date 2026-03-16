import { useState } from 'react';
import { useParams, Link } from 'react-router';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ChevronRight } from 'lucide-react';
import { api } from '../../lib/api';
import { useAuth } from '../../hooks/useAuth';
import { Badge } from '../../components/ui/badge';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';

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

  const [editing, setEditing] = useState(false);
  const [name, setName] = useState('');
  const [location, setLocation] = useState('');
  const [description, setDescription] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');

  const publishMutation = useMutation({
    mutationFn: () => api.publishEvent(id!),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['events'] }),
  });

  const updateMutation = useMutation({
    mutationFn: () =>
      api.updateEvent(id!, {
        name: name.trim(),
        location: location.trim() || null,
        description: description.trim() || null,
        startDate: startDate || null,
        endDate: endDate || null,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['events'] });
      setEditing(false);
    },
  });

  if (!event) return <div className="p-6">Event not found.</div>;

  const openEdit = () => {
    setName(event.name);
    setLocation(event.location ?? '');
    setDescription(event.description ?? '');
    setStartDate(event.startDate ?? '');
    setEndDate(event.endDate ?? '');
    setEditing(true);
  };

  return (
    <div className="p-6 max-w-4xl mx-auto space-y-4">
      <nav className="flex items-center gap-1.5 text-sm text-muted-foreground">
        <Link to="/dashboard" className="hover:text-foreground transition-colors">
          Dashboard
        </Link>
        <ChevronRight className="h-3.5 w-3.5 shrink-0" />
        <span className="text-foreground font-medium max-w-[300px] truncate">{event.name}</span>
      </nav>

      {editing ? (
        <div className="space-y-3 rounded border p-4">
          <h2 className="font-semibold">Edit Event</h2>
          <div className="space-y-2">
            <label className="text-sm font-medium block">Name *</label>
            <Input value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium block">Location</label>
            <Input value={location} onChange={(e) => setLocation(e.target.value)} placeholder="e.g. Fox Airsoft, Victorville CA" />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium block">Description</label>
            <textarea
              className="min-h-[80px] w-full rounded border p-2 text-sm"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Optional event description"
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <label className="text-sm font-medium block">Start Date</label>
              <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium block">End Date</label>
              <Input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
            </div>
          </div>
          <div className="flex gap-2 pt-1">
            <Button
              onClick={() => updateMutation.mutate()}
              disabled={!name.trim() || updateMutation.isPending}
            >
              {updateMutation.isPending ? 'Saving...' : 'Save'}
            </Button>
            <Button variant="outline" onClick={() => setEditing(false)}>
              Cancel
            </Button>
          </div>
          {updateMutation.isError && (
            <p className="text-sm text-destructive">
              {(updateMutation.error as Error).message}
            </p>
          )}
        </div>
      ) : (
        <>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold">{event.name}</h1>
            <Badge variant={event.status === 'Published' ? 'default' : 'secondary'}>
              {event.status}
            </Badge>
            {isCommander && (
              <Button variant="ghost" size="sm" onClick={openEdit}>
                Edit
              </Button>
            )}
          </div>

          {event.location && <p className="text-muted-foreground">{event.location}</p>}
          {event.description && <p className="text-sm">{event.description}</p>}
          {event.startDate && <p className="text-sm">Start: {event.startDate}</p>}
          {event.endDate && <p className="text-sm">End: {event.endDate}</p>}
        </>
      )}

      <div className="flex flex-wrap gap-3 pt-4">
        {isCommander && event.status === 'Draft' && !editing && (
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
        <Button variant="outline" asChild>
          <Link to={`/events/${id}/briefing`}>Briefing</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link to={`/events/${id}/maps`}>Maps</Link>
        </Button>
        {isCommander && (
          <Button variant="outline" asChild>
            <Link to={`/events/${id}/notifications`}>Notifications</Link>
          </Button>
        )}
        {isCommander && (
          <Button variant="outline" asChild>
            <Link to={`/events/${id}/change-requests`}>Change Requests</Link>
          </Button>
        )}
      </div>
    </div>
  );
}
