import { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  MapPin,
  ChevronDown,
  ChevronUp,
  ExternalLink,
  ClipboardList,
  Shield,
  CalendarDays,
} from 'lucide-react';
import Markdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { api, type MapResource } from '../../lib/api';
import { EventBreadcrumb } from '../EventBreadcrumb';
import { Card, CardContent } from '../ui/card';
import { Badge } from '../ui/badge';
import { Button } from '../ui/button';

interface AssignmentDto {
  id: string;
  name: string;
  callsign: string | null;
  teamAffiliation: string | null;
  role: string | null;
  platoon: { id: string; name: string } | null;
  squad: { id: string; name: string } | null;
  isAssigned: boolean;
}

interface ChangeRequestDto {
  id: string;
  note: string;
  status: string;
  commanderNote: string | null;
  createdAt: string;
}

interface Props {
  eventId: string;
  onNavigate: (tab: 'roster' | 'briefing' | 'maps' | 'change-request') => void;
}

// ── Countdown helpers ───────────────────────────────────────────────────────

function parseEventDate(dateOnly: string): Date {
  // dateOnly = "YYYY-MM-DD" → treat as midnight local
  return new Date(dateOnly + 'T00:00:00');
}

interface CountdownDisplay {
  value: string;
  unit: string | null; // null = no unit label (e.g. "NOW" or HH:MM:SS)
}

function formatCountdown(ms: number): CountdownDisplay {
  if (ms <= 0) return { value: 'NOW', unit: null };
  const totalSecs = Math.floor(ms / 1000);
  const days = Math.floor(totalSecs / 86400);
  if (days >= 1) return { value: String(days), unit: 'Days' };
  const h = Math.floor(totalSecs / 3600).toString().padStart(2, '0');
  const m = Math.floor((totalSecs % 3600) / 60).toString().padStart(2, '0');
  const s = (totalSecs % 60).toString().padStart(2, '0');
  return { value: `${h}:${m}:${s}`, unit: null };
}

function useCountdown(startDate: string | null | undefined) {
  const [display, setDisplay] = useState<CountdownDisplay | null>(null);

  useEffect(() => {
    if (!startDate) { setDisplay(null); return; }
    const target = parseEventDate(startDate);

    function tick() {
      const ms = target.getTime() - Date.now();
      setDisplay(formatCountdown(ms));
    }
    tick();
    const id = setInterval(tick, 1000);
    return () => clearInterval(id);
  }, [startDate]);

  return display;
}

// ── Date display ────────────────────────────────────────────────────────────

function formatDate(d: string | null) {
  if (!d) return null;
  return new Date(d + 'T00:00:00').toLocaleDateString(undefined, {
    weekday: 'short', month: 'short', day: 'numeric', year: 'numeric',
  });
}

// ── Component ───────────────────────────────────────────────────────────────

export function PlayerOverviewTab({ eventId, onNavigate }: Props) {
  const [expandedSections, setExpandedSections] = useState<Set<string>>(new Set());

  const { data: events, isLoading: isEventsLoading } = useQuery({
    queryKey: ['events'],
    queryFn: () => api.getEvents(),
  });
  const event = events?.find((e) => e.id === eventId);

  const { data: assignment, isLoading: isAssignmentLoading } = useQuery<AssignmentDto>({
    queryKey: ['events', eventId, 'my-assignment'],
    queryFn: async () => {
      try {
        return await api.get<AssignmentDto>(`/events/${eventId}/my-assignment`);
      } catch (e: unknown) {
        const err = e as { status?: number };
        if (err?.status === 404)
          return { id: '', name: '', callsign: null, teamAffiliation: null, role: null, platoon: null, squad: null, isAssigned: false };
        throw e;
      }
    },
  });

  const { data: myRequest } = useQuery<ChangeRequestDto | null>({
    queryKey: ['events', eventId, 'roster-change-requests', 'mine'],
    queryFn: async () => {
      try {
        return await api.get<ChangeRequestDto>(`/events/${eventId}/roster-change-requests/mine`);
      } catch (e: unknown) {
        const err = e as { status?: number };
        if (err?.status === 204 || err?.status === 404) return null;
        throw e;
      }
    },
  });

  const { data: sections = [], isLoading: isSectionsLoading } = useQuery({
    queryKey: ['info-sections', eventId],
    queryFn: () => api.getInfoSections(eventId),
  });

  const { data: mapResources = [], isLoading: isMapResourcesLoading } = useQuery({
    queryKey: ['map-resources', eventId],
    queryFn: () => api.getMapResources(eventId),
  });

  const countdown = useCountdown(event?.startDate);

  const toggleSection = (id: string) =>
    setExpandedSections((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });

  const isAssigned = assignment?.isAssigned ?? false;
  const hasPendingRequest = myRequest?.status === 'Pending';

  if (isEventsLoading || isAssignmentLoading) {
    return <div className="p-6 text-muted-foreground">Loading event overview...</div>;
  }

  if (!event) {
    return <div className="p-6 text-muted-foreground">Event not found.</div>;
  }

  return (
    <div className="mx-auto max-w-3xl lg:max-w-5xl space-y-5 p-5">
      <EventBreadcrumb eventId={eventId} page="Overview" />

      {/* ── Event header ──────────────────────────────────────────────── */}
      <div className="space-y-2">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-xl font-semibold">{event?.name ?? '…'}</h1>
          {event?.status && (
            <Badge variant={event.status === 'Published' ? 'default' : 'secondary'}>
              {event.status}
            </Badge>
          )}
        </div>

        {/* Meta row */}
        <div className="flex flex-wrap gap-4 text-xs text-muted-foreground">
          {event?.location && (
            <span className="flex items-center gap-1">
              <MapPin className="h-3 w-3 shrink-0" />
              {event.location}
            </span>
          )}
          {event?.startDate && (
            <span className="flex items-center gap-1">
              <CalendarDays className="h-3 w-3 shrink-0" />
              {formatDate(event.startDate)}
              {event.endDate ? ` — ${formatDate(event.endDate)}` : ''}
            </span>
          )}
        </div>

        {event?.description && (
          <p className="text-sm text-muted-foreground">{event.description}</p>
        )}

        {/* Countdown */}
        {countdown && (
          <div className="flex items-center gap-3 pt-1">
            <span className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
              Countdown
            </span>
            <span
              className="font-mono font-bold animate-pulse"
              style={{ fontSize: 28, color: 'oklch(var(--primary))' }}
            >
              {countdown.value}
            </span>
            {countdown.unit && (
              <span
                className="font-bold animate-pulse"
                style={{ fontSize: 20, color: 'oklch(var(--primary))' }}
              >
                {countdown.unit}
              </span>
            )}
          </div>
        )}
      </div>

      {/* ── Assignment unit card ───────────────────────────────────────── */}
      <Card>
        <CardContent className="p-4 space-y-3">
          {/* Card header row */}
          <div className="flex items-center gap-2">
            <span
              className="h-7 w-7 rounded-[8px] flex items-center justify-center shrink-0"
              style={{
                backgroundColor: 'oklch(var(--primary-soft))',
                border: '1px solid oklch(var(--primary-border))',
              }}
            >
              <Shield className="h-3.5 w-3.5" style={{ color: 'oklch(var(--primary))' }} />
            </span>
            <span className="rp0-label">My Assignment</span>
          </div>

          {/* Callsign / slot code */}
          <div
            className="font-mono font-medium text-2xl leading-none"
            style={{ color: isAssigned ? 'oklch(var(--primary))' : 'oklch(var(--muted-foreground))' }}
          >
            {assignment?.callsign ? `[${assignment.callsign}]` : '[—]'}
          </div>

          {/* Assignment details or unassigned notice */}
          {!isAssigned ? (
            <div
              className="rounded-[8px] p-3"
              style={{ backgroundColor: 'oklch(var(--muted))' }}
            >
              <p className="text-xs font-medium">Unassigned</p>
              <p className="text-xs text-muted-foreground mt-0.5">
                You have not been assigned to a platoon and squad yet.
              </p>
            </div>
          ) : (
            <div className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5">
              {assignment?.platoon && (
                <>
                  <span className="rp0-label self-center">Platoon</span>
                  <span className="text-sm font-medium">{assignment.platoon.name}</span>
                </>
              )}
              {assignment?.squad && (
                <>
                  <span className="rp0-label self-center">Squad</span>
                  <span className="text-sm font-medium">{assignment.squad.name}</span>
                </>
              )}
              {assignment?.teamAffiliation && (
                <>
                  <span className="rp0-label self-center">Team</span>
                  <Badge variant="info" className="w-fit">{assignment.teamAffiliation}</Badge>
                </>
              )}
              {assignment?.role && (
                <>
                  <span className="rp0-label self-center">Role</span>
                  <span className="text-sm">{assignment.role}</span>
                </>
              )}
            </div>
          )}

          {/* Change request link */}
          {isAssigned && (
            <div className="pt-2 border-t">
              <Button
                variant="ghost"
                size="sm"
                className="h-auto p-0 text-xs text-muted-foreground hover:text-foreground gap-1.5"
                onClick={() => onNavigate('change-request')}
              >
                <ClipboardList className="h-3.5 w-3.5" />
                {hasPendingRequest ? (
                  <span className="flex items-center gap-1.5">
                    Change request
                    <Badge variant="secondary" className="text-[9px]">Pending</Badge>
                  </span>
                ) : (
                  'Request a change'
                )}
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      {/* ── Briefing mini-cards ────────────────────────────────────────── */}
      {isSectionsLoading ? (
        <Card>
          <CardContent className="p-4 text-sm text-muted-foreground">
            Loading briefing preview...
          </CardContent>
        </Card>
      ) : sections.length > 0 && (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="rp0-label">Briefing</span>
            <Button variant="ghost" size="sm" className="text-xs h-7 px-2" onClick={() => onNavigate('briefing')}>
              View all
            </Button>
          </div>
          <div className="space-y-1.5">
            {sections.slice().sort((a, b) => a.order - b.order).map((section) => {
              const expanded = expandedSections.has(section.id);
              return (
                <Card key={section.id}>
                  <CardContent className="p-0">
                    <button
                      type="button"
                      className="flex w-full items-center justify-between px-4 py-3 text-left"
                      onClick={() => toggleSection(section.id)}
                    >
                      <span className="text-sm font-medium">{section.title}</span>
                      {expanded
                        ? <ChevronUp className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                        : <ChevronDown className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                      }
                    </button>
                    {expanded && section.bodyMarkdown && (
                      <div className="prose prose-sm max-w-none border-t px-4 pb-4 pt-3">
                        <Markdown remarkPlugins={[remarkGfm]}>{section.bodyMarkdown}</Markdown>
                      </div>
                    )}
                  </CardContent>
                </Card>
              );
            })}
          </div>
        </div>
      )}

      {/* ── Maps ──────────────────────────────────────────────────────── */}
      {isMapResourcesLoading ? (
        <Card>
          <CardContent className="p-4 text-sm text-muted-foreground">
            Loading map preview...
          </CardContent>
        </Card>
      ) : mapResources.length > 0 && (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="rp0-label">Maps</span>
            <Button variant="ghost" size="sm" className="text-xs h-7 px-2" onClick={() => onNavigate('maps')}>
              View all
            </Button>
          </div>
          <div className="space-y-2">
            {mapResources.slice().sort((a, b) => a.order - b.order).map((resource) => (
              <MapResourcePreview key={resource.id} eventId={eventId} resource={resource} />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

// ── Inline map resource preview ────────────────────────────────────────────

interface MapResourcePreviewProps {
  eventId: string;
  resource: MapResource;
}

function MapResourcePreview({ eventId, resource }: MapResourcePreviewProps) {
  const isImage = resource.isFile && resource.contentType?.startsWith('image/');
  const isPdf = resource.isFile && resource.contentType === 'application/pdf';
  const isViewable = isImage || isPdf;

  const { data: urlData } = useQuery({
    queryKey: ['map-download-url', resource.id],
    queryFn: () => api.getMapResourceDownloadUrl(eventId, resource.id),
    enabled: resource.isFile && isViewable,
    staleTime: 1000 * 60 * 50,
  });

  return (
    <Card className="overflow-hidden">
      {/* Header row */}
      <div className="flex items-center justify-between gap-2 px-4 py-3">
        <span className="text-sm font-medium">{resource.friendlyName ?? 'Untitled'}</span>
        {resource.externalUrl && (
          <a
            href={resource.externalUrl}
            target="_blank"
            rel="noreferrer"
            className="flex items-center gap-1 text-xs hover:underline"
            style={{ color: 'oklch(var(--primary))' }}
          >
            Open <ExternalLink className="h-3 w-3" />
          </a>
        )}
        {resource.isFile && urlData?.downloadUrl && !isViewable && (
          <a
            href={urlData.downloadUrl}
            target="_blank"
            rel="noreferrer"
            className="flex items-center gap-1 text-xs hover:underline"
            style={{ color: 'oklch(var(--primary))' }}
          >
            Open <ExternalLink className="h-3 w-3" />
          </a>
        )}
      </div>

      {isImage && urlData?.downloadUrl && (
        <img
          src={urlData.downloadUrl}
          alt={resource.friendlyName ?? 'Map'}
          className="w-full block"
        />
      )}

      {isPdf && urlData?.downloadUrl && (
        <iframe
          src={urlData.downloadUrl}
          title={resource.friendlyName ?? 'Map PDF'}
          className="w-full block border-t"
          style={{ height: '600px' }}
        />
      )}

      {resource.externalUrl && resource.instructions && (
        <div className="prose prose-sm max-w-none px-4 pb-4 text-muted-foreground">
          <Markdown remarkPlugins={[remarkGfm]}>{resource.instructions}</Markdown>
        </div>
      )}
    </Card>
  );
}
