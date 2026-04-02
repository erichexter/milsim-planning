import { useFrequencies } from '../../hooks/useFrequencies';
import type { FrequencyPairDto } from '../../lib/api';

interface Props {
  eventId: string;
  canEdit?: boolean;
  onEdit?: (level: 'squad' | 'platoon' | 'command', id: string | null, current: FrequencyPairDto) => void;
}

function FrequencyPair({ label, primary, backup }: { label: string; primary: string | null; backup: string | null }) {
  return (
    <div className="flex items-center gap-4 py-2">
      <span className="text-sm font-medium w-40 shrink-0">{label}</span>
      <div className="flex gap-4 text-sm">
        <div>
          <span className="text-muted-foreground">Primary: </span>
          <span className="font-mono">{primary ?? <em className="text-muted-foreground">not set</em>}</span>
        </div>
        <div>
          <span className="text-muted-foreground">Backup: </span>
          <span className="font-mono">{backup ?? <em className="text-muted-foreground">not set</em>}</span>
        </div>
      </div>
    </div>
  );
}

export function FrequencyDisplay({ eventId, canEdit, onEdit }: Props) {
  const { data, isLoading, error } = useFrequencies(eventId);

  if (isLoading) {
    return <p className="text-sm text-muted-foreground">Loading frequencies…</p>;
  }

  if (error || !data) {
    return <p className="text-sm text-destructive">Failed to load frequencies.</p>;
  }

  const hasAny = data.command !== null || data.platoons.length > 0 || data.squads.length > 0;

  if (!hasAny) {
    return <p className="text-sm text-muted-foreground">No frequencies assigned for your role.</p>;
  }

  return (
    <div data-testid="frequency-display" className="space-y-1">
      {data.command && (
        <div className="mb-4">
          <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">Command</h4>
          <div className="flex items-center gap-2">
            <FrequencyPair label="Command Net" primary={data.command.primary} backup={data.command.backup} />
            {canEdit && onEdit && (
              <button
                type="button"
                className="text-xs text-primary underline ml-2"
                onClick={() => onEdit('command', null, data.command!)}
              >
                Edit
              </button>
            )}
          </div>
        </div>
      )}

      {data.platoons.length > 0 && (
        <div className="mb-4">
          <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">Platoons</h4>
          {data.platoons.map((pl) => (
            <div key={pl.platoonId} className="flex items-center gap-2">
              <FrequencyPair label={pl.name} primary={pl.primary} backup={pl.backup} />
              {canEdit && onEdit && (
                <button
                  type="button"
                  className="text-xs text-primary underline ml-2"
                  onClick={() => onEdit('platoon', pl.platoonId, { primary: pl.primary, backup: pl.backup })}
                >
                  Edit
                </button>
              )}
            </div>
          ))}
        </div>
      )}

      {data.squads.length > 0 && (
        <div className="mb-4">
          <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">Squads</h4>
          {data.squads.map((sq) => (
            <div key={sq.squadId} className="flex items-center gap-2">
              <FrequencyPair label={sq.name} primary={sq.primary} backup={sq.backup} />
              {canEdit && onEdit && (
                <button
                  type="button"
                  className="text-xs text-primary underline ml-2"
                  onClick={() => onEdit('squad', sq.squadId, { primary: sq.primary, backup: sq.backup })}
                >
                  Edit
                </button>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
