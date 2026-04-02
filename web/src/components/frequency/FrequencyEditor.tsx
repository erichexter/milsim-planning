import { useState } from 'react';
import type { FrequencyPairDto, UpdateFrequencyRequest } from '../../lib/api';
import type { UseMutationResult } from '@tanstack/react-query';

interface Props {
  level: 'squad' | 'platoon' | 'command';
  targetId: string | null;
  initial: FrequencyPairDto;
  mutation: UseMutationResult<void, Error, UpdateFrequencyRequest> | UseMutationResult<void, Error, { squadId: string; req: UpdateFrequencyRequest }> | UseMutationResult<void, Error, { platoonId: string; req: UpdateFrequencyRequest }>;
  onClose: () => void;
}

export function FrequencyEditor({ level, targetId, initial, mutation, onClose }: Props) {
  const [primary, setPrimary] = useState(initial.primary ?? '');
  const [backup, setBackup] = useState(initial.backup ?? '');

  const handleSave = () => {
    const req: UpdateFrequencyRequest = {
      primary: primary.trim() || null,
      backup: backup.trim() || null,
    };

    if (level === 'command') {
      (mutation as UseMutationResult<void, Error, UpdateFrequencyRequest>).mutate(req, {
        onSuccess: onClose,
      });
    } else if (level === 'squad') {
      (mutation as UseMutationResult<void, Error, { squadId: string; req: UpdateFrequencyRequest }>).mutate(
        { squadId: targetId!, req },
        { onSuccess: onClose }
      );
    } else {
      (mutation as UseMutationResult<void, Error, { platoonId: string; req: UpdateFrequencyRequest }>).mutate(
        { platoonId: targetId!, req },
        { onSuccess: onClose }
      );
    }
  };

  return (
    <div data-testid="frequency-editor" className="border rounded p-3 bg-muted/50 space-y-3">
      <h4 className="text-sm font-semibold capitalize">{level} Frequency Editor</h4>
      <div className="flex gap-3">
        <label className="flex flex-col gap-1 text-sm">
          Primary
          <input
            type="text"
            value={primary}
            onChange={(e) => setPrimary(e.target.value)}
            placeholder="e.g. 155.000"
            className="border rounded px-2 py-1 text-sm font-mono w-32"
            data-testid="frequency-primary-input"
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          Backup
          <input
            type="text"
            value={backup}
            onChange={(e) => setBackup(e.target.value)}
            placeholder="e.g. 155.500"
            className="border rounded px-2 py-1 text-sm font-mono w-32"
            data-testid="frequency-backup-input"
          />
        </label>
      </div>
      <div className="flex gap-2">
        <button
          type="button"
          onClick={handleSave}
          disabled={mutation.isPending}
          className="px-3 py-1 text-sm bg-primary text-primary-foreground rounded disabled:opacity-50"
          data-testid="frequency-save-btn"
        >
          {mutation.isPending ? 'Saving…' : 'Save'}
        </button>
        <button
          type="button"
          onClick={onClose}
          className="px-3 py-1 text-sm border rounded"
          data-testid="frequency-cancel-btn"
        >
          Cancel
        </button>
      </div>
      {mutation.isError && (
        <p className="text-sm text-destructive">Failed to save frequency.</p>
      )}
    </div>
  );
}
