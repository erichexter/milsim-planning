import { useState } from 'react';
import type { FrequencyVisibilityDto, UpdateFrequencyRequest } from '../../lib/api';

interface FrequencyEditorProps {
  frequencies: FrequencyVisibilityDto;
  factionId: string;
  onUpdateSquad?: (squadId: string, req: UpdateFrequencyRequest) => Promise<void>;
  onUpdatePlatoon?: (platoonId: string, req: UpdateFrequencyRequest) => Promise<void>;
  onUpdateFaction?: (factionId: string, req: UpdateFrequencyRequest) => Promise<void>;
}

interface FrequencyFieldsProps {
  label: string;
  primary: string | null;
  backup: string | null;
  onSave: (primary: string | null, backup: string | null) => Promise<void>;
}

function FrequencyFields({ label, primary, backup, onSave }: FrequencyFieldsProps) {
  const [primaryVal, setPrimary] = useState(primary ?? '');
  const [backupVal, setBackup] = useState(backup ?? '');
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    setSaving(true);
    try {
      await onSave(primaryVal || null, backupVal || null);
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="space-y-2 border rounded p-3">
      <h3 className="text-sm font-semibold">{label}</h3>
      <div className="flex gap-2 items-center">
        <label className="text-xs w-16 shrink-0">Primary</label>
        <input
          type="text"
          value={primaryVal}
          onChange={e => setPrimary(e.target.value)}
          placeholder="e.g. 43.325"
          className="flex-1 text-sm border rounded px-2 py-1"
          aria-label={`${label} primary frequency`}
        />
      </div>
      <div className="flex gap-2 items-center">
        <label className="text-xs w-16 shrink-0">Backup</label>
        <input
          type="text"
          value={backupVal}
          onChange={e => setBackup(e.target.value)}
          placeholder="e.g. 44.000"
          className="flex-1 text-sm border rounded px-2 py-1"
          aria-label={`${label} backup frequency`}
        />
      </div>
      <button
        onClick={handleSave}
        disabled={saving}
        className="text-xs px-3 py-1 bg-primary text-primary-foreground rounded disabled:opacity-50"
      >
        {saving ? 'Saving…' : 'Save'}
      </button>
    </section>
  );
}

export function FrequencyEditor({
  frequencies,
  factionId,
  onUpdateSquad,
  onUpdatePlatoon,
  onUpdateFaction,
}: FrequencyEditorProps) {
  const { squad, platoon, command } = frequencies;

  return (
    <div className="space-y-4">
      {squad && onUpdateSquad && (
        <FrequencyFields
          label={squad.squadName}
          primary={squad.primary}
          backup={squad.backup}
          onSave={(primary, backup) => onUpdateSquad(squad.squadId, { primary, backup })}
        />
      )}
      {platoon && onUpdatePlatoon && (
        <FrequencyFields
          label={platoon.platoonName}
          primary={platoon.primary}
          backup={platoon.backup}
          onSave={(primary, backup) => onUpdatePlatoon(platoon.platoonId, { primary, backup })}
        />
      )}
      {command && onUpdateFaction && (
        <FrequencyFields
          label="Command"
          primary={command.primary}
          backup={command.backup}
          onSave={(primary, backup) => onUpdateFaction(factionId, { primary, backup })}
        />
      )}
    </div>
  );
}
