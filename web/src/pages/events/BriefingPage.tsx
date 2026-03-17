import { useState } from 'react';
import { useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { useAuth } from '@/hooks/useAuth';
import { SectionList } from '@/components/content/SectionList';
import { EventBreadcrumb } from '@/components/EventBreadcrumb';
import { Button } from '@/components/ui/button';

export function BriefingPage() {
  const { eventId, id } = useParams<{ eventId: string; id: string }>();
  const resolvedEventId = eventId ?? id;
  const { user } = useAuth();
  const isCommander = user?.role === 'faction_commander';
  const [preview, setPreview] = useState(false);

  const { data: sections = [], isLoading, refetch } = useQuery({
    queryKey: ['info-sections', resolvedEventId],
    queryFn: () => api.getInfoSections(resolvedEventId!),
    enabled: Boolean(resolvedEventId),
  });

  if (!resolvedEventId) return <div className="p-6">Event id missing.</div>;
  if (isLoading) return <div className="p-6">Loading briefing...</div>;

  return (
    <div className="mx-auto max-w-4xl lg:max-w-5xl space-y-4 p-6">
      <EventBreadcrumb eventId={resolvedEventId} page="Briefing" />
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Briefing</h1>
        {isCommander && (
          <Button
            variant={preview ? 'default' : 'outline'}
            size="sm"
            onClick={() => setPreview(v => !v)}
          >
            {preview ? 'Exit Preview' : 'Player Preview'}
          </Button>
        )}
      </div>
      {preview && (
        <p className="text-sm text-muted-foreground border rounded px-3 py-2">
          Previewing as player — edit controls are hidden.
        </p>
      )}
      <SectionList
        eventId={resolvedEventId}
        sections={sections}
        onRefresh={() => {
          void refetch();
        }}
        isCommander={isCommander && !preview}
      />
    </div>
  );
}
