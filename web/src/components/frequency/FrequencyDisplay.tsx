import type { FrequencyLevelDto, FrequenciesDto } from '@/lib/api';

interface FrequencyRowProps {
  label: string;
  level: FrequencyLevelDto;
}

function FrequencyRow({ label, level }: FrequencyRowProps) {
  return (
    <div className="space-y-1">
      <h3 className="font-semibold text-sm text-muted-foreground uppercase tracking-wide">
        {label}: {level.name}
      </h3>
      <div className="grid grid-cols-2 gap-4 text-sm">
        <div>
          <span className="text-muted-foreground">Primary: </span>
          <span className="font-mono">{level.primary ?? '—'}</span>
        </div>
        <div>
          <span className="text-muted-foreground">Backup: </span>
          <span className="font-mono">{level.backup ?? '—'}</span>
        </div>
      </div>
    </div>
  );
}

interface FrequencyDisplayProps {
  frequencies: FrequenciesDto;
  role: string;
}

export function FrequencyDisplay({ frequencies, role }: FrequencyDisplayProps) {
  const showSquad = role === 'player' || role === 'squad_leader';
  const showPlatoon = role === 'squad_leader' || role === 'platoon_leader';
  const showCommand =
    role === 'platoon_leader' ||
    role === 'faction_commander' ||
    role === 'system_admin';
  const showAll =
    role === 'faction_commander' || role === 'system_admin';

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-bold">Radio Frequencies</h2>

      {showSquad && frequencies.squad && (
        <section data-testid="freq-squad">
          <FrequencyRow label="Squad" level={frequencies.squad} />
        </section>
      )}

      {showPlatoon && frequencies.platoon && (
        <section data-testid="freq-platoon">
          <FrequencyRow label="Platoon" level={frequencies.platoon} />
        </section>
      )}

      {showCommand && frequencies.command && (
        <section data-testid="freq-command">
          <FrequencyRow label="Command" level={frequencies.command} />
        </section>
      )}

      {showAll && frequencies.allPlatoons && frequencies.allPlatoons.length > 0 && (
        <section data-testid="freq-all-platoons">
          <h3 className="font-semibold text-sm text-muted-foreground uppercase tracking-wide mb-2">
            All Platoons
          </h3>
          <div className="space-y-3">
            {frequencies.allPlatoons.map((p) => (
              <FrequencyRow key={p.id} label="Platoon" level={p} />
            ))}
          </div>
        </section>
      )}

      {showAll && frequencies.allSquads && frequencies.allSquads.length > 0 && (
        <section data-testid="freq-all-squads">
          <h3 className="font-semibold text-sm text-muted-foreground uppercase tracking-wide mb-2">
            All Squads
          </h3>
          <div className="space-y-3">
            {frequencies.allSquads.map((s) => (
              <FrequencyRow key={s.id} label="Squad" level={s} />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
