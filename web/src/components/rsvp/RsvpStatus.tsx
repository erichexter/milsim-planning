import { useRsvp } from '../../hooks/useRsvp';
import { Button } from '../ui/button';
import type { RsvpStatus as RsvpStatusType } from '../../lib/api';

const STATUS_OPTIONS: { value: RsvpStatusType; label: string }[] = [
  { value: 'Attending', label: 'Attending' },
  { value: 'Maybe', label: 'Maybe' },
  { value: 'NotAttending', label: 'Not Attending' },
];

export function RsvpStatus({ eventId }: { eventId: string }) {
  const { rsvp, isLoading, setRsvp, isUpdating } = useRsvp(eventId);

  if (isLoading) return <div className="text-sm text-muted-foreground">Loading RSVP...</div>;

  const currentStatus = rsvp?.status ?? null;

  return (
    <div className="border rounded-lg p-4 space-y-3">
      <p className="text-sm font-semibold">RSVP</p>
      {currentStatus && (
        <p className="text-sm text-muted-foreground">
          Your status: <span className="font-medium text-foreground">{currentStatus === 'NotAttending' ? 'Not Attending' : currentStatus}</span>
        </p>
      )}
      <div className="flex flex-wrap gap-2">
        {STATUS_OPTIONS.map(({ value, label }) => (
          <Button
            key={value}
            size="sm"
            variant={currentStatus === value ? 'default' : 'outline'}
            disabled={isUpdating}
            onClick={() => setRsvp(value)}
          >
            {label}
          </Button>
        ))}
      </div>
    </div>
  );
}
