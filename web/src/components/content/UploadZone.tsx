import { useDropzone } from 'react-dropzone';

interface UploadZoneProps {
  onFile: (file: File) => void;
  accept?: Record<string, string[]>;
  maxSizeMB?: number;
  error?: string;
  disabled?: boolean;
}

export function UploadZone({
  onFile,
  accept,
  maxSizeMB = 10,
  error,
  disabled = false,
}: UploadZoneProps) {
  const maxSize = maxSizeMB * 1024 * 1024;
  const { getRootProps, getInputProps, isDragActive, fileRejections } = useDropzone({
    onDropAccepted: (files) => {
      if (files[0]) onFile(files[0]);
    },
    accept,
    maxSize,
    multiple: false,
    disabled,
  });

  const rejectionMessage = fileRejections[0]?.errors[0]?.message;

  return (
    <div className="space-y-2">
      <div
        {...getRootProps()}
        className={`rounded-md border-2 border-dashed p-4 text-sm transition-colors ${
          isDragActive ? 'border-blue-500 bg-blue-50' : 'border-gray-300'
        } ${disabled ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'}`}
      >
        <input {...getInputProps()} />
        <p className="text-center text-muted-foreground">
          {isDragActive
            ? 'Drop your file here...'
            : `Drag a file here or click to upload (max ${maxSizeMB}MB)`}
        </p>
      </div>
      {(error || rejectionMessage) && (
        <p className="text-sm text-red-600">{error ?? rejectionMessage}</p>
      )}
    </div>
  );
}
