import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { CalendarDays, MapPin, ChevronDown, ChevronUp, ExternalLink, ClipboardList } from 'lucide-react';
import Markdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { api, type MapResource } from '../../lib/api';
import { EventBreadcrumb } from '../EventBreadcrumb';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
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

function formatDate(d: string | null) {
  if (!d) return null;
  return new Date(d + 'T00:00:00').toLocaleDateString(undefined, {
    weekday: 'short', month: 'short', day: 'numeric', year: 'numeric',
  });
}

export function PlayerOverviewTab({ eventId, onNavigate }: Props) {
  const [expandedSections, setExpandedSections] = useState<Set<string>>(new Set());

  // ── Data fetches (all parallel) ──────────────────────────────────────────
  const { data: events } = useQuery({
    queryKey: ['events'],
    queryFn: () => api.getEvents(),
  });
  const event = events?.find((e) => e.id === eventId);

  const { data: assignment } = useQuery<AssignmentDto>({
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

  const { data: sections = [] } = useQuery({
    queryKey: ['info-sections', eventId],
    queryFn: () => api.getInfoSections(eventId),
  });

  const { data: mapResources = [] } = useQuery({
    queryKey: ['map-resources', eventId],
    queryFn: () => api.getMapResources(eventId),
  });

  const toggleSection = (id: string) =>
    setExpandedSections((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });

  const isAssigned = assignment?.isAssigned ?? false;
  const hasPendingRequest = myRequest?.status === 'Pending';

  return (
    <div className="mx-auto max-w-4xl space-y-6 p-6">
      <EventBreadcrumb eventId={eventId} page="Overview" />

      {/* ── Event hero ─────────────────────────────────────────────────── */}
      <div className="space-y-1">
        <div className="flex items-center gap-2">
          <h1 className="text-2xl font-bold">{event?.name ?? '…'}</h1>
          {event?.status && (
            <Badge variant={event.status === 'Published' ? 'default' : 'secondary'}>
              {event.status}
            </Badge>
          )}
        </div>
        {(event?.location || event?.startDate) && (
          <div className="flex flex-wrap gap-4 text-sm text-muted-foreground">
            {event.location && (
              <span className="flex items-center gap-1">
                <MapPin className="h-3.5 w-3.5 shrink-0" />
                {event.location}
              </span>
            )}
            {event?.startDate && (
              <span className="flex items-center gap-1">
                <CalendarDays className="h-3.5 w-3.5 shrink-0" />
                {formatDate(event.startDate)}
                {event.endDate ? ` — ${formatDate(event.endDate)}` : ''}
              </span>
            )}
          </div>
        )}
        {event?.description && (
          <p className="text-sm text-muted-foreground pt-1">{event.description}</p>
        )}
      </div>

      {/* ── Assignment ─────────────────────────────────────────────────── */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">My Assignment</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {assignment?.callsign ? (
            <div className="font-mono text-2xl font-bold text-orange-500">
              [{assignment.callsign}]
            </div>
          ) : (
            <div className="font-mono text-2xl font-bold text-muted-foreground">[—]</div>
          )}

          {!isAssigned ? (
            <div className="rounded-md bg-muted p-3">
              <p className="text-sm font-medium">Unassigned</p>
              <p className="text-xs text-muted-foreground mt-1">
                You have not been assigned to a platoon and squad yet.
              </p>
            </div>
          ) : (
            <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
              {assignment?.platoon && (
                <>
                  <span className="text-muted-foreground">Platoon</span>
                  <span className="font-medium">{assignment.platoon.name}</span>
                </>
              )}
              {assignment?.squad && (
                <>
                  <span className="text-muted-foreground">Squad</span>
                  <span className="font-medium">{assignment.squad.name}</span>
                </>
              )}
              {assignment?.teamAffiliation && (
                <>
                  <span className="text-muted-foreground">Team</span>
                  <span className="font-medium">{assignment.teamAffiliation}</span>
                </>
              )}
              {assignment?.role && (
                <>
                  <span className="text-muted-foreground">Role</span>
                  <span className="font-medium">{assignment.role}</span>
                </>
              )}
            </div>
          )}

          {/* Change request link — only shown when assigned */}
          {isAssigned && (
            <div className="pt-1 border-t">
              <Button
                variant="ghost"
                size="sm"
                className="h-auto p-0 text-sm text-muted-foreground hover:text-foreground gap-1.5"
                onClick={() => onNavigate('change-request')}
              >
                <ClipboardList className="h-3.5 w-3.5" />
                {hasPendingRequest ? (
                  <span className="flex items-center gap-1.5">
                    Change request
                    <Badge variant="secondary" className="text-xs">Pending</Badge>
                  </span>
                ) : (
                  'Request a change'
                )}
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      {/* ── Briefing sections ──────────────────────────────────────────── */}
      {sections.length > 0 && (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold">Briefing</h2>
            <Button variant="ghost" size="sm" onClick={() => onNavigate('briefing')}>
              View all
            </Button>
          </div>
          <div className="space-y-2">
            {sections.slice().sort((a, b) => a.order - b.order).map((section) => {
              const expanded = expandedSections.has(section.id);
              return (
                <Card key={section.id}>
                  <CardContent className="p-0">
                    <button
                      type="button"
                      className="flex w-full items-center justify-between p-4 text-left"
                      onClick={() => toggleSection(section.id)}
                    >
                      <span className="font-semibold text-sm">{section.title}</span>
                      {expanded
                        ? <ChevronUp className="h-4 w-4 shrink-0 text-muted-foreground" />
                        : <ChevronDown className="h-4 w-4 shrink-0 text-muted-foreground" />
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

      {/* ── Maps ───────────────────────────────────────────────────────── */}
      {mapResources.length > 0 && (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold">Maps</h2>
            <Button variant="ghost" size="sm" onClick={() => onNavigate('maps')}>
              View all
            </Button>
          </div>
          <div className="space-y-3">
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
        <span className="font-medium text-sm">{resource.friendlyName ?? 'Untitled'}</span>
        {resource.externalUrl && (
          <a
            href={resource.externalUrl}
            target="_blank"
            rel="noreferrer"
            className="flex items-center gap-1 text-xs text-blue-600 hover:underline"
          >
            Open <ExternalLink className="h-3 w-3" />
          </a>
        )}
        {resource.isFile && urlData?.downloadUrl && !isViewable && (
          <a
            href={urlData.downloadUrl}
            target="_blank"
            rel="noreferrer"
            className="flex items-center gap-1 text-xs text-blue-600 hover:underline"
          >
            Open <ExternalLink className="h-3 w-3" />
          </a>
        )}
      </div>

      {/* Full-width image — no padding, bleeds to card edges */}
      {isImage && urlData?.downloadUrl && (
        <img
          src={urlData.downloadUrl}
          alt={resource.friendlyName ?? 'Map'}
          className="w-full block"
        />
      )}

      {/* PDF viewer */}
      {isPdf && urlData?.downloadUrl && (
        <iframe
          src={urlData.downloadUrl}
          title={resource.friendlyName ?? 'Map PDF'}
          className="w-full block border-t"
          style={{ height: '600px' }}
        />
      )}

      {/* External link instructions */}
      {resource.externalUrl && resource.instructions && (
        <div className="prose prose-sm max-w-none px-4 pb-4 text-muted-foreground">
          <Markdown remarkPlugins={[remarkGfm]}>{resource.instructions}</Markdown>
        </div>
      )}
    </Card>
  );
}
