import type { FrequencyVisibilityDto } from '../../lib/api';

interface FrequencyDisplayProps {
  frequencies: FrequencyVisibilityDto;
}

function FrequencyRow({ label, value }: { label: string; value: string | null }) {
  return (
    <div className="flex justify-between text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-mono">{value ?? 'no frequency set'}</span>
    </div>
  );
}

export function FrequencyDisplay({ frequencies }: FrequencyDisplayProps) {
  const { squad, platoon, command } = frequencies;

  return (
    <div className="space-y-4">
      {squad && (
        <section aria-label="Squad Frequencies">
          <h3 className="text-sm font-semibold mb-1">{squad.squadName}</h3>
          <FrequencyRow label="Primary" value={squad.primary} />
          <FrequencyRow label="Backup" value={squad.backup} />
        </section>
      )}
      {platoon && (
        <section aria-label="Platoon Frequencies">
          <h3 className="text-sm font-semibold mb-1">{platoon.platoonName}</h3>
          <FrequencyRow label="Primary" value={platoon.primary} />
          <FrequencyRow label="Backup" value={platoon.backup} />
        </section>
      )}
      {command && (
        <section aria-label="Command Frequencies">
          <h3 className="text-sm font-semibold mb-1">Command</h3>
          <FrequencyRow label="Primary" value={command.primary} />
          <FrequencyRow label="Backup" value={command.backup} />
        </section>
      )}
      {!squad && !platoon && !command && (
        <p className="text-sm text-muted-foreground">No frequency data available.</p>
      )}
    </div>
  );
}
