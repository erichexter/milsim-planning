import { useQuery } from '@tanstack/react-query';
import { api, type FrequencyAuditLogDto } from '../lib/api';
import { useState } from 'react';

interface Props {
  eventId: string;
}

const actionTypeLabels: Record<string, string> = {
  'created': 'Created',
  'updated': 'Updated',
  'deleted': 'Deleted',
  'conflict_detected': 'Conflict Detected',
  'conflict_overridden': 'Conflict Overridden',
};

const actionTypeColors: Record<string, string> = {
  'created': 'bg-green-50 text-green-800',
  'updated': 'bg-blue-50 text-blue-800',
  'deleted': 'bg-red-50 text-red-800',
  'conflict_detected': 'bg-yellow-50 text-yellow-800',
  'conflict_overridden': 'bg-orange-50 text-orange-800',
};

function formatDate(isoString: string): string {
  const date = new Date(isoString);
  return date.toLocaleString();
}

function formatFrequency(freq: string | null): string {
  if (!freq) return '—';
  return `${freq} MHz`;
}

function AuditLogEntry({ entry }: { entry: FrequencyAuditLogDto }) {
  const actionLabel = actionTypeLabels[entry.actionType] || entry.actionType;
  const actionClass = actionTypeColors[entry.actionType] || 'bg-gray-50 text-gray-800';

  return (
    <div className="border-b pb-4 mb-4">
      <div className="flex items-start justify-between mb-2">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <span className={`inline-block px-2 py-1 rounded-md text-xs font-semibold ${actionClass}`}>
              {actionLabel}
            </span>
            <span className="text-sm text-muted-foreground">{formatDate(entry.occurredAt)}</span>
          </div>
          <p className="font-semibold text-sm">{entry.unitName}</p>
          <p className="text-xs text-muted-foreground">
            {entry.unitType.charAt(0).toUpperCase() + entry.unitType.slice(1)} • {entry.channelName}
          </p>
        </div>
        <div className="text-right text-xs text-muted-foreground">
          <p>{entry.performedByDisplayName || entry.performedByUserId}</p>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2 text-xs mb-2">
        <div>
          <span className="text-muted-foreground">Primary: </span>
          <span className="font-mono">{formatFrequency(entry.primaryFrequency)}</span>
        </div>
        <div>
          <span className="text-muted-foreground">Alternate: </span>
          <span className="font-mono">{formatFrequency(entry.alternateFrequency)}</span>
        </div>
      </div>

      {entry.conflictingUnitName && (
        <div className="text-xs text-amber-700 bg-amber-50 p-2 rounded">
          Conflicting with: <span className="font-semibold">{entry.conflictingUnitName}</span>
        </div>
      )}
    </div>
  );
}

export function FrequencyAuditLog({ eventId }: Props) {
  const [unitFilter, setUnitFilter] = useState('');
  const [newestFirst, setNewestFirst] = useState(true);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['frequency-audit-log', eventId, unitFilter, newestFirst],
    queryFn: () => api.getFrequencyAuditLog(eventId, unitFilter || undefined, undefined, undefined, newestFirst),
  });

  if (isLoading) {
    return <p className="text-sm text-muted-foreground">Loading audit log…</p>;
  }

  if (isError || !data) {
    return <p className="text-sm text-destructive">Failed to load audit log.</p>;
  }

  // AC-05: Log is read-only (UI is display-only, no edit/delete buttons)
  // AC-06: Log is persistent (data comes from database)

  return (
    <div data-testid="frequency-audit-log" className="space-y-4">
      {/* AC-07: Filter controls (optional) */}
      <div className="flex gap-4 items-end">
        <div className="flex-1">
          <label htmlFor="unit-filter" className="text-xs font-semibold text-muted-foreground block mb-1">
            Filter by unit name
          </label>
          <input
            id="unit-filter"
            type="text"
            placeholder="Search unit..."
            value={unitFilter}
            onChange={(e) => setUnitFilter(e.target.value)}
            className="w-full px-3 py-2 border border-input rounded-md text-sm"
          />
        </div>

        <div className="flex items-center gap-2">
          <label htmlFor="sort-order" className="text-xs font-semibold text-muted-foreground">
            Sort
          </label>
          <select
            id="sort-order"
            value={newestFirst ? 'newest' : 'oldest'}
            onChange={(e) => setNewestFirst(e.target.value === 'newest')}
            className="px-3 py-2 border border-input rounded-md text-sm"
          >
            <option value="newest">Newest First</option>
            <option value="oldest">Oldest First</option>
          </select>
        </div>
      </div>

      {/* AC-02: Chronological entries (newest first or oldest first, user configurable) */}
      <div className="border rounded-lg p-4 bg-background">
        {data.length === 0 ? (
          <p className="text-sm text-muted-foreground">No audit log entries found.</p>
        ) : (
          <div>
            <p className="text-xs text-muted-foreground mb-4">Showing {data.length} entries</p>
            {/* AC-03: Each log entry shows all required fields */}
            {data.map((entry) => (
              <AuditLogEntry key={entry.id} entry={entry} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
