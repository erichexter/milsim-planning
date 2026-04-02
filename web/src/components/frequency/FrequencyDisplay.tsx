import { useState } from 'react';
import { FrequencyViewDto, FrequencyLevelDto } from '../../lib/api';
import { FrequencyEditForm } from './FrequencyEditForm';
import { Button } from '../ui/button';

interface FrequencyDisplayProps {
  data: FrequencyViewDto;
  role: string;
  onRefetch?: () => void;
  /** Additional squad rows for platoon leaders to edit (squads are null in their frequency view). */
  editableSquads?: FrequencyLevelDto[];
}

function canEditCommand(role: string): boolean {
  return role === 'faction_commander' || role === 'system_admin';
}

function canEditPlatoon(role: string): boolean {
  return role === 'platoon_leader' || role === 'faction_commander' || role === 'system_admin';
}

function canEditSquad(role: string): boolean {
  return role === 'squad_leader' || role === 'platoon_leader' || role === 'faction_commander' || role === 'system_admin';
}

interface FrequencyRowProps {
  level: FrequencyLevelDto;
  levelType: 'squad' | 'platoon' | 'command';
  showEdit: boolean;
  onRefetch?: () => void;
}

function FrequencyRow({ level, levelType, showEdit, onRefetch }: FrequencyRowProps) {
  const [editing, setEditing] = useState(false);

  return (
    <div className="rounded-lg border bg-card p-4 space-y-2">
      <div className="flex items-center justify-between">
        <h4 className="font-medium text-sm">{level.name}</h4>
        {showEdit && !editing && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => setEditing(true)}
            aria-label={`Edit ${level.name} frequencies`}
          >
            Edit
          </Button>
        )}
      </div>
      {editing ? (
        <FrequencyEditForm
          levelId={level.id}
          levelType={levelType}
          levelName={level.name}
          onSuccess={() => {
            setEditing(false);
            onRefetch?.();
          }}
        />
      ) : (
        <div className="space-y-1 text-sm">
          <div className="flex gap-2">
            <span className="text-muted-foreground w-16 shrink-0">Primary:</span>
            <span>{level.primary ?? 'Not set'}</span>
          </div>
          <div className="flex gap-2">
            <span className="text-muted-foreground w-16 shrink-0">Backup:</span>
            <span>{level.backup ?? 'Not set'}</span>
          </div>
        </div>
      )}
    </div>
  );
}

export function FrequencyDisplay({ data, role, onRefetch, editableSquads }: FrequencyDisplayProps) {
  // For platoon leaders: use editableSquads (fetched separately) since data.squads is null.
  // For other roles: use data.squads directly.
  const squadRows = data.squads ?? (editableSquads && editableSquads.length > 0 ? editableSquads : null);
  const squadRowsShowEdit = data.squads !== null ? canEditSquad(role) : true;

  return (
    <div className="space-y-6">
      {data.command !== null && (
        <section aria-label="Command Frequencies">
          <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground mb-3">
            Command Frequencies
          </h3>
          <FrequencyRow
            level={data.command}
            levelType="command"
            showEdit={canEditCommand(role)}
            onRefetch={onRefetch}
          />
        </section>
      )}

      {data.platoons !== null && (
        <section aria-label="Platoon Frequencies">
          <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground mb-3">
            Platoon Frequencies
          </h3>
          <div className="space-y-3">
            {data.platoons.map((platoon) => (
              <FrequencyRow
                key={platoon.id}
                level={platoon}
                levelType="platoon"
                showEdit={canEditPlatoon(role)}
                onRefetch={onRefetch}
              />
            ))}
          </div>
        </section>
      )}

      {squadRows !== null && (
        <section aria-label="Squad Frequencies">
          <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground mb-3">
            Squad Frequencies
          </h3>
          <div className="space-y-3">
            {squadRows.map((squad) => (
              <FrequencyRow
                key={squad.id}
                level={squad}
                levelType="squad"
                showEdit={squadRowsShowEdit}
                onRefetch={onRefetch}
              />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
