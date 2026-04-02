import type { EventFrequenciesDto, FrequencyLevelDto } from '../../lib/api';

interface FrequencyDisplayProps {
  frequencies: EventFrequenciesDto;
}

function FrequencyLevelSection({ level }: { level: FrequencyLevelDto }) {
  return (
    <div className="space-y-1">
      <h4 className="font-medium text-sm">{level.name}</h4>
      <div className="text-sm text-muted-foreground">
        <span className="font-medium">Primary:</span>{' '}
        {level.primary ?? 'Not set'}
      </div>
      <div className="text-sm text-muted-foreground">
        <span className="font-medium">Backup:</span>{' '}
        {level.backup ?? 'Not set'}
      </div>
    </div>
  );
}

export function FrequencyDisplay({ frequencies }: FrequencyDisplayProps) {
  const { squad, platoon, command } = frequencies;

  if (!squad && !platoon && !command) {
    return <p className="text-sm text-muted-foreground">No frequencies assigned</p>;
  }

  return (
    <div className="space-y-4">
      {squad && <FrequencyLevelSection level={squad} />}
      {platoon && <FrequencyLevelSection level={platoon} />}
      {command && <FrequencyLevelSection level={command} />}
    </div>
  );
}
