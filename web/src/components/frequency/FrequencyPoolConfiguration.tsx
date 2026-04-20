import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertCircle, Radio, Trash2, Plus } from 'lucide-react';
import { api } from '../../lib/api';
import type { CreateFrequencyPoolRequest, FrequencyPoolEntryInputDto } from '../../lib/api';
import { Button } from '../ui/button';
import { Badge } from '../ui/badge';

interface FrequencyPoolConfigurationProps {
  eventId: string;
  onSuccess?: () => void;
}

const RESERVED_ROLES = ['Safety', 'Medical', 'Control'];

export function FrequencyPoolConfiguration({ eventId, onSuccess }: FrequencyPoolConfigurationProps) {
  const queryClient = useQueryClient();
  const [entries, setEntries] = useState<FrequencyPoolEntryInputDto[]>([]);
  const [frequencyInput, setFrequencyInput] = useState('');
  const [errors, setErrors] = useState<string[]>([]);

  // ── Queries ────────────────────────────────────────────────────────────
  const { data: pool, isLoading } = useQuery({
    queryKey: ['frequency-pool', eventId],
    queryFn: () => api.getFrequencyPool(eventId),
    staleTime: 60000,
  });

  const createMutation = useMutation({
    mutationFn: (req: CreateFrequencyPoolRequest) =>
      api.createOrUpdateFrequencyPool(eventId, req),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['frequency-pool', eventId] });
      setFrequencyInput('');
      setEntries([]);
      setErrors([]);
      onSuccess?.();
    },
  });

  // ── Handlers ───────────────────────────────────────────────────────────
  const parseFrequencies = (input: string): string[] => {
    return input
      .split(/[,\n]+/)
      .map((f) => f.trim())
      .filter((f) => f.length > 0);
  };

  const handleAddFrequencies = () => {
    const newFrequencies = parseFrequencies(frequencyInput);

    if (newFrequencies.length === 0) {
      setErrors(['Please enter at least one frequency']);
      return;
    }

    const newEntries = newFrequencies.map((freq, idx) => ({
      channel: freq,
      displayGroup: null,
      sortOrder: entries.length + idx + 1,
      isReserved: false,
      reservedRole: null,
    }));

    setEntries([...entries, ...newEntries]);
    setFrequencyInput('');
    setErrors([]);
  };

  const handleRemoveEntry = (index: number) => {
    setEntries(entries.filter((_, i) => i !== index));
  };

  const handleToggleReserved = (index: number, role: string) => {
    const newEntries = [...entries];
    const entry = newEntries[index];

    if (entry.isReserved && entry.reservedRole === role) {
      // Toggle off
      entry.isReserved = false;
      entry.reservedRole = null;
    } else {
      // Toggle on
      entry.isReserved = true;
      entry.reservedRole = role;
    }

    setEntries(newEntries);
  };

  const handleSave = () => {
    const saveErrors: string[] = [];

    if (entries.length === 0) {
      saveErrors.push('Frequency pool must contain at least one frequency');
    }

    // Check for duplicates
    const channels = entries.map((e) => e.channel.toLowerCase().trim());
    const duplicates = channels.filter((ch, idx) => channels.indexOf(ch) !== idx);
    if (duplicates.length > 0) {
      saveErrors.push(`Duplicate frequencies: ${[...new Set(duplicates)].join(', ')}`);
    }

    if (saveErrors.length > 0) {
      setErrors(saveErrors);
      return;
    }

    createMutation.mutate({ entries });
  };

  // ── Derived stats ────────────────────────────────────────────────────────
  const totalEntries = entries.length;
  const reservedCount = entries.filter((e) => e.isReserved).length;
  const availableToFactions = totalEntries - reservedCount;

  if (isLoading) {
    return <div className="p-4 text-sm text-muted-foreground">Loading frequency pool...</div>;
  }

  // AC-07: Show confirmation if pool exists
  if (pool && entries.length === 0) {
    return (
      <div className="space-y-4">
        <div className="rounded-lg border border-green-200 bg-green-50 p-4">
          <div className="space-y-3">
            <h3 className="font-semibold text-green-900">Frequency Pool Configured</h3>
            <div className="text-sm text-green-800 space-y-2">
              <p>
                <strong>Total Frequencies:</strong> {pool.entries.length}
              </p>
              <p>
                <strong>Reserved Frequencies:</strong> {pool.entries.filter((e) => e.isReserved).length}
              </p>
              <p className="text-base font-semibold text-green-900">
                <strong>Available to Factions:</strong> {pool.entries.filter((e) => !e.isReserved).length}
              </p>
            </div>
          </div>
        </div>

        <div className="space-y-2">
          <h4 className="text-sm font-semibold">Current Pool</h4>
          <div className="max-h-[300px] overflow-y-auto rounded border p-3 space-y-2">
            {pool.entries.sort((a, b) => a.sortOrder - b.sortOrder).map((entry) => (
              <div key={entry.id} className="flex items-center justify-between text-sm bg-muted p-2 rounded">
                <div className="flex-1">
                  <p className="font-mono">{entry.channel}</p>
                  {entry.displayGroup && (
                    <p className="text-xs text-muted-foreground">{entry.displayGroup}</p>
                  )}
                </div>
                {entry.isReserved && (
                  <Badge variant="secondary">{entry.reservedRole}</Badge>
                )}
              </div>
            ))}
          </div>
        </div>

        <Button onClick={() => setEntries([])} variant="outline" className="w-full">
          Update Pool
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* ── Input Section ─────────────────────────────────────────────────── */}
      <div className="space-y-3">
        <label className="block text-sm font-semibold">
          AC-02: Add Frequencies (comma-separated or one per line)
        </label>
        <textarea
          value={frequencyInput}
          onChange={(e) => setFrequencyInput(e.target.value)}
          placeholder="e.g. 152.4 MHz&#10;152.5 MHz&#10;154.2 MHz"
          className="w-full h-24 p-3 border rounded-lg font-mono text-sm focus:outline-none focus:ring-2 focus:ring-ring"
        />
        <Button onClick={handleAddFrequencies} disabled={!frequencyInput.trim()}>
          <Plus className="h-4 w-4 mr-1" />
          Add to Pool
        </Button>
      </div>

      {/* ── Error Messages ────────────────────────────────────────────────── */}
      {errors.length > 0 && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3">
          <div className="flex gap-2">
            <AlertCircle className="h-4 w-4 text-red-600 shrink-0 mt-0.5" />
            <div className="text-sm text-red-700 space-y-1">
              {errors.map((err, idx) => (
                <p key={idx}>{err}</p>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* ── AC-03: Reserved Frequency Selection ────────────────────────────── */}
      {entries.length > 0 && (
        <div className="space-y-3">
          <label className="block text-sm font-semibold">
            AC-03: Designate Reserved Frequencies (Safety, Medical, Control)
          </label>
          <div className="max-h-[400px] overflow-y-auto space-y-2 border rounded-lg p-3">
            {entries.map((entry, idx) => (
              <div
                key={idx}
                className="flex items-center justify-between gap-3 p-3 bg-muted rounded-lg"
              >
                <div className="flex-1 min-w-0">
                  <p className="font-mono text-sm break-all">{entry.channel}</p>
                  <div className="flex gap-1 mt-1">
                    {RESERVED_ROLES.map((role) => (
                      <button
                        key={role}
                        onClick={() => handleToggleReserved(idx, role)}
                        className={`text-xs px-2 py-1 rounded font-medium transition-colors ${
                          entry.isReserved && entry.reservedRole === role
                            ? 'bg-blue-600 text-white'
                            : 'bg-muted-foreground/10 text-muted-foreground hover:bg-muted-foreground/20'
                        }`}
                      >
                        <Radio className="h-3 w-3 inline mr-1" />
                        {role}
                      </button>
                    ))}
                  </div>
                </div>
                <button
                  onClick={() => handleRemoveEntry(idx)}
                  className="p-2 hover:bg-red-100 rounded transition-colors shrink-0"
                  title="Remove frequency"
                >
                  <Trash2 className="h-4 w-4 text-red-600" />
                </button>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ── AC-07: Confirmation Summary ────────────────────────────────────── */}
      {entries.length > 0 && (
        <div className="rounded-lg border border-blue-200 bg-blue-50 p-4">
          <h4 className="font-semibold text-blue-900 mb-3">AC-07: Confirmation Summary</h4>
          <div className="space-y-2 text-sm text-blue-800">
            <p>
              <strong>Total Frequencies in Pool:</strong> {totalEntries}
            </p>
            <p>
              <strong>Reserved Frequencies:</strong> {reservedCount}
            </p>
            <p className="text-base font-bold text-blue-900">
              <strong>Available to Factions:</strong> {availableToFactions}
            </p>
          </div>
        </div>
      )}

      {/* ── AC-04, AC-05 Actions ──────────────────────────────────────────── */}
      {entries.length > 0 && (
        <div className="flex gap-2">
          <Button
            onClick={handleSave}
            disabled={createMutation.isPending}
            className="flex-1"
          >
            {createMutation.isPending ? 'Saving...' : 'Save Frequency Pool'}
          </Button>
          <Button
            onClick={() => setEntries([])}
            variant="outline"
            className="flex-1"
          >
            Cancel
          </Button>
        </div>
      )}

      {createMutation.isError && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3">
          <p className="text-sm text-red-700">
            {(createMutation.error as Error).message}
          </p>
        </div>
      )}
    </div>
  );
}
