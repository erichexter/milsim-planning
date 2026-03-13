import { useState, type FormEvent } from 'react';
import { useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { useAuth } from '@/hooks/useAuth';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { UploadZone } from '@/components/content/UploadZone';
import { MapResourceCard } from '@/components/content/MapResourceCard';

export function MapResourcesPage() {
  const { eventId, id } = useParams<{ eventId: string; id: string }>();
  const resolvedEventId = eventId ?? id;
  const { user } = useAuth();
  const isCommander = user?.role === 'Commander';

  const [externalUrl, setExternalUrl] = useState('');
  const [friendlyName, setFriendlyName] = useState('');
  const [instructions, setInstructions] = useState('');
  const [error, setError] = useState<string | undefined>();

  const { data: resources = [], isLoading, refetch } = useQuery({
    queryKey: ['map-resources', resolvedEventId],
    queryFn: () => api.getMapResources(resolvedEventId!),
    enabled: Boolean(resolvedEventId),
  });

  if (!resolvedEventId) return <div className="p-6">Event id missing.</div>;
  if (isLoading) return <div className="p-6">Loading map resources...</div>;

  const handleAddExternal = async (event: FormEvent) => {
    event.preventDefault();
    if (!externalUrl.trim()) return;

    await api.createExternalMapResource(resolvedEventId, {
      externalUrl: externalUrl.trim(),
      instructions: instructions.trim() || null,
      friendlyName: friendlyName.trim() || null,
    });

    setExternalUrl('');
    setInstructions('');
    setFriendlyName('');
    await refetch();
  };

  const handleFileUpload = async (file: File) => {
    setError(undefined);
    try {
      const resourceId = crypto.randomUUID();
      const query = new URLSearchParams({
        fileName: file.name,
        contentType: file.type || 'application/octet-stream',
        fileSizeBytes: String(file.size),
        friendlyName: file.name,
        instructions: '',
      }).toString();

      const upload = await api.get<{
        uploadId: string;
        presignedPutUrl: string;
        r2Key: string;
      }>(`/events/${resolvedEventId}/map-resources/${resourceId}/upload-url?${query}`);

      const putResponse = await fetch(upload.presignedPutUrl, {
        method: 'PUT',
        headers: { 'Content-Type': file.type || 'application/octet-stream' },
        body: file,
      });

      if (!putResponse.ok) throw new Error('Upload to storage failed');

      await api.confirmMapResourceUpload(resolvedEventId, resourceId, {
        r2Key: upload.r2Key,
        friendlyName: file.name,
        contentType: file.type || 'application/octet-stream',
        fileSizeBytes: file.size,
      });

      await refetch();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'File upload failed');
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-6 p-6">
      <h1 className="text-2xl font-bold">Map Resources</h1>

      {isCommander && (
        <>
          <form onSubmit={handleAddExternal} className="space-y-2 rounded border p-4">
            <h2 className="font-semibold">Add External Link</h2>
            <Input
              placeholder="https://example.com/map"
              value={externalUrl}
              onChange={(e) => setExternalUrl(e.target.value)}
            />
            <Input
              placeholder="Friendly name"
              value={friendlyName}
              onChange={(e) => setFriendlyName(e.target.value)}
            />
            <textarea
              className="min-h-[120px] w-full rounded border p-2"
              placeholder="Optional instructions (markdown supported)"
              value={instructions}
              onChange={(e) => setInstructions(e.target.value)}
            />
            <Button type="submit" disabled={!externalUrl.trim()}>
              Add Link
            </Button>
          </form>

          <div className="space-y-2 rounded border p-4">
            <h2 className="font-semibold">Add Map File</h2>
            <UploadZone onFile={handleFileUpload} error={error} />
          </div>
        </>
      )}

      <div className="space-y-3">
        {resources
          .slice()
          .sort((a, b) => a.order - b.order)
          .map((resource) => (
            <MapResourceCard
              key={resource.id}
              eventId={resolvedEventId}
              resource={resource}
              isCommander={isCommander}
              onDelete={() => {
                void api.deleteMapResource(resolvedEventId, resource.id).then(() => {
                  void refetch();
                });
              }}
            />
          ))}
      </div>
    </div>
  );
}
