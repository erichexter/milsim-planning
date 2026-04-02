import { useFrequencies } from '../../hooks/useFrequencies';
import { Card, CardHeader, CardTitle, CardContent } from '../ui/card';

interface FrequencyPairProps {
  primary: string | null;
  backup: string | null;
}

function FrequencyPair({ primary, backup }: FrequencyPairProps) {
  return (
    <div className="grid grid-cols-2 gap-2 text-sm">
      <div>
        <span className="text-muted-foreground">Primary:</span>{' '}
        <span className="font-mono">{primary ?? '—'}</span>
      </div>
      <div>
        <span className="text-muted-foreground">Backup:</span>{' '}
        <span className="font-mono">{backup ?? '—'}</span>
      </div>
    </div>
  );
}

interface FrequencyDisplayProps {
  eventId: string;
}

export function FrequencyDisplay({ eventId }: FrequencyDisplayProps) {
  const { data, isLoading, error } = useFrequencies(eventId);

  if (isLoading) {
    return <div className="text-sm text-muted-foreground">Loading frequencies...</div>;
  }

  if (error) {
    return <div className="text-sm text-destructive">Failed to load frequencies.</div>;
  }

  if (!data) return null;

  const hasCommand = data.command !== null;
  const hasPlatoons = data.platoons.length > 0;
  const hasSquads = data.squads.length > 0;

  if (!hasCommand && !hasPlatoons && !hasSquads) {
    return <div className="text-sm text-muted-foreground">No frequencies assigned.</div>;
  }

  return (
    <div className="space-y-4">
      {hasCommand && data.command && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Command</CardTitle>
          </CardHeader>
          <CardContent>
            <FrequencyPair primary={data.command.primary} backup={data.command.backup} />
          </CardContent>
        </Card>
      )}

      {hasPlatoons && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Platoons</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {data.platoons.map((p) => (
              <div key={p.platoonId}>
                <div className="text-sm font-medium mb-1">{p.platoonName}</div>
                <FrequencyPair primary={p.primary} backup={p.backup} />
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {hasSquads && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Squads</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {data.squads.map((s) => (
              <div key={s.squadId}>
                <div className="text-sm font-medium mb-1">{s.squadName}</div>
                <FrequencyPair primary={s.primary} backup={s.backup} />
              </div>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
