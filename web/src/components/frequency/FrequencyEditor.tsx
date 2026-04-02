import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { FrequencyLevelDto, UpdateFrequencyRequest } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

type FrequencyScope = 'squad' | 'platoon' | 'faction';

interface FrequencyEditorProps {
  eventId: string;
  scope: FrequencyScope;
  entityId: string;
  level: FrequencyLevelDto;
  role: string;
}

function canEdit(scope: FrequencyScope, role: string): boolean {
  if (role === 'system_admin' || role === 'faction_commander') return true;
  if (scope === 'squad' && (role === 'squad_leader' || role === 'platoon_leader')) return true;
  if (scope === 'platoon' && role === 'platoon_leader') return true;
  return false;
}

export function FrequencyEditor({
  eventId,
  scope,
  entityId,
  level,
  role,
}: FrequencyEditorProps) {
  const queryClient = useQueryClient();
  const [primary, setPrimary] = useState(level.primary ?? '');
  const [backup, setBackup] = useState(level.backup ?? '');
  const [editing, setEditing] = useState(false);

  const mutation = useMutation({
    mutationFn: (req: UpdateFrequencyRequest) => {
      if (scope === 'squad') return api.patchSquadFrequencies(entityId, req);
      if (scope === 'platoon') return api.patchPlatoonFrequencies(entityId, req);
      return api.patchFactionFrequencies(entityId, req);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['frequencies', eventId] });
      setEditing(false);
    },
  });

  if (!canEdit(scope, role)) return null;

  const scopeLabel = scope.charAt(0).toUpperCase() + scope.slice(1);

  if (!editing) {
    return (
      <Button
        variant="outline"
        size="sm"
        onClick={() => setEditing(true)}
        data-testid={`freq-edit-btn-${scope}`}
      >
        Edit {scopeLabel} Frequencies
      </Button>
    );
  }

  return (
    <form
      data-testid={`freq-editor-${scope}`}
      onSubmit={(e) => {
        e.preventDefault();
        mutation.mutate({
          primary: primary.trim() || null,
          backup: backup.trim() || null,
        });
      }}
      className="space-y-3 border rounded p-4"
    >
      <h4 className="font-semibold text-sm">Edit {scopeLabel} Frequencies</h4>

      <div className="space-y-1">
        <Label htmlFor={`freq-primary-${scope}`}>Primary</Label>
        <Input
          id={`freq-primary-${scope}`}
          value={primary}
          onChange={(e) => setPrimary(e.target.value)}
          placeholder="e.g. 148.500"
        />
      </div>

      <div className="space-y-1">
        <Label htmlFor={`freq-backup-${scope}`}>Backup</Label>
        <Input
          id={`freq-backup-${scope}`}
          value={backup}
          onChange={(e) => setBackup(e.target.value)}
          placeholder="e.g. 148.550"
        />
      </div>

      {mutation.isError && (
        <p className="text-sm text-destructive">
          Failed to save. Please try again.
        </p>
      )}

      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={mutation.isPending}>
          {mutation.isPending ? 'Saving…' : 'Save'}
        </Button>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => setEditing(false)}
        >
          Cancel
        </Button>
      </div>
    </form>
  );
}
