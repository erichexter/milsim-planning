import { useQuery } from '@tanstack/react-query';
import { api, type EventFrequenciesDto, type FrequencyLevelDto } from '../lib/api';

interface Props {
  eventId: string;
}

function FrequencyRow({ label, freq }: { label: string; freq: FrequencyLevelDto | null }) {
  if (!freq) return null;

  return (
    <div className="mb-3">
      <p className="text-sm font-semibold text-muted-foreground uppercase tracking-wide mb-1">{label}</p>
      <div className="flex gap-4">
        <div>
          <span className="text-xs text-muted-foreground">Primary: </span>
          <span className="font-mono text-sm">{freq.primary ?? <em className="text-muted-foreground">not set</em>}</span>
        </div>
        <div>
          <span className="text-xs text-muted-foreground">Backup: </span>
          <span className="font-mono text-sm">{freq.backup ?? <em className="text-muted-foreground">not set</em>}</span>
        </div>
      </div>
    </div>
  );
}

function hasAnyFrequency(dto: EventFrequenciesDto): boolean {
  return dto.squad !== null || dto.platoon !== null || dto.command !== null;
}

export function EventFrequencies({ eventId }: Props) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['frequencies', eventId],
    queryFn: () => api.getEventFrequencies(eventId),
  });

  if (isLoading) {
    return <p className="text-sm text-muted-foreground">Loading frequencies…</p>;
  }

  if (isError || !data) {
    return <p className="text-sm text-destructive">Failed to load frequencies.</p>;
  }

  if (!hasAnyFrequency(data)) {
    return <p className="text-sm text-muted-foreground">No frequencies assigned for your role.</p>;
  }

  return (
    <div data-testid="event-frequencies">
      <FrequencyRow label="Squad" freq={data.squad} />
      <FrequencyRow label="Platoon" freq={data.platoon} />
      <FrequencyRow label="Command" freq={data.command} />
    </div>
  );
}
