import { useState } from 'react';
import { api, type SectionAttachment } from '@/lib/api';
import { UploadZone } from './UploadZone';
import { Button } from '@/components/ui/button';

interface SectionAttachmentsProps {
  eventId: string;
  sectionId: string;
  attachments: SectionAttachment[];
  canUpload?: boolean;
  onRefresh: () => void;
}

export function SectionAttachments({
  eventId,
  sectionId,
  attachments,
  canUpload = false,
  onRefresh,
}: SectionAttachmentsProps) {
  const [error, setError] = useState<string | undefined>();

  const handleUpload = async (file: File) => {
    setError(undefined);
    try {
      const upload = await api.get<{
        uploadId: string;
        presignedPutUrl: string;
        r2Key: string;
      }>(
        `/events/${eventId}/info-sections/${sectionId}/attachments/upload-url?fileName=${encodeURIComponent(file.name)}&contentType=${encodeURIComponent(file.type || 'application/octet-stream')}&fileSizeBytes=${file.size}`
      );

      const putResponse = await fetch(upload.presignedPutUrl, {
        method: 'PUT',
        headers: { 'Content-Type': file.type || 'application/octet-stream' },
        body: file,
      });

      if (!putResponse.ok) {
        throw new Error('Upload to storage failed');
      }

      await api.confirmInfoSectionAttachment(eventId, sectionId, {
        r2Key: upload.r2Key,
        friendlyName: file.name,
        contentType: file.type || 'application/octet-stream',
        fileSizeBytes: file.size,
      });

      onRefresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Upload failed');
    }
  };

  const handleDownload = async (attachmentId: string) => {
    const { downloadUrl } = await api.getInfoSectionAttachmentDownloadUrl(
      eventId,
      sectionId,
      attachmentId
    );
    window.location.href = downloadUrl;
  };

  return (
    <div className="space-y-3 rounded border p-3">
      <h4 className="font-semibold">Attachments</h4>
      {canUpload && <UploadZone onFile={handleUpload} error={error} />}
      <ul className="space-y-2">
        {attachments.map((attachment) => (
          <li key={attachment.id} className="flex items-center justify-between rounded border p-2 text-sm">
            <span>{attachment.friendlyName}</span>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => handleDownload(attachment.id)}
            >
              Download
            </Button>
          </li>
        ))}
      </ul>
    </div>
  );
}
