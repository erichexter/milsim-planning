import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router';
import { api } from '../../lib/api';
import { ImageUploadZone } from '../../components/briefings/ImageUploadZone';
import type { BriefingDto } from '../../lib/api';

export function BriefingEditorPage() {
  const { id } = useParams<{ id: string }>();

  const {
    data: briefing,
    isLoading,
    error,
  } = useQuery<BriefingDto>({
    queryKey: ['briefing', id],
    queryFn: () => api.getBriefing(id!),
    enabled: !!id,
  });

  if (isLoading) {
    return (
      <div className="p-6">
        <p className="text-muted-foreground">Loading briefing…</p>
      </div>
    );
  }

  if (error || !briefing) {
    return (
      <div className="p-6">
        <p className="text-destructive">Failed to load briefing.</p>
        <Link to="/briefings" className="text-sm text-primary underline mt-2 block">
          ← Back to briefings
        </Link>
      </div>
    );
  }

  return (
    <div className="p-6 max-w-2xl mx-auto">
      {/* Header */}
      <div className="mb-6">
        <Link
          to="/briefings"
          className="text-sm text-muted-foreground hover:text-foreground"
        >
          ← Briefing Channels
        </Link>
        <h1 className="text-2xl font-bold mt-2">{briefing.title}</h1>
        {briefing.description && (
          <p className="text-muted-foreground mt-1">{briefing.description}</p>
        )}
        <div className="flex items-center gap-2 mt-2">
          <span className="text-xs text-muted-foreground">
            Channel: <code className="font-mono">{briefing.channelIdentifier}</code>
          </span>
          <span className="text-xs px-1.5 py-0.5 rounded bg-muted">
            {briefing.publicationState}
          </span>
        </div>
      </div>

      {/* Image upload section */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold">Map Image</h2>
        <p className="text-sm text-muted-foreground">
          Upload the area-of-operations map image. Field participants will see it on their devices.
          Accepted: JPEG, PNG, WebP, AVIF — up to 50 MB.
        </p>
        <ImageUploadZone briefingId={briefing.id} />
      </section>
    </div>
  );
}
