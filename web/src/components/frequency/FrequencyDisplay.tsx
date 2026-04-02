import { useFrequencies } from '../../hooks/useFrequencies';
import type { PlatoonFrequencyDto, SquadFrequencyDto } from '../../lib/api';

interface FrequencyDisplayProps {
  eventId: string;
}

function FrequencyBand({ label, primary, backup }: { label: string; primary: string | null; backup: string | null }) {
  return (
    <div className="flex items-center justify-between rounded border px-3 py-2">
      <span className="font-medium">{label}</span>
      <div className="flex gap-4 text-sm">
        <span>Pri: {primary ?? '—'}</span>
        <span>Bkp: {backup ?? '—'}</span>
      </div>
    </div>
  );
}

export function FrequencyDisplay({ eventId }: FrequencyDisplayProps) {
  const { data, isLoading, error } = useFrequencies(eventId);

  if (isLoading) {
    return <div data-testid="frequency-loading" className="animate-pulse text-muted-foreground">Loading frequencies...</div>;
  }

  if (error) {
    return <div className="text-destructive">Failed to load frequencies.</div>;
  }

  if (!data) return null;

  return (
    <div className="space-y-4">
      {data.command && (
        <section data-testid="command-section">
          <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">Command</h3>
          <FrequencyBand label="Command Net" primary={data.command.primary} backup={data.command.backup} />
        </section>
      )}

      {data.platoons && data.platoons.length > 0 && (
        <section data-testid="platoon-section">
          <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">Platoons</h3>
          <div className="space-y-1">
            {data.platoons.map((p: PlatoonFrequencyDto) => (
              <FrequencyBand key={p.platoonId} label={p.platoonName} primary={p.primary} backup={p.backup} />
            ))}
          </div>
        </section>
      )}

      {data.squads && data.squads.length > 0 && (
        <section data-testid="squad-section">
          <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">Squads</h3>
          <div className="space-y-1">
            {data.squads.map((s: SquadFrequencyDto) => (
              <FrequencyBand key={s.squadId} label={s.squadName} primary={s.primary} backup={s.backup} />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
