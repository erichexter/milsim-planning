import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';
import { api } from '../../lib/api';
import { Badge } from '../../components/ui/badge';
import type { BriefingSummaryDto } from '../../lib/api';

function publicationStateBadgeVariant(state: BriefingSummaryDto['publicationState']) {
  switch (state) {
    case 'Published':
      return 'default' as const;   // green
    case 'Archived':
      return 'destructive' as const; // red
    default:
      return 'secondary' as const;  // gray — Draft
  }
}

function formatUpdatedAt(iso: string): string {
  const date = new Date(iso);
  return date.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function BriefingsListPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['briefings', { limit: 20, offset: 0 }],
    queryFn: () => api.getBriefings(20, 0),
  });

  if (isLoading) return <div>Loading briefing channels...</div>;

  if (error) {
    return (
      <div className="p-6">
        <p className="text-destructive">Failed to load briefing channels.</p>
      </div>
    );
  }

  const items = data?.items ?? [];
  const pagination = data?.pagination;

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Briefing Channels</h1>
        {pagination && (
          <span className="text-sm text-muted-foreground">
            {pagination.total} channel{pagination.total !== 1 ? 's' : ''}
          </span>
        )}
      </div>

      {items.length === 0 ? (
        <p className="text-muted-foreground">No briefing channels yet.</p>
      ) : (
        <div className="space-y-3">
          {items.map((briefing) => (
            <div
              key={briefing.id}
              className="border rounded-lg p-4 flex items-center justify-between"
            >
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <Link
                    to={`/briefings/${briefing.id}`}
                    className="font-semibold hover:underline truncate"
                  >
                    {briefing.title}
                  </Link>
                  <Badge variant={publicationStateBadgeVariant(briefing.publicationState)}>
                    {briefing.publicationState}
                  </Badge>
                </div>
                {briefing.description && (
                  <p className="text-sm text-muted-foreground mt-1 truncate">
                    {briefing.description}
                  </p>
                )}
                <p className="text-xs text-muted-foreground mt-1">
                  Updated {formatUpdatedAt(briefing.updatedAt)}
                </p>
              </div>
              <div className="ml-4 shrink-0">
                <Link
                  to={`/briefings/${briefing.id}`}
                  className="text-sm font-medium hover:underline"
                >
                  View →
                </Link>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
