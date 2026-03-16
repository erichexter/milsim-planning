import { useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { useAuth } from '@/hooks/useAuth';
import { SectionList } from '@/components/content/SectionList';

export function BriefingPage() {
  const { eventId, id } = useParams<{ eventId: string; id: string }>();
  const resolvedEventId = eventId ?? id;
  const { user } = useAuth();
  const isCommander = user?.role === 'faction_commander';

  const { data: sections = [], isLoading, refetch } = useQuery({
    queryKey: ['info-sections', resolvedEventId],
    queryFn: () => api.getInfoSections(resolvedEventId!),
    enabled: Boolean(resolvedEventId),
  });

  if (!resolvedEventId) return <div className="p-6">Event id missing.</div>;
  if (isLoading) return <div className="p-6">Loading briefing...</div>;

  return (
    <div className="mx-auto max-w-4xl space-y-4 p-6">
      <h1 className="text-2xl font-bold">Briefing</h1>
      <SectionList
        eventId={resolvedEventId}
        sections={sections}
        onRefresh={() => {
          void refetch();
        }}
        isCommander={isCommander}
      />
    </div>
  );
}
