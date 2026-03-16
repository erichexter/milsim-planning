import { useMemo, useState, useRef, useEffect } from 'react';
import { useParams } from 'react-router';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../../lib/api';
import { SquadCell } from '../../components/hierarchy/SquadCell';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { EventBreadcrumb } from '../../components/EventBreadcrumb';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../components/ui/table';

export function HierarchyBuilder() {
  const { id: eventId } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const { data: roster } = useQuery({
    queryKey: ['roster', eventId],
    queryFn: () => api.getRoster(eventId!),
  });

  // ── Create platoon ────────────────────────────────────────────────────────
  const [newPlatoonName, setNewPlatoonName] = useState('');
  const createPlatoonMutation = useMutation({
    mutationFn: (name: string) => api.createPlatoon(eventId!, name),
    onSuccess: () => {
      setNewPlatoonName('');
      void queryClient.invalidateQueries({ queryKey: ['roster', eventId] });
    },
  });

  // ── Set player role ───────────────────────────────────────────────────────
  const setRoleMutation = useMutation({
    mutationFn: ({ playerId, role }: { playerId: string; role: string | null }) =>
      api.setPlayerRole(playerId, role),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['roster', eventId] }),
  });

  // ── Create squad ──────────────────────────────────────────────────────────
  const [selectedPlatoonId, setSelectedPlatoonId] = useState('');
  const [newSquadName, setNewSquadName] = useState('');
  const createSquadMutation = useMutation({
    mutationFn: ({ platoonId, name }: { platoonId: string; name: string }) =>
      api.createSquad(platoonId, name),
    onSuccess: () => {
      setNewSquadName('');
      void queryClient.invalidateQueries({ queryKey: ['roster', eventId] });
    },
  });

  // ── Player table data ─────────────────────────────────────────────────────
  const allPlayers = useMemo(() => {
    if (!roster) return [];
    const assigned = roster.platoons.flatMap((p) =>
      p.squads.flatMap((s) =>
        s.players.map((pl) => ({ ...pl, squadId: s.id, eventId: eventId! }))
      )
    );
    const unassigned = roster.unassignedPlayers.map((pl) => ({
      ...pl,
      squadId: null as string | null,
      eventId: eventId!,
    }));
    return [...assigned, ...unassigned];
  }, [roster, eventId]);

  const allSquads = useMemo(
    () => roster?.platoons.flatMap((p) => p.squads) ?? [],
    [roster]
  );

  const playersByAffiliation = useMemo(() => {
    const groups = new Map<string, typeof allPlayers>();
    for (const player of allPlayers) {
      const key = player.teamAffiliation ?? '(No Team)';
      const group = groups.get(key) ?? [];
      group.push(player);
      groups.set(key, group);
    }
    return groups;
  }, [allPlayers]);

  if (!roster) return <div className="p-6">Loading hierarchy...</div>;

  const platoons = roster.platoons;

  return (
    <div className="p-6 max-w-5xl mx-auto space-y-8">
      <EventBreadcrumb eventId={eventId!} page="Hierarchy" />
      <h1 className="text-2xl font-bold">Hierarchy Builder</h1>

      {/* ── Structure panel ──────────────────────────────────────────────── */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">

        {/* Create platoon */}
        <div className="border rounded-lg p-4 space-y-3">
          <h2 className="font-semibold text-sm">Create Platoon</h2>
          <div className="flex gap-2">
            <Input
              placeholder="e.g. Alpha Platoon"
              value={newPlatoonName}
              onChange={(e) => setNewPlatoonName(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && newPlatoonName.trim()) {
                  createPlatoonMutation.mutate(newPlatoonName.trim());
                }
              }}
            />
            <Button
              onClick={() => createPlatoonMutation.mutate(newPlatoonName.trim())}
              disabled={!newPlatoonName.trim() || createPlatoonMutation.isPending}
            >
              Add
            </Button>
          </div>
          {platoons.length > 0 && (
            <ul className="text-sm space-y-1">
              {platoons.map((p) => (
                <li key={p.id} className="text-muted-foreground">
                  {p.name}
                  <span className="ml-2 text-xs">
                    ({p.squads.length} squad{p.squads.length !== 1 ? 's' : ''})
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* Create squad */}
        <div className="border rounded-lg p-4 space-y-3">
          <h2 className="font-semibold text-sm">Create Squad</h2>
          {platoons.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              Create a platoon first.
            </p>
          ) : (
            <>
              <select
                className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={selectedPlatoonId}
                onChange={(e) => setSelectedPlatoonId(e.target.value)}
              >
                <option value="">Select platoon...</option>
                {platoons.map((p) => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
              <div className="flex gap-2">
                <Input
                  placeholder="e.g. Alpha-1"
                  value={newSquadName}
                  onChange={(e) => setNewSquadName(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' && newSquadName.trim() && selectedPlatoonId) {
                      createSquadMutation.mutate({
                        platoonId: selectedPlatoonId,
                        name: newSquadName.trim(),
                      });
                    }
                  }}
                />
                <Button
                  onClick={() =>
                    createSquadMutation.mutate({
                      platoonId: selectedPlatoonId,
                      name: newSquadName.trim(),
                    })
                  }
                  disabled={
                    !newSquadName.trim() ||
                    !selectedPlatoonId ||
                    createSquadMutation.isPending
                  }
                >
                  Add
                </Button>
              </div>
              {/* Show existing squads under each platoon */}
              {platoons.map((p) =>
                p.squads.length > 0 ? (
                  <div key={p.id} className="text-sm">
                    <span className="font-medium text-muted-foreground">{p.name}:</span>{' '}
                    {p.squads.map((s) => s.name).join(', ')}
                  </div>
                ) : null
              )}
            </>
          )}
        </div>
      </div>

      {/* ── Player assignment table ───────────────────────────────────────── */}
      <div>
        <h2 className="text-lg font-semibold mb-2">Assign Players to Squads</h2>
        <p className="text-sm text-muted-foreground mb-4">
          Players are grouped by their Team Affiliation from the CSV. Use the dropdown to assign each player to a squad.
        </p>

        {allPlayers.length === 0 && (
          <p className="text-sm text-muted-foreground">
            No players yet. Import a roster first.
          </p>
        )}

        {allSquads.length === 0 && allPlayers.length > 0 && (
          <p className="text-sm text-amber-600 mb-4">
            Create platoons and squads above before assigning players.
          </p>
        )}

        {[...playersByAffiliation.entries()].map(([affiliation, players]) => (
          <div key={affiliation} className="mb-6">
            <h3 className="text-base font-semibold mb-2">{affiliation}</h3>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Callsign</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Squad</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {players.map((player) => (
                  <TableRow key={player.id}>
                    <TableCell className="font-mono font-bold text-orange-500">
                      [{player.callsign ?? '—'}]
                    </TableCell>
                    <TableCell>{player.name}</TableCell>
                    <TableCell>
                      <RoleCell
                        playerId={player.id}
                        role={player.role}
                        onSave={(role) => setRoleMutation.mutate({ playerId: player.id, role })}
                      />
                    </TableCell>
                    <TableCell>
                      <SquadCell player={player} squads={allSquads} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Inline editable role cell ─────────────────────────────────────────────────

function RoleCell({
  playerId,
  role,
  onSave,
}: {
  playerId: string;
  role: string | null;
  onSave: (role: string | null) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(role ?? '');
  const inputRef = useRef<HTMLInputElement>(null);

  // Sync if parent value changes (e.g. after mutation invalidates query)
  useEffect(() => { setValue(role ?? ''); }, [role]);

  const commit = () => {
    setEditing(false);
    const trimmed = value.trim() || null;
    if (trimmed !== role) onSave(trimmed);
  };

  if (editing) {
    return (
      <Input
        ref={inputRef}
        autoFocus
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onBlur={commit}
        onKeyDown={(e) => {
          if (e.key === 'Enter') commit();
          if (e.key === 'Escape') { setValue(role ?? ''); setEditing(false); }
        }}
        placeholder="e.g. Platoon Commander"
        className="h-7 text-sm w-40"
      />
    );
  }

  return (
    <button
      onClick={() => { setEditing(true); }}
      className="text-sm text-left w-full min-h-[28px] px-1 rounded hover:bg-muted/50 transition-colors"
      title="Click to edit role"
    >
      {role
        ? <span className="text-muted-foreground italic">{role}</span>
        : <span className="text-muted-foreground/40 text-xs">set role…</span>
      }
    </button>
  );
}
