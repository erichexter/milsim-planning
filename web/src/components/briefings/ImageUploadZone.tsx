import { useCallback, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../lib/api';
import type { ImageUploadDto, ImageUploadStatusDto } from '../../lib/api';

const ACCEPTED_EXTENSIONS = ['.jpg', '.jpeg', '.png', '.webp', '.avif'];
const MAX_SIZE_BYTES = 50 * 1024 * 1024; // 50 MB

type UploadState =
  | { phase: 'idle' }
  | { phase: 'uploading'; progress: number }
  | { phase: 'polling'; uploadId: string }
  | { phase: 'done'; uploadId: string }
  | { phase: 'error'; message: string };

interface Props {
  briefingId: string;
}

function ProgressBar({ value }: { value: number }) {
  return (
    <div className="w-full h-2 bg-muted rounded-full overflow-hidden">
      <div
        className="h-full bg-primary transition-all duration-300"
        style={{ width: `${Math.min(100, Math.max(0, value))}%` }}
      />
    </div>
  );
}

function UploadStatusBadge({ status }: { status: string }) {
  const colorMap: Record<string, string> = {
    Queued: 'text-muted-foreground',
    Processing: 'text-blue-600',
    Completed: 'text-green-600',
    Failed: 'text-destructive',
  };
  return (
    <span className={`text-xs font-medium ${colorMap[status] ?? 'text-muted-foreground'}`}>
      {status}
    </span>
  );
}

function PollingStatus({ briefingId, uploadId }: { briefingId: string; uploadId: string }) {
  const { data, error } = useQuery<ImageUploadStatusDto>({
    queryKey: ['imageUpload', briefingId, uploadId],
    queryFn: () => api.getBriefingImageStatus(briefingId, uploadId),
    refetchInterval: (query) => {
      const d = query.state.data;
      if (!d) return 3000;
      // Stop polling when all jobs are done or failed
      const allDone = d.resizeJobs.every(
        (j) => j.resizeStatus === 'Completed' || j.resizeStatus === 'Failed'
      );
      return allDone ? false : 3000;
    },
  });

  if (error) {
    return <p className="text-xs text-destructive">Failed to load status.</p>;
  }

  if (!data) {
    return <p className="text-xs text-muted-foreground">Loading status…</p>;
  }

  return (
    <div className="space-y-1">
      <p className="text-xs font-medium">
        Upload: <UploadStatusBadge status={data.uploadStatus} />
      </p>
      {data.resizeJobs.length > 0 && (
        <div className="space-y-0.5">
          {data.resizeJobs.map((job) => (
            <div key={job.jobId} className="flex items-center justify-between text-xs">
              <span className="text-muted-foreground">{job.dimensions}</span>
              <UploadStatusBadge status={job.resizeStatus} />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export function ImageUploadZone({ briefingId }: Props) {
  const [uploadState, setUploadState] = useState<UploadState>({ phase: 'idle' });
  const [isDragOver, setIsDragOver] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const validateFile = (file: File): string | null => {
    const ext = '.' + file.name.split('.').pop()?.toLowerCase();
    if (!ACCEPTED_EXTENSIONS.includes(ext)) {
      return `File type "${ext}" is not allowed. Accepted: ${ACCEPTED_EXTENSIONS.join(', ')}`;
    }
    if (file.size > MAX_SIZE_BYTES) {
      return `File is too large (${(file.size / 1024 / 1024).toFixed(1)} MB). Maximum is 50 MB.`;
    }
    return null;
  };

  const uploadFile = useCallback(
    async (file: File) => {
      const validationError = validateFile(file);
      if (validationError) {
        setUploadState({ phase: 'error', message: validationError });
        return;
      }

      setUploadState({ phase: 'uploading', progress: 0 });

      // Use XMLHttpRequest for progress tracking
      const uploadId = await new Promise<string>((resolve, reject) => {
        const token = localStorage.getItem('token');
        const xhr = new XMLHttpRequest();

        xhr.upload.addEventListener('progress', (e) => {
          if (e.lengthComputable) {
            const pct = Math.round((e.loaded / e.total) * 100);
            setUploadState({ phase: 'uploading', progress: pct });
          }
        });

        xhr.addEventListener('load', () => {
          if (xhr.status === 202) {
            try {
              const dto: ImageUploadDto = JSON.parse(xhr.responseText);
              resolve(dto.uploadId);
            } catch {
              reject(new Error('Invalid response from server.'));
            }
          } else if (xhr.status === 401) {
            window.location.href = '/auth/login';
            reject(new Error('Unauthorized.'));
          } else {
            try {
              const err = JSON.parse(xhr.responseText);
              reject(new Error(err.detail ?? err.error ?? 'Upload failed.'));
            } catch {
              reject(new Error(`Upload failed with status ${xhr.status}.`));
            }
          }
        });

        xhr.addEventListener('error', () => reject(new Error('Network error during upload.')));

        const BASE_URL = (import.meta.env.DEV ? '' : (import.meta.env.VITE_API_URL ?? '')) + '/api';
        xhr.open('POST', `${BASE_URL}/v1/briefings/${briefingId}/images`);
        if (token) xhr.setRequestHeader('Authorization', `Bearer ${token}`);

        const form = new FormData();
        form.append('file', file);
        xhr.send(form);
      }).catch((err: Error) => {
        setUploadState({ phase: 'error', message: err.message });
        return null;
      });

      if (uploadId) {
        setUploadState({ phase: 'polling', uploadId });
      }
    },
    [briefingId]
  );

  const handleDrop = useCallback(
    (e: React.DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      setIsDragOver(false);
      const file = e.dataTransfer.files?.[0];
      if (file) uploadFile(file);
    },
    [uploadFile]
  );

  const handleFileChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (file) uploadFile(file);
      // Reset input so the same file can be re-selected after an error
      e.target.value = '';
    },
    [uploadFile]
  );

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragOver(true);
  };

  const handleDragLeave = () => setIsDragOver(false);

  const reset = () => setUploadState({ phase: 'idle' });

  return (
    <div className="space-y-3">
      {/* Drop zone */}
      <div
        role="button"
        tabIndex={0}
        aria-label="Drop image here or click to select"
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onClick={() => inputRef.current?.click()}
        onKeyDown={(e) => e.key === 'Enter' && inputRef.current?.click()}
        className={[
          'border-2 border-dashed rounded-lg p-8 text-center cursor-pointer transition-colors',
          isDragOver
            ? 'border-primary bg-primary/5'
            : 'border-muted-foreground/30 hover:border-primary/50',
          uploadState.phase === 'uploading' ? 'pointer-events-none opacity-75' : '',
        ].join(' ')}
      >
        <input
          ref={inputRef}
          type="file"
          accept={ACCEPTED_EXTENSIONS.join(',')}
          className="sr-only"
          onChange={handleFileChange}
        />

        {uploadState.phase === 'idle' && (
          <>
            <div className="text-4xl mb-2">🗺️</div>
            <p className="text-sm font-medium">Drag &amp; drop a map image here</p>
            <p className="text-xs text-muted-foreground mt-1">
              JPEG, PNG, WebP, AVIF — up to 50 MB
            </p>
          </>
        )}

        {uploadState.phase === 'uploading' && (
          <div className="space-y-2">
            <p className="text-sm font-medium">Uploading… {uploadState.progress}%</p>
            <ProgressBar value={uploadState.progress} />
          </div>
        )}

        {(uploadState.phase === 'polling' || uploadState.phase === 'done') && (
          <div className="space-y-2">
            <p className="text-sm font-medium text-green-700">Upload complete — processing…</p>
          </div>
        )}

        {uploadState.phase === 'error' && (
          <div className="space-y-2">
            <p className="text-sm font-medium text-destructive">Upload failed</p>
            <p className="text-xs text-destructive/80">{uploadState.message}</p>
          </div>
        )}
      </div>

      {/* Polling status */}
      {uploadState.phase === 'polling' && (
        <div className="rounded-md border p-3 bg-muted/30">
          <PollingStatus briefingId={briefingId} uploadId={uploadState.uploadId} />
        </div>
      )}

      {/* Reset button after error */}
      {uploadState.phase === 'error' && (
        <button
          type="button"
          onClick={reset}
          className="text-xs text-primary underline hover:no-underline"
        >
          Try again
        </button>
      )}
    </div>
  );
}
