import { useState } from 'react';
import type { UpdateFrequencyRequest } from '../../lib/api';

interface FrequencyEditorProps {
  label: string;
  initialPrimary: string | null;
  initialBackup: string | null;
  onSave: (request: UpdateFrequencyRequest) => void;
  isPending: boolean;
  error: Error | null;
}

export function FrequencyEditor({ label, initialPrimary, initialBackup, onSave, isPending, error }: FrequencyEditorProps) {
  const [primary, setPrimary] = useState(initialPrimary ?? '');
  const [backup, setBackup] = useState(initialBackup ?? '');

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    onSave({
      primary: primary.trim() || null,
      backup: backup.trim() || null,
    });
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3 rounded border p-4">
      <h3 className="font-semibold">{label}</h3>

      <div className="flex gap-4">
        <label className="flex flex-col gap-1 text-sm">
          Primary
          <input
            type="text"
            data-testid="primary-input"
            value={primary}
            onChange={(e) => setPrimary(e.target.value)}
            placeholder="e.g. 145.500"
            className="rounded border px-2 py-1"
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          Backup
          <input
            type="text"
            data-testid="backup-input"
            value={backup}
            onChange={(e) => setBackup(e.target.value)}
            placeholder="e.g. 145.600"
            className="rounded border px-2 py-1"
          />
        </label>
      </div>

      {error && (
        <p data-testid="editor-error" className="text-sm text-destructive">{error.message}</p>
      )}

      <button
        type="submit"
        disabled={isPending}
        className="rounded bg-primary px-3 py-1 text-sm text-primary-foreground disabled:opacity-50"
      >
        {isPending ? 'Saving...' : 'Save'}
      </button>
    </form>
  );
}
