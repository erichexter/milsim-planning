import { useState } from 'react';
import { useParams, Link } from 'react-router';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ChevronRight, Users, BookOpen, Map, AlertCircle, Calendar, MapPin, Eye } from 'lucide-react';
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

  const { data: events, isLoading: isEventsLoading } = useQuery({
    queryKey: ['events'],
    queryFn: () => api.getEvents(),
  });
  const event = events?.find((e) => e.id === id);

  // ── Summary data — fetched in parallel ───────────────────────────────────
  const { data: roster } = useQuery({
    queryKey: ['roster', id],
    queryFn: () => api.getRoster(id!),
    enabled: !!id,
  });

  const { data: infoSections } = useQuery({
    queryKey: ['info-sections', id],
    queryFn: () => api.getInfoSections(id!),
    enabled: !!id,
  });

  const { data: mapResources } = useQuery({
    queryKey: ['map-resources', id],
    queryFn: () => api.getMapResources(id!),
    enabled: !!id,
  });

  // ── Edit form state ───────────────────────────────────────────────────────
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

  if (isEventsLoading) return <div className="p-6">Loading event...</div>;
  if (!event) return <div className="p-6">Event not found.</div>;

  const openEdit = () => {
    setName(event.name);
    setLocation(event.location ?? '');
    setDescription(event.description ?? '');
    setStartDate(event.startDate ?? '');
    setEndDate(event.endDate ?? '');
    setEditing(true);
  };

  // ── Derived stats ─────────────────────────────────────────────────────────
  const totalPlayers = roster
    ? roster.platoons.reduce(
        (sum, p) => sum + p.hqPlayers.length + p.squads.reduce((s, sq) => s + sq.players.length, 0),
        0
      ) + roster.unassignedPlayers.length
    : null;

  const assignedPlayers = roster
    ? roster.platoons.reduce(
        (sum, p) => sum + p.hqPlayers.length + p.squads.reduce((s, sq) => s + sq.players.length, 0),
        0
      )
    : null;

  const unassignedCount = roster ? roster.unassignedPlayers.length : null;

  const formatDate = (d: string | null) => {
    if (!d) return null;
    return new Date(d + 'T00:00:00').toLocaleDateString(undefined, {
      month: 'short', day: 'numeric', year: 'numeric',
    });
  };

  return (
    <div className="p-6 max-w-4xl lg:max-w-5xl mx-auto space-y-6">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-1.5 text-sm text-muted-foreground">
        <Link to="/dashboard" className="hover:text-foreground transition-colors">
          Dashboard
        </Link>
        <ChevronRight className="h-3.5 w-3.5 shrink-0" />
        <span className="text-foreground font-medium max-w-[300px] truncate">{event.name}</span>
      </nav>

      {/* ── Edit form ────────────────────────────────────────────────────── */}
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
              className="min-h-[80px] w-full rounded-[8px] border bg-card px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
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
            <Button onClick={() => updateMutation.mutate()} disabled={!name.trim() || updateMutation.isPending}>
              {updateMutation.isPending ? 'Saving...' : 'Save'}
            </Button>
            <Button variant="outline" onClick={() => setEditing(false)}>Cancel</Button>
          </div>
          {updateMutation.isError && (
            <p className="text-sm text-destructive">{(updateMutation.error as Error).message}</p>
          )}
        </div>
      ) : (
        <>
          {/* ── Event header ───────────────────────────────────────────── */}
          <div className="space-y-2">
            <div className="flex items-center gap-3 flex-wrap">
              <h1 className="text-xl font-semibold">{event.name}</h1>
              <Badge variant={event.status === 'Published' ? 'default' : 'secondary'}>
                {event.status}
              </Badge>
              {isCommander && (
                <Button variant="ghost" size="sm" onClick={openEdit}>Edit</Button>
              )}
            </div>

            <div className="flex flex-wrap gap-4 text-sm text-muted-foreground">
              {event.location && (
                <span className="flex items-center gap-1">
                  <MapPin className="h-3.5 w-3.5" />
                  {event.location}
                </span>
              )}
              {(event.startDate || event.endDate) && (
                <span className="flex items-center gap-1">
                  <Calendar className="h-3.5 w-3.5" />
                  {formatDate(event.startDate)}
                  {event.startDate && event.endDate && ' – '}
                  {formatDate(event.endDate)}
                </span>
              )}
            </div>

            {event.description && (
              <p className="text-sm text-muted-foreground">{event.description}</p>
            )}
          </div>

          {/* ── Summary cards ───────────────────────────────────────────── */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">

            {/* Players card */}
            <Link
              to={`/events/${id}/roster`}
              className="block border rounded-lg p-4 hover:bg-muted/40 transition-colors space-y-2"
            >
              <div className="flex items-center gap-2 text-sm font-semibold">
                <Users className="h-4 w-4 text-muted-foreground" />
                Players
              </div>
              {totalPlayers === null ? (
                <p className="text-2xl font-bold text-muted-foreground">—</p>
              ) : (
                <>
                  <p className="text-2xl font-bold">{totalPlayers}</p>
                  <div className="text-xs text-muted-foreground space-y-0.5">
                    <p>{assignedPlayers} assigned</p>
                    {unassignedCount! > 0 && (
                      <p className="text-amber-600 font-medium">{unassignedCount} unassigned</p>
                    )}
                  </div>
                </>
              )}
            </Link>

            {/* Briefing card */}
            <Link
              to={`/events/${id}/briefing`}
              className="block border rounded-lg p-4 hover:bg-muted/40 transition-colors space-y-2"
            >
              <div className="flex items-center gap-2 text-sm font-semibold">
                <BookOpen className="h-4 w-4 text-muted-foreground" />
                Briefing
              </div>
              {infoSections === undefined ? (
                <p className="text-2xl font-bold text-muted-foreground">—</p>
              ) : infoSections.length === 0 ? (
                <p className="text-sm text-muted-foreground italic">No sections yet</p>
              ) : (
                <>
                  <p className="text-2xl font-bold">{infoSections.length} section{infoSections.length !== 1 ? 's' : ''}</p>
                  <ul className="text-xs text-muted-foreground space-y-0.5">
                    {infoSections.slice(0, 3).map((s) => (
                      <li key={s.id} className="truncate">· {s.title}</li>
                    ))}
                    {infoSections.length > 3 && (
                      <li className="text-muted-foreground/60">and {infoSections.length - 3} more…</li>
                    )}
                  </ul>
                </>
              )}
            </Link>

            {/* Maps card */}
            <Link
              to={`/events/${id}/maps`}
              className="block border rounded-lg p-4 hover:bg-muted/40 transition-colors space-y-2"
            >
              <div className="flex items-center gap-2 text-sm font-semibold">
                <Map className="h-4 w-4 text-muted-foreground" />
                Maps
              </div>
              {mapResources === undefined ? (
                <p className="text-2xl font-bold text-muted-foreground">—</p>
              ) : mapResources.length === 0 ? (
                <p className="text-sm text-muted-foreground italic">No maps yet</p>
              ) : (
                <>
                  <p className="text-2xl font-bold">{mapResources.length} resource{mapResources.length !== 1 ? 's' : ''}</p>
                  <ul className="text-xs text-muted-foreground space-y-0.5">
                    {mapResources.slice(0, 3).map((m) => (
                      <li key={m.id} className="truncate">· {m.friendlyName ?? m.externalUrl ?? 'Unnamed'}</li>
                    ))}
                    {mapResources.length > 3 && (
                      <li className="text-muted-foreground/60">and {mapResources.length - 3} more…</li>
                    )}
                  </ul>
                </>
              )}
            </Link>
          </div>

          {/* ── Commander controls ───────────────────────────────────────── */}
          {isCommander && (
            <div className="rounded-lg border border-dashed p-4 space-y-3">
              <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
                Commander Controls
              </p>
              <div className="flex flex-wrap gap-3">
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
                  <Link to={`/events/${id}/change-requests`}>
                    Change Requests
                    {unassignedCount !== null && unassignedCount > 0 && (
                      <span className="ml-1.5 inline-flex items-center gap-1 text-amber-600">
                        <AlertCircle className="h-3.5 w-3.5" />
                      </span>
                    )}
                  </Link>
                </Button>
                <Button variant="outline" asChild>
                  <Link to={`/events/${id}/notifications`}>Notifications</Link>
                </Button>
                <Button variant="outline" asChild>
                  <Link to={`/events/${id}/player`}>
                    <Eye className="h-4 w-4 mr-1.5" />
                    Player View
                  </Link>
                </Button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
