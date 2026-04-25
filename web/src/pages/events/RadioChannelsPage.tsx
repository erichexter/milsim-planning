import { useState } from 'react';
import { useParams, Link } from 'react-router';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Radio, Plus, Pencil, Check, X, AlertTriangle, Trash2 } from 'lucide-react';
import {
  api,
  type RadioChannelListDto,
  type ChannelScope,
  type ChannelAssignmentDto,
} from '../../lib/api';
import { useAuth } from '../../hooks/useAuth';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Badge } from '../../components/ui/badge';
import { toast } from 'sonner';

// ── Frequency range constants (NATO standard) ─────────────────────────────────
const SCOPE_INFO: Record<ChannelScope, { label: string; range: string; min: number; max: number }> = {
  VHF: { label: 'VHF', range: '30.0–87.975 MHz', min: 30.0, max: 87.975 },
  UHF: { label: 'UHF', range: '225–400 MHz', min: 225.0, max: 400.0 },
};

// ── Real-time frequency validation (AC-10) ────────────────────────────────────

function validateFrequency(value: string, scope: ChannelScope): string | null {
  const num = parseFloat(value);
  if (isNaN(num)) return 'Enter a valid frequency in MHz (e.g., 36.500).';
  const { min, max, range } = SCOPE_INFO[scope];
  if (num < min || num > max) return `Frequency must be within ${scope} range: ${range}.`;
  // 25 kHz spacing: (freq * 1000) % 25 === 0
  if (Math.round(num * 1000) % 25 !== 0)
    return 'Frequency must align to 25 kHz spacing (e.g., 36.500, 36.525).';
  return null;
}

// ── Inline edit row ────────────────────────────────────────────────────────────

interface ChannelRowProps {
  channel: RadioChannelListDto;
  isCommander: boolean;
}

function ChannelRow({ channel, isCommander }: ChannelRowProps) {
  const queryClient = useQueryClient();
  const { id: eventId } = useParams<{ id: string }>();

  const [editing, setEditing] = useState(false);
  const [editName, setEditName] = useState(channel.name);
  const [editScope, setEditScope] = useState<ChannelScope>(channel.scope);
  const [editError, setEditError] = useState<string | null>(null);

  const updateMutation = useMutation({
    mutationFn: () => api.updateRadioChannel(channel.id, { name: editName.trim(), scope: editScope }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['radio-channels', eventId] });
      setEditing(false);
      setEditError(null);
      toast.success('Channel updated');
    },
    onError: (err: Error & { status?: number }) => {
      if (err.status === 409) {
        setEditError('Channel name already exists in this operation.');
      } else {
        setEditError(err.message);
      }
    },
  });

  const cancelEdit = () => {
    setEditName(channel.name);
    setEditScope(channel.scope);
    setEditError(null);
    setEditing(false);
  };

  const scopeInfo = SCOPE_INFO[channel.scope];

  if (editing) {
    return (
      <tr className="border-b">
        <td className="py-3 px-4">
          <Input
            value={editName}
            onChange={(e) => setEditName(e.target.value)}
            className="h-8 w-full"
            autoFocus
            aria-label="Channel name"
          />
          {editError && <p className="text-xs text-red-600 mt-1">{editError}</p>}
        </td>
        <td className="py-3 px-4">
          <select
            value={editScope}
            onChange={(e) => setEditScope(e.target.value as ChannelScope)}
            className="h-8 rounded border border-input bg-background px-2 text-sm"
            aria-label="Channel scope"
          >
            <option value="VHF">VHF (30.0–87.975 MHz)</option>
            <option value="UHF">UHF (225–400 MHz)</option>
          </select>
        </td>
        <td className="py-3 px-4 text-sm text-muted-foreground">{channel.assignmentCount}</td>
        <td className="py-3 px-4">
          {channel.conflictCount > 0 && (
            <Badge variant="destructive" className="text-xs">
              {channel.conflictCount} conflict{channel.conflictCount !== 1 ? 's' : ''}
            </Badge>
          )}
        </td>
        <td className="py-3 px-4">
          <div className="flex gap-1">
            <Button
              size="sm"
              variant="ghost"
              onClick={() => updateMutation.mutate()}
              disabled={updateMutation.isPending || !editName.trim()}
              aria-label="Save changes"
            >
              <Check className="h-4 w-4 text-green-600" />
            </Button>
            <Button size="sm" variant="ghost" onClick={cancelEdit} aria-label="Cancel edit">
              <X className="h-4 w-4 text-red-600" />
            </Button>
          </div>
        </td>
      </tr>
    );
  }

  return (
    <tr className="border-b hover:bg-muted/50">
      <td className="py-3 px-4 font-medium">{channel.name}</td>
      <td className="py-3 px-4">
        <Badge variant={channel.scope === 'VHF' ? 'default' : 'secondary'}>
          {channel.scope}
        </Badge>
        <span className="ml-2 text-xs text-muted-foreground">{scopeInfo.range}</span>
      </td>
      <td className="py-3 px-4 text-sm text-muted-foreground">{channel.assignmentCount}</td>
      <td className="py-3 px-4">
        {channel.conflictCount > 0 && (
          <Badge variant="destructive" className="text-xs">
            <AlertTriangle className="h-3 w-3 mr-1" />
            {channel.conflictCount} conflict{channel.conflictCount !== 1 ? 's' : ''}
          </Badge>
        )}
      </td>
      <td className="py-3 px-4">
        {isCommander && (
          <Button
            size="sm"
            variant="ghost"
            onClick={() => setEditing(true)}
            aria-label={`Edit channel ${channel.name}`}
          >
            <Pencil className="h-4 w-4" />
          </Button>
        )}
      </td>
    </tr>
  );
}

// ── Create channel form ────────────────────────────────────────────────────────

interface CreateChannelFormProps {
  eventId: string;
}

function CreateChannelForm({ eventId }: CreateChannelFormProps) {
  const queryClient = useQueryClient();

  const [name, setName] = useState('');
  const [scope, setScope] = useState<ChannelScope>('VHF');
  const [formError, setFormError] = useState<string | null>(null);
  const [open, setOpen] = useState(false);

  const createMutation = useMutation({
    mutationFn: () => api.createRadioChannel(eventId, { name: name.trim(), scope }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['radio-channels', eventId] });
      setName('');
      setScope('VHF');
      setFormError(null);
      setOpen(false);
      toast.success('Channel created');
    },
    onError: (err: Error & { status?: number }) => {
      if (err.status === 409) {
        setFormError('Channel name already exists in this operation.');
      } else {
        setFormError(err.message);
      }
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setFormError('Channel name is required.');
      return;
    }
    setFormError(null);
    createMutation.mutate();
  };

  if (!open) {
    return (
      <Button onClick={() => setOpen(true)} size="sm">
        <Plus className="h-4 w-4 mr-1" />
        New Channel
      </Button>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="border rounded-lg p-4 bg-muted/30 space-y-4">
      <h3 className="font-semibold text-sm">Create Radio Channel</h3>

      {/* Frequency range reference */}
      <div className="rounded-md bg-blue-50 dark:bg-blue-950/20 border border-blue-200 dark:border-blue-800 p-3 text-sm">
        <p className="font-medium text-blue-800 dark:text-blue-300 mb-1">NATO Frequency Ranges</p>
        <ul className="text-blue-700 dark:text-blue-400 space-y-0.5">
          <li><span className="font-medium">VHF:</span> 30.0–87.975 MHz (25 kHz spacing)</li>
          <li><span className="font-medium">UHF:</span> 225–400 MHz (25 kHz spacing)</li>
        </ul>
      </div>

      <div className="space-y-2">
        <label htmlFor="channel-name" className="text-sm font-medium">
          Channel name <span className="text-red-500">*</span>
        </label>
        <Input
          id="channel-name"
          aria-label="Channel name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Command Net, Air Net"
          className="max-w-sm"
          autoFocus
        />
      </div>

      <div className="space-y-2">
        <label htmlFor="channel-scope" className="text-sm font-medium">
          Scope
        </label>
        <div className="flex gap-4">
          {(['VHF', 'UHF'] as ChannelScope[]).map((s) => (
            <label key={s} className="flex items-center gap-2 cursor-pointer">
              <input
                type="radio"
                name="scope"
                value={s}
                checked={scope === s}
                onChange={() => setScope(s)}
                className="accent-primary"
              />
              <span className="text-sm font-medium">{s}</span>
              <span className="text-xs text-muted-foreground">{SCOPE_INFO[s].range}</span>
            </label>
          ))}
        </div>
      </div>

      {formError && (
        <p className="text-sm text-red-600" role="alert">{formError}</p>
      )}

      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={createMutation.isPending || !name.trim()}>
          {createMutation.isPending ? 'Saving…' : 'Save Channel'}
        </Button>
        <Button
          type="button"
          size="sm"
          variant="ghost"
          onClick={() => { setOpen(false); setName(''); setFormError(null); }}
        >
          Cancel
        </Button>
      </div>
    </form>
  );
}

// ── Assignment row (AC-09: edit/delete controls) ──────────────────────────────

interface AssignmentRowProps {
  assignment: ChannelAssignmentDto;
  channels: RadioChannelListDto[];
  isCommander: boolean;
  eventId: string;
}

function AssignmentRow({ assignment, channels, isCommander, eventId }: AssignmentRowProps) {
  const queryClient = useQueryClient();

  const [editing, setEditing] = useState(false);
  const [freqValue, setFreqValue] = useState(assignment.primaryFrequency.toString());
  const [freqError, setFreqError] = useState<string | null>(null);

  // Real-time validation as user types (AC-10)
  const handleFreqChange = (val: string) => {
    setFreqValue(val);
    const scope = (assignment.channelScope as ChannelScope) ?? 'VHF';
    setFreqError(validateFrequency(val, scope));
  };

  const updateMutation = useMutation({
    mutationFn: () =>
      api.updateChannelAssignment(eventId, assignment.id, {
        primaryFrequency: parseFloat(freqValue),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['channel-assignments', eventId] });
      setEditing(false);
      toast.success('Assignment updated');
    },
    onError: (err: Error & { status?: number }) => {
      setFreqError(err.message);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => api.deleteChannelAssignment(eventId, assignment.id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['channel-assignments', eventId] });
      void queryClient.invalidateQueries({ queryKey: ['radio-channels', eventId] });
      toast.success('Assignment deleted');
    },
    onError: (err: Error) => {
      toast.error(err.message);
    },
  });

  const channel = channels.find((c) => c.id === assignment.radioChannelId);

  if (editing) {
    return (
      <tr className="border-b">
        <td className="py-3 px-4 text-sm">{assignment.squadName}</td>
        <td className="py-3 px-4 text-sm">{assignment.channelName}</td>
        <td className="py-3 px-4">
          <div className="space-y-1">
            <Input
              aria-label="Primary frequency (MHz)"
              value={freqValue}
              onChange={(e) => handleFreqChange(e.target.value)}
              placeholder="e.g. 36.500"
              className="h-8 w-32"
              type="number"
              step="0.025"
              min={channel ? SCOPE_INFO[channel.scope].min : 30}
              max={channel ? SCOPE_INFO[channel.scope].max : 400}
              autoFocus
            />
            {freqError && (
              <p className="text-xs text-red-600" role="alert">{freqError}</p>
            )}
          </div>
        </td>
        <td className="py-3 px-4">
          <div className="flex gap-1">
            <Button
              size="sm"
              variant="ghost"
              onClick={() => updateMutation.mutate()}
              disabled={updateMutation.isPending || !!freqError || !freqValue}
              aria-label="Save assignment"
            >
              <Check className="h-4 w-4 text-green-600" />
            </Button>
            <Button
              size="sm"
              variant="ghost"
              onClick={() => { setEditing(false); setFreqValue(assignment.primaryFrequency.toString()); setFreqError(null); }}
              aria-label="Cancel edit"
            >
              <X className="h-4 w-4 text-red-600" />
            </Button>
          </div>
        </td>
      </tr>
    );
  }

  return (
    <tr className="border-b hover:bg-muted/50">
      <td className="py-3 px-4 text-sm font-medium">{assignment.squadName}</td>
      <td className="py-3 px-4 text-sm">
        {assignment.channelName}
        <span className="ml-2 text-xs text-muted-foreground">{assignment.channelScope}</span>
      </td>
      <td className="py-3 px-4 text-sm font-mono">{assignment.primaryFrequency.toFixed(3)} MHz</td>
      <td className="py-3 px-4">
        {isCommander && (
          <div className="flex gap-1">
            <Button
              size="sm"
              variant="ghost"
              onClick={() => setEditing(true)}
              aria-label={`Edit assignment for ${assignment.squadName}`}
            >
              <Pencil className="h-4 w-4" />
            </Button>
            <Button
              size="sm"
              variant="ghost"
              onClick={() => deleteMutation.mutate()}
              disabled={deleteMutation.isPending}
              aria-label={`Delete assignment for ${assignment.squadName}`}
            >
              <Trash2 className="h-4 w-4 text-red-500" />
            </Button>
          </div>
        )}
      </td>
    </tr>
  );
}

// ── Create assignment form (AC-01, AC-02, AC-03, AC-10) ───────────────────────

interface CreateAssignmentFormProps {
  eventId: string;
  channels: RadioChannelListDto[];
}

interface FlatSquad {
  id: string;
  name: string;
  platoonName: string;
}

function CreateAssignmentForm({ eventId, channels }: CreateAssignmentFormProps) {
  const queryClient = useQueryClient();

  const [open, setOpen] = useState(false);
  const [channelId, setChannelId] = useState('');
  const [squadId, setSquadId] = useState('');
  const [freqValue, setFreqValue] = useState('');
  const [freqError, setFreqError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  // Load roster to get squad list (AC-01)
  const { data: roster } = useQuery({
    queryKey: ['roster', eventId],
    queryFn: () => api.getRoster(eventId),
    enabled: open,
  });

  // Flatten squads from platoon hierarchy
  const squads: FlatSquad[] = roster
    ? roster.platoons.flatMap((p) =>
        p.squads.map((s) => ({ id: s.id, name: s.name, platoonName: p.name }))
      )
    : [];

  const selectedChannel = channels.find((c) => c.id === channelId);

  // Real-time frequency validation (AC-10)
  const handleFreqChange = (val: string) => {
    setFreqValue(val);
    if (selectedChannel) {
      setFreqError(validateFrequency(val, selectedChannel.scope));
    } else {
      setFreqError(null);
    }
  };

  // Re-validate when channel scope changes
  const handleChannelChange = (id: string) => {
    setChannelId(id);
    const ch = channels.find((c) => c.id === id);
    if (ch && freqValue) {
      setFreqError(validateFrequency(freqValue, ch.scope));
    }
  };

  const createMutation = useMutation({
    mutationFn: () =>
      api.createChannelAssignment(eventId, {
        radioChannelId: channelId,
        squadId,
        primaryFrequency: parseFloat(freqValue),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['channel-assignments', eventId] });
      void queryClient.invalidateQueries({ queryKey: ['radio-channels', eventId] });
      setOpen(false);
      setChannelId('');
      setSquadId('');
      setFreqValue('');
      setFreqError(null);
      setFormError(null);
      toast.success('Assignment created');
    },
    onError: (err: Error & { status?: number }) => {
      setFormError(err.message);
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!channelId) { setFormError('Select a channel.'); return; }
    if (!squadId) { setFormError('Select a unit.'); return; }
    const err = selectedChannel ? validateFrequency(freqValue, selectedChannel.scope) : 'Select a channel first.';
    if (err) { setFreqError(err); return; }
    setFormError(null);
    createMutation.mutate();
  };

  if (!open) {
    return (
      <Button onClick={() => setOpen(true)} size="sm" variant="outline">
        <Plus className="h-4 w-4 mr-1" />
        Assign Frequency
      </Button>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="border rounded-lg p-4 bg-muted/30 space-y-4">
      <h3 className="font-semibold text-sm">Assign Frequency to Unit</h3>

      {/* AC-02: Channel selector */}
      <div className="space-y-2">
        <label htmlFor="assign-channel" className="text-sm font-medium">
          Channel <span className="text-red-500">*</span>
        </label>
        <select
          id="assign-channel"
          aria-label="Select channel"
          value={channelId}
          onChange={(e) => handleChannelChange(e.target.value)}
          className="h-9 w-full max-w-sm rounded border border-input bg-background px-3 text-sm"
        >
          <option value="">Select a channel…</option>
          {channels.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name} ({c.scope} — {SCOPE_INFO[c.scope].range})
            </option>
          ))}
        </select>
      </div>

      {/* AC-01: Unit (squad) selector */}
      <div className="space-y-2">
        <label htmlFor="assign-squad" className="text-sm font-medium">
          Unit <span className="text-red-500">*</span>
        </label>
        <select
          id="assign-squad"
          aria-label="Select unit"
          value={squadId}
          onChange={(e) => setSquadId(e.target.value)}
          className="h-9 w-full max-w-sm rounded border border-input bg-background px-3 text-sm"
        >
          <option value="">Select a unit…</option>
          {squads.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name} ({s.platoonName})
            </option>
          ))}
        </select>
      </div>

      {/* AC-03: Primary frequency input (MHz) with AC-10 real-time validation */}
      <div className="space-y-2">
        <label htmlFor="assign-freq" className="text-sm font-medium">
          Primary frequency (MHz) <span className="text-red-500">*</span>
        </label>
        <Input
          id="assign-freq"
          aria-label="Primary frequency (MHz)"
          value={freqValue}
          onChange={(e) => handleFreqChange(e.target.value)}
          placeholder={
            selectedChannel
              ? `e.g. ${selectedChannel.scope === 'VHF' ? '36.500' : '225.025'}`
              : 'Select a channel first'
          }
          type="number"
          step="0.025"
          min={selectedChannel ? SCOPE_INFO[selectedChannel.scope].min : undefined}
          max={selectedChannel ? SCOPE_INFO[selectedChannel.scope].max : undefined}
          className="max-w-xs"
          disabled={!channelId}
        />
        {freqError && (
          <p className="text-xs text-red-600" role="alert">{freqError}</p>
        )}
        {selectedChannel && !freqError && freqValue && (
          <p className="text-xs text-green-600">Valid {selectedChannel.scope} frequency</p>
        )}
        {selectedChannel && (
          <p className="text-xs text-muted-foreground">
            {selectedChannel.scope} range: {SCOPE_INFO[selectedChannel.scope].range}, 25 kHz spacing
          </p>
        )}
      </div>

      {formError && (
        <p className="text-sm text-red-600" role="alert">{formError}</p>
      )}

      <div className="flex gap-2">
        <Button
          type="submit"
          size="sm"
          disabled={createMutation.isPending || !channelId || !squadId || !!freqError || !freqValue}
        >
          {createMutation.isPending ? 'Saving…' : 'Save Assignment'}
        </Button>
        <Button
          type="button"
          size="sm"
          variant="ghost"
          onClick={() => {
            setOpen(false);
            setChannelId('');
            setSquadId('');
            setFreqValue('');
            setFreqError(null);
            setFormError(null);
          }}
        >
          Cancel
        </Button>
      </div>
    </form>
  );
}

// ── Assignments section (AC-08: list view) ────────────────────────────────────

interface AssignmentsSectionProps {
  eventId: string;
  channels: RadioChannelListDto[];
  isCommander: boolean;
}

function AssignmentsSection({ eventId, channels, isCommander }: AssignmentsSectionProps) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['channel-assignments', eventId],
    queryFn: () => api.getChannelAssignments(eventId),
    enabled: !!eventId,
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Frequency Assignments</h2>
        {isCommander && channels.length > 0 && (
          <CreateAssignmentForm eventId={eventId} channels={channels} />
        )}
      </div>

      {isLoading && <div className="text-sm text-muted-foreground">Loading assignments…</div>}
      {error && <div className="text-sm text-red-600">Failed to load assignments.</div>}

      {data && data.items.length === 0 && (
        <div className="rounded-lg border border-dashed p-6 text-center text-muted-foreground text-sm">
          No frequency assignments yet.
          {isCommander && channels.length > 0 && (
            <span> Click "Assign Frequency" to get started.</span>
          )}
        </div>
      )}

      {data && data.items.length > 0 && (
        <div className="rounded-lg border overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="text-left py-2 px-4 font-medium">Unit</th>
                <th className="text-left py-2 px-4 font-medium">Channel</th>
                <th className="text-left py-2 px-4 font-medium">Primary Frequency</th>
                <th className="py-2 px-4" />
              </tr>
            </thead>
            <tbody>
              {data.items.map((assignment) => (
                <AssignmentRow
                  key={assignment.id}
                  assignment={assignment}
                  channels={channels}
                  isCommander={isCommander}
                  eventId={eventId}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ── Page component ─────────────────────────────────────────────────────────────

export function RadioChannelsPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const isCommander = user?.role === 'faction_commander';

  const { data: channels, isLoading, error } = useQuery({
    queryKey: ['radio-channels', id],
    queryFn: () => api.getRadioChannels(id!),
    enabled: !!id,
  });

  if (isLoading) return <div className="p-6">Loading channels…</div>;
  if (error) return <div className="p-6 text-red-600">Failed to load channels.</div>;

  return (
    <div className="p-6 space-y-8">
      {/* Breadcrumb */}
      <nav className="text-sm text-muted-foreground flex items-center gap-1">
        <Link to="/events" className="hover:underline">Events</Link>
        <span>/</span>
        <Link to={`/events/${id}`} className="hover:underline">Event</Link>
        <span>/</span>
        <span className="text-foreground font-medium">Radio Channels</span>
      </nav>

      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Radio className="h-5 w-5 text-primary" />
          <h1 className="text-xl font-semibold">Radio Channels</h1>
        </div>
        {isCommander && <CreateChannelForm eventId={id!} />}
      </div>

      {/* Channel list */}
      {channels && channels.length === 0 ? (
        <div className="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
          <Radio className="h-8 w-8 mx-auto mb-2 opacity-40" />
          <p>No radio channels yet.</p>
          {isCommander && <p className="text-sm mt-1">Click "New Channel" to get started.</p>}
        </div>
      ) : (
        <div className="rounded-lg border overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="text-left py-2 px-4 font-medium">Channel Name</th>
                <th className="text-left py-2 px-4 font-medium">Scope / Range</th>
                <th className="text-left py-2 px-4 font-medium">Assignments</th>
                <th className="text-left py-2 px-4 font-medium">Conflicts</th>
                <th className="py-2 px-4" />
              </tr>
            </thead>
            <tbody>
              {channels?.map((channel) => (
                <ChannelRow
                  key={channel.id}
                  channel={channel}
                  isCommander={isCommander}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Frequency assignments section (AC-08) */}
      {id && (
        <AssignmentsSection
          eventId={id}
          channels={channels ?? []}
          isCommander={isCommander}
        />
      )}
    </div>
  );
}
