import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
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
  const isImage = resource.isFile && resource.contentType?.startsWith('image/');
  const isPdf = resource.isFile && resource.contentType === 'application/pdf';
  const isViewable = isImage || isPdf;

  // Eagerly fetch a download URL for viewable files so they render inline.
  // For non-viewable files it's fetched on-demand when Download is clicked.
  const { data: downloadUrlData } = useQuery({
    queryKey: ['map-download-url', resource.id],
    queryFn: () => api.getMapResourceDownloadUrl(eventId, resource.id),
    enabled: resource.isFile && isViewable,
    staleTime: 1000 * 60 * 50,   // presigned URLs are valid for 1h; refresh at 50min
  });

  const [downloading, setDownloading] = useState(false);

  const handleDownload = async () => {
    setDownloading(true);
    try {
      const { downloadUrl } = await api.getMapResourceDownloadUrl(eventId, resource.id);
      // Open in new tab so PDFs / images display rather than trigger a save dialog
      window.open(downloadUrl, '_blank', 'noreferrer');
    } finally {
      setDownloading(false);
    }
  };

  return (
    <div className="space-y-3 rounded border p-4">
      {/* Header row */}
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <h3 className="font-semibold">{resource.friendlyName || 'Untitled resource'}</h3>
          <Badge variant="secondary">{isExternalLink ? 'External Link' : fileLabel(resource)}</Badge>
        </div>

        <div className="flex items-center gap-2">
          {resource.isFile && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={handleDownload}
              disabled={downloading}
            >
              {downloading ? 'Opening…' : 'Open'}
            </Button>
          )}
          {isCommander && (
            <Button type="button" variant="destructive" size="sm" onClick={onDelete}>
              Delete
            </Button>
          )}
        </div>
      </div>

      {/* External link */}
      {isExternalLink && (
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
      )}

      {/* Inline image preview */}
      {isImage && downloadUrlData?.downloadUrl && (
        <img
          src={downloadUrlData.downloadUrl}
          alt={resource.friendlyName ?? 'Map image'}
          className="w-full rounded border object-contain max-h-[600px]"
        />
      )}

      {/* Inline PDF viewer */}
      {isPdf && downloadUrlData?.downloadUrl && (
        <iframe
          src={downloadUrlData.downloadUrl}
          title={resource.friendlyName ?? 'Map PDF'}
          className="w-full rounded border"
          style={{ height: '600px' }}
        />
      )}

      {/* Non-viewable file — just show name + open button (already in header) */}
      {resource.isFile && !isViewable && (
        <p className="text-sm text-muted-foreground">{resource.friendlyName}</p>
      )}
    </div>
  );
}
