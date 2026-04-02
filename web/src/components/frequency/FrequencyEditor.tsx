import { useState } from 'react';
import { UseMutationResult } from '@tanstack/react-query';
import { Button } from '../ui/button';
import { type UpdateFrequencyRequest } from '../../lib/api';

interface Props {
  label: string;
  currentPrimary: string | null;
  currentBackup: string | null;
  mutation: UseMutationResult<void, Error, UpdateFrequencyRequest>;
}

export function FrequencyEditor({ label, currentPrimary, currentBackup, mutation }: Props) {
  const [editing, setEditing] = useState(false);
  const [primary, setPrimary] = useState(currentPrimary ?? '');
  const [backup, setBackup] = useState(currentBackup ?? '');

  function handleOpen() {
    setPrimary(currentPrimary ?? '');
    setBackup(currentBackup ?? '');
    setEditing(true);
  }

  function handleCancel() {
    setEditing(false);
  }

  function handleSave() {
    mutation.mutate(
      { primary: primary.trim() || null, backup: backup.trim() || null },
      { onSuccess: () => setEditing(false) },
    );
  }

  if (!editing) {
    return (
      <Button
        variant="ghost"
        size="sm"
        className="h-auto py-0 px-1 text-xs text-muted-foreground hover:text-foreground"
        onClick={handleOpen}
        aria-label={`Edit ${label} frequency`}
      >
        Edit
      </Button>
    );
  }

  return (
    <div className="flex flex-col gap-2 pt-1">
      <div className="flex items-center gap-2">
        <label className="rp0-label w-14 shrink-0">Primary</label>
        <input
          type="text"
          value={primary}
          onChange={(e) => setPrimary(e.target.value)}
          placeholder="e.g. 45.500 MHz"
          className="flex-1 rounded border bg-background px-2 py-1 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </div>
      <div className="flex items-center gap-2">
        <label className="rp0-label w-14 shrink-0">Backup</label>
        <input
          type="text"
          value={backup}
          onChange={(e) => setBackup(e.target.value)}
          placeholder="e.g. 46.750 MHz"
          className="flex-1 rounded border bg-background px-2 py-1 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </div>
      <div className="flex gap-2">
        <Button
          size="sm"
          onClick={handleSave}
          disabled={mutation.isPending}
          className="text-xs h-7"
        >
          {mutation.isPending ? 'Saving…' : 'Save'}
        </Button>
        <Button
          variant="ghost"
          size="sm"
          onClick={handleCancel}
          disabled={mutation.isPending}
          className="text-xs h-7"
        >
          Cancel
        </Button>
      </div>
      {mutation.isError && (
        <p className="text-xs text-destructive">{mutation.error.message}</p>
      )}
    </div>
  );
}
