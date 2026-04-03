import { useState } from 'react';
import { useParams } from 'react-router';
import {
  useFrequencies,
  useUpdateSquadFrequencies,
  useUpdatePlatoonFrequencies,
  useUpdateCommandFrequencies,
} from '../hooks/useFrequencies';
import { useAuth } from '../hooks/useAuth';
import type {
  FrequencyReadDto,
  SquadFrequencyDto,
  PlatoonFrequencyDto,
  CommandFrequencyDto,
  AllFrequenciesSquadDto,
} from '../lib/api';

interface FrequencyEditFormProps {
  label: string;
  primary: string | null;
  backup: string | null;
  onSave: (primary: string | null, backup: string | null) => void;
  isSaving: boolean;
}

function FrequencyEditForm({ label, primary, backup, onSave, isSaving }: FrequencyEditFormProps) {
  const [editPrimary, setEditPrimary] = useState(primary ?? '');
  const [editBackup, setEditBackup] = useState(backup ?? '');
  const [isEditing, setIsEditing] = useState(false);

  if (!isEditing) {
    return (
      <button
        className="text-xs text-blue-600 hover:underline"
        onClick={() => setIsEditing(true)}
      >
        Edit
      </button>
    );
  }

  return (
    <div className="mt-2 space-y-1" data-testid={`edit-form-${label}`}>
      <input
        className="block w-full border rounded px-2 py-1 text-sm"
        placeholder="Primary frequency"
        value={editPrimary}
        onChange={(e) => setEditPrimary(e.target.value)}
        data-testid={`input-primary-${label}`}
      />
      <input
        className="block w-full border rounded px-2 py-1 text-sm"
        placeholder="Backup frequency"
        value={editBackup}
        onChange={(e) => setEditBackup(e.target.value)}
        data-testid={`input-backup-${label}`}
      />
      <div className="flex gap-2">
        <button
          className="text-xs bg-blue-600 text-white px-2 py-1 rounded"
          disabled={isSaving}
          onClick={() => {
            onSave(editPrimary || null, editBackup || null);
            setIsEditing(false);
          }}
        >
          Save
        </button>
        <button
          className="text-xs text-gray-500 hover:underline"
          onClick={() => setIsEditing(false)}
        >
          Cancel
        </button>
      </div>
    </div>
  );
}

function FrequencyRow({ label, primary, backup }: { label: string; primary: string | null; backup: string | null }) {
  return (
    <div className="flex justify-between items-center py-1" data-testid={`freq-row-${label}`}>
      <span className="text-sm font-medium">{label}</span>
      <span className="text-sm text-gray-600">
        {primary ?? '—'}{backup ? ` / ${backup}` : ''}
      </span>
    </div>
  );
}

export function FrequencyDisplay() {
  const { id: eventId } = useParams<{ id: string }>();
  const { user } = useAuth();
  const { data, isLoading, error } = useFrequencies(eventId!);

  const updateSquad = useUpdateSquadFrequencies(eventId!);
  const updatePlatoon = useUpdatePlatoonFrequencies(eventId!);
  const updateCommand = useUpdateCommandFrequencies(eventId!);

  if (isLoading) return <div data-testid="freq-loading">Loading frequencies...</div>;
  if (error) return <div data-testid="freq-error">Failed to load frequencies.</div>;
  if (!data) return null;

  const role = user?.role;
  const canEditSquad = role === 'squad_leader' || role === 'platoon_leader' || role === 'faction_commander' || role === 'system_admin';
  const canEditPlatoon = role === 'platoon_leader' || role === 'faction_commander' || role === 'system_admin';
  const canEditCommand = role === 'faction_commander' || role === 'system_admin';

  return (
    <div className="space-y-4" data-testid="frequency-display">
      {data.squad && (
        <div data-testid="freq-squad">
          <h3 className="font-semibold text-sm mb-1">Squad: {data.squad.squadName}</h3>
          <FrequencyRow label="squad" primary={data.squad.primary} backup={data.squad.backup} />
          {canEditSquad && (
            <FrequencyEditForm
              label="squad"
              primary={data.squad.primary}
              backup={data.squad.backup}
              isSaving={updateSquad.isPending}
              onSave={(primary, backup) =>
                updateSquad.mutate({ squadId: data.squad!.squadId, body: { primary, backup } })
              }
            />
          )}
        </div>
      )}

      {data.platoon && (
        <div data-testid="freq-platoon">
          <h3 className="font-semibold text-sm mb-1">Platoon: {data.platoon.platoonName}</h3>
          <FrequencyRow label="platoon" primary={data.platoon.primary} backup={data.platoon.backup} />
          {canEditPlatoon && (
            <FrequencyEditForm
              label="platoon"
              primary={data.platoon.primary}
              backup={data.platoon.backup}
              isSaving={updatePlatoon.isPending}
              onSave={(primary, backup) =>
                updatePlatoon.mutate({ platoonId: data.platoon!.platoonId, body: { primary, backup } })
              }
            />
          )}
        </div>
      )}

      {data.command && (
        <div data-testid="freq-command">
          <h3 className="font-semibold text-sm mb-1">Command: {data.command.factionName}</h3>
          <FrequencyRow label="command" primary={data.command.primary} backup={data.command.backup} />
          {canEditCommand && (
            <FrequencyEditForm
              label="command"
              primary={data.command.primary}
              backup={data.command.backup}
              isSaving={updateCommand.isPending}
              onSave={(primary, backup) =>
                updateCommand.mutate({ factionId: data.command!.factionId, body: { primary, backup } })
              }
            />
          )}
        </div>
      )}

      {data.allFrequencies && (
        <div data-testid="freq-all">
          <h3 className="font-semibold text-sm mb-2">All Frequencies Overview</h3>
          <div className="space-y-2">
            <div>
              <span className="text-xs font-semibold uppercase text-gray-500">Command</span>
              <FrequencyRow
                label="all-command"
                primary={data.allFrequencies.command.primary}
                backup={data.allFrequencies.command.backup}
              />
            </div>
            {data.allFrequencies.platoons.map((p) => (
              <div key={p.platoonId}>
                <span className="text-xs font-semibold uppercase text-gray-500">Platoon: {p.platoonName}</span>
                <FrequencyRow label={`all-platoon-${p.platoonId}`} primary={p.primary} backup={p.backup} />
                {canEditPlatoon && (
                  <FrequencyEditForm
                    label={`all-platoon-${p.platoonId}`}
                    primary={p.primary}
                    backup={p.backup}
                    isSaving={updatePlatoon.isPending}
                    onSave={(primary, backup) =>
                      updatePlatoon.mutate({ platoonId: p.platoonId, body: { primary, backup } })
                    }
                  />
                )}
              </div>
            ))}
            {data.allFrequencies.squads.map((s) => (
              <div key={s.squadId}>
                <span className="text-xs font-semibold uppercase text-gray-500">
                  Squad: {s.squadName} ({s.platoonName})
                </span>
                <FrequencyRow label={`all-squad-${s.squadId}`} primary={s.primary} backup={s.backup} />
                {canEditSquad && (
                  <FrequencyEditForm
                    label={`all-squad-${s.squadId}`}
                    primary={s.primary}
                    backup={s.backup}
                    isSaving={updateSquad.isPending}
                    onSave={(primary, backup) =>
                      updateSquad.mutate({ squadId: s.squadId, body: { primary, backup } })
                    }
                  />
                )}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
