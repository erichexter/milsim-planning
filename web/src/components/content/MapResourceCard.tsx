import Markdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { api, type MapResource } from '@/lib/api';

interface MapResourceCardProps {
  eventId: string;
  resource: MapResource;
  isCommander: boolean;
  onDelete: () => void;
}

function fileLabel(resource: MapResource) {
  const name = resource.friendlyName;
  if (!name || !name.includes('.')) return 'File';
  return name.split('.').pop()?.toUpperCase() ?? 'File';
}

export function MapResourceCard({
  eventId,
  resource,
  isCommander,
  onDelete,
}: MapResourceCardProps) {
  const isExternalLink = Boolean(resource.externalUrl);

  const handleDownload = async () => {
    const { downloadUrl } = await api.getMapResourceDownloadUrl(eventId, resource.id);
    window.location.href = downloadUrl;
  };

  return (
    <div className="space-y-3 rounded border p-4">
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <h3 className="font-semibold">{resource.friendlyName || 'Untitled resource'}</h3>
          <Badge variant="secondary">{isExternalLink ? 'External Link' : fileLabel(resource)}</Badge>
        </div>

        {isCommander && (
          <Button type="button" variant="destructive" size="sm" onClick={onDelete}>
            Delete
          </Button>
        )}
      </div>

      {isExternalLink ? (
        <div className="space-y-2 text-sm">
          <a
            href={resource.externalUrl ?? '#'}
            target="_blank"
            rel="noreferrer"
            className="text-blue-600 underline"
          >
            {resource.externalUrl}
          </a>
          {resource.instructions && (
            <div className="prose prose-sm max-w-none">
              <Markdown remarkPlugins={[remarkGfm]}>{resource.instructions}</Markdown>
            </div>
          )}
        </div>
      ) : (
        <div className="flex items-center justify-between text-sm">
          <span>{resource.friendlyName}</span>
          <Button type="button" variant="outline" size="sm" onClick={handleDownload}>
            Download
          </Button>
        </div>
      )}
    </div>
  );
}
