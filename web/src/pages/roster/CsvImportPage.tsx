import { useState, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router';
import { useDropzone } from 'react-dropzone';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api, type CsvValidationResult } from '../../lib/api';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { EventBreadcrumb } from '../../components/EventBreadcrumb';

export function CsvImportPage() {
  const { id: eventId } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [file, setFile] = useState<File | null>(null);
  const [validation, setValidation] = useState<CsvValidationResult | null>(null);
  const [committed, setCommitted] = useState(false);
  const queryClient = useQueryClient();

  const validateMutation = useMutation({
    mutationFn: (f: File) => api.validateRoster(eventId!, f),
    onSuccess: (result) => setValidation(result),
  });

  const commitMutation = useMutation({
    mutationFn: () => api.commitRoster(eventId!, file!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['events'] });
      void navigate(`/events/${eventId}/hierarchy`);
    },
  });

  const onDrop = useCallback(
    (accepted: File[]) => {
      const f = accepted[0];
      if (!f) return;
      setFile(f);
      setCommitted(false);
      setValidation(null);
      validateMutation.mutate(f);
    },
    [validateMutation]
  );

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    accept: { 'text/csv': ['.csv'] },
    multiple: false,
  });

  const hasErrors = (validation?.errorCount ?? 0) > 0;

  return (
    <div className="p-6 max-w-3xl mx-auto space-y-6">
      <EventBreadcrumb eventId={eventId!} page="Import Roster" />
      <h1 className="text-2xl font-bold">Import Roster</h1>

      <div
        {...getRootProps()}
        className={`border-2 border-dashed rounded-lg p-8 text-center cursor-pointer transition-colors ${
          isDragActive ? 'border-primary bg-primary/5' : 'border-muted-foreground/30'
        }`}
      >
        <input {...getInputProps()} />
        {file ? (
          <p className="text-sm font-medium">{file.name}</p>
        ) : (
          <p className="text-sm text-muted-foreground">
            Drag a CSV file here, or click to select
          </p>
        )}
      </div>

      {validateMutation.isPending && (
        <p className="text-sm text-muted-foreground">Validating…</p>
      )}

      {validation?.fatalError && (
        <div className="rounded-md bg-destructive/10 p-4 text-destructive text-sm">
          {validation.fatalError}
        </div>
      )}

      {validation && !validation.fatalError && (
        <div className="space-y-4">
          {/* Summary line — errors-only preview design (ROST-02) */}
          <p className="text-sm font-medium">
            <span className="text-green-600">{validation.validCount} valid</span>
            {' · '}
            <span className={validation.errorCount > 0 ? 'text-destructive' : ''}>
              {validation.errorCount} errors
            </span>
            {' · '}
            <span className={validation.warningCount > 0 ? 'text-yellow-600' : ''}>
              {validation.warningCount} warnings
            </span>
          </p>

          {/* Error-only table: only show problematic rows */}
          {validation.errors.length > 0 && (
            <div className="border rounded-md overflow-hidden">
              <table className="w-full text-sm">
                <thead className="bg-muted">
                  <tr>
                    <th className="p-2 text-left">Row</th>
                    <th className="p-2 text-left">Field</th>
                    <th className="p-2 text-left">Issue</th>
                    <th className="p-2 text-left">Severity</th>
                  </tr>
                </thead>
                <tbody>
                  {validation.errors.map((err, idx) => (
                    <tr
                      key={idx}
                      className={err.severity === 'Error' ? 'bg-destructive/5' : 'bg-yellow-50'}
                    >
                      <td className="p-2">{err.row}</td>
                      <td className="p-2 font-mono text-xs">{err.field}</td>
                      <td className="p-2">{err.message}</td>
                      <td className="p-2">
                        <Badge variant={err.severity === 'Error' ? 'destructive' : 'outline'}>
                          {err.severity}
                        </Badge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <Button
            onClick={() => commitMutation.mutate()}
            disabled={hasErrors || commitMutation.isPending || !file}
          >
            {commitMutation.isPending
              ? 'Importing…'
              : hasErrors
                ? 'Fix errors to import'
                : `Import ${validation.validCount} players`}
          </Button>
        </div>
      )}

      {committed && (
        <div className="rounded-md bg-green-50 p-4 text-green-700 text-sm">
          Roster imported successfully.
        </div>
      )}
    </div>
  );
}
