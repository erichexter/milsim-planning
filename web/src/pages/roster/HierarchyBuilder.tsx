import { useMemo, useState, useRef, useEffect } from 'react';
import { useParams } from 'react-router';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Check, ChevronsUpDown } from 'lucide-react';
import { api, PlatoonDto } from '../../lib/api';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { EventBreadcrumb } from '../../components/EventBreadcrumb';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '../../components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '../../components/ui/popover';
import { cn } from '../../lib/utils';
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
  const [isCommandElement, setIsCommandElement] = useState(false);
  const createPlatoonMutation = useMutation({
    mutationFn: ({ name, isCommandElement }: { name: string; isCommandElement: boolean }) =>
      api.createPlatoon(eventId!, name, isCommandElement),
    onSuccess: () => {
      setNewPlatoonName('');
      setIsCommandElement(false);
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
      [
        // HQ players (platoon-level, no squad)
        ...p.hqPlayers.map((pl) => ({
          ...pl,
          squadId: null as string | null,
          platoonId: p.id,
          eventId: eventId!,
        })),
        // Squad players
        ...p.squads.flatMap((s) =>
          s.players.map((pl) => ({ ...pl, squadId: s.id, platoonId: p.id, eventId: eventId! }))
        ),
      ]
    );
    const unassigned = roster.unassignedPlayers.map((pl) => ({
      ...pl,
      squadId: null as string | null,
      platoonId: null as string | null,
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
  const commandElements = platoons.filter((p) => p.isCommandElement);
  const regularPlatoons = platoons.filter((p) => !p.isCommandElement);

  return (
    <div className="p-6 max-w-5xl mx-auto space-y-8">
      <EventBreadcrumb eventId={eventId!} page="Hierarchy" />
      <h1 className="text-2xl font-bold">Hierarchy Builder</h1>

      {/* ── Structure panel ──────────────────────────────────────────────── */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">

        {/* Create platoon / command element */}
        <div className="border rounded-lg p-4 space-y-3">
          <h2 className="font-semibold text-sm">Create Platoon / Command Element</h2>
          <div className="flex gap-2">
            <Input
              placeholder={isCommandElement ? 'e.g. Battalion HQ' : 'e.g. Alpha Platoon'}
              value={newPlatoonName}
              onChange={(e) => setNewPlatoonName(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && newPlatoonName.trim()) {
                  createPlatoonMutation.mutate({ name: newPlatoonName.trim(), isCommandElement });
                }
              }}
            />
            <Button
              onClick={() => createPlatoonMutation.mutate({ name: newPlatoonName.trim(), isCommandElement })}
              disabled={!newPlatoonName.trim() || createPlatoonMutation.isPending}
            >
              Add
            </Button>
          </div>
          {/* Command element checkbox */}
          <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
            <input
              type="checkbox"
              checked={isCommandElement}
              onChange={(e) => setIsCommandElement(e.target.checked)}
              className="rounded"
            />
            <span>Command Element <span className="text-muted-foreground text-xs">(no squads — for CO, XO, etc.)</span></span>
          </label>

          {/* Command elements list */}
          {commandElements.length > 0 && (
            <div className="space-y-1">
              <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">Command Elements</p>
              <ul className="text-sm space-y-1">
                {commandElements.map((p) => (
                  <li key={p.id} className="text-amber-600 font-medium">
                    ★ {p.name}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {/* Regular platoons list */}
          {regularPlatoons.length > 0 && (
            <div className="space-y-1">
              {commandElements.length > 0 && (
                <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">Platoons</p>
              )}
              <ul className="text-sm space-y-1">
                {regularPlatoons.map((p) => (
                  <li key={p.id} className="text-muted-foreground">
                    {p.name}
                    <span className="ml-2 text-xs">
                      ({p.squads.length} squad{p.squads.length !== 1 ? 's' : ''})
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>

        {/* Create squad — only under regular platoons */}
        <div className="border rounded-lg p-4 space-y-3">
          <h2 className="font-semibold text-sm">Create Squad</h2>
          {regularPlatoons.length === 0 ? (
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
                {regularPlatoons.map((p) => (
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
              {regularPlatoons.map((p) =>
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
          Players are grouped by their Team Affiliation from the CSV. Use the dropdown to assign each player to a command element, platoon HQ slot, or squad.
        </p>

        {allPlayers.length === 0 && (
          <p className="text-sm text-muted-foreground">
            No players yet. Import a roster first.
          </p>
        )}

        {allSquads.length === 0 && commandElements.length === 0 && allPlayers.length > 0 && (
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
                  <TableHead>Assignment</TableHead>
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
                      <AssignmentCell
                        player={player}
                        platoons={platoons}
                        eventId={eventId!}
                      />
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

// ── Assignment cell — grouped dropdown: command elements, platoon HQ, squads ──

// Value encoding:
//   ""                  = unassigned
//   "platoon:{id}"      = platoon-level HQ slot (calls assignPlayerToPlatoon)
//   "squad:{id}"        = squad assignment (calls assignSquad)

function AssignmentCell({
  player,
  platoons,
  eventId,
}: {
  player: { id: string; squadId: string | null; platoonId: string | null; eventId: string };
  platoons: PlatoonDto[];
  eventId: string;
}) {
  const [open, setOpen] = useState(false);
  const queryClient = useQueryClient();

  const assignSquadMutation = useMutation({
    mutationFn: (squadId: string | null) => api.assignSquad(player.id, squadId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['roster', eventId] });
      setOpen(false);
    },
  });

  const assignPlatoonMutation = useMutation({
    mutationFn: (platoonId: string | null) => api.assignPlayerToPlatoon(player.id, platoonId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['roster', eventId] });
      setOpen(false);
    },
  });

  const isPending = assignSquadMutation.isPending || assignPlatoonMutation.isPending;

  // Determine current display label
  const currentLabel = (() => {
    if (player.squadId) {
      const squad = platoons.flatMap((p) => p.squads).find((s) => s.id === player.squadId);
      return squad?.name ?? null;
    }
    if (player.platoonId) {
      const platoon = platoons.find((p) => p.id === player.platoonId);
      if (platoon?.isCommandElement) return platoon.name;
      if (platoon) return `${platoon.name} HQ`;
    }
    return null;
  })();

  const handleSelect = (value: string) => {
    if (value === '') {
      // Unassign — clear both squad and platoon
      assignPlatoonMutation.mutate(null);
    } else if (value.startsWith('platoon:')) {
      const platoonId = value.slice('platoon:'.length);
      assignPlatoonMutation.mutate(platoonId);
    } else if (value.startsWith('squad:')) {
      const squadId = value.slice('squad:'.length);
      assignSquadMutation.mutate(squadId);
    }
  };

  const isSelected = (value: string) => {
    if (value === '' && !player.squadId && !player.platoonId) return true;
    if (value === `platoon:${player.platoonId}` && !player.squadId) return true;
    if (value === `squad:${player.squadId}`) return true;
    return false;
  };

  const commandElements = platoons.filter((p) => p.isCommandElement);
  const regularPlatoons = platoons.filter((p) => !p.isCommandElement);
  const hasAnyOptions = commandElements.length > 0 || regularPlatoons.some((p) => p.squads.length > 0);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          disabled={isPending}
          className="w-[200px] justify-between text-sm font-normal"
        >
          {currentLabel ?? (
            <span className="text-muted-foreground">Assign…</span>
          )}
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[220px] p-0">
        <Command>
          <CommandInput placeholder="Search…" />
          <CommandList>
            {!hasAnyOptions && (
              <CommandEmpty>No platoons or squads yet.</CommandEmpty>
            )}

            {/* Command elements */}
            {commandElements.length > 0 && (
              <CommandGroup heading="Command">
                {commandElements.map((p) => (
                  <CommandItem
                    key={`platoon:${p.id}`}
                    value={`cmd-${p.name}`}
                    onSelect={() => handleSelect(`platoon:${p.id}`)}
                  >
                    <Check
                      className={cn('mr-2 h-4 w-4', isSelected(`platoon:${p.id}`) ? 'opacity-100' : 'opacity-0')}
                    />
                    <span className="text-amber-600 font-medium">★ {p.name}</span>
                  </CommandItem>
                ))}
              </CommandGroup>
            )}

            {/* Regular platoons — HQ slot + squads */}
            {regularPlatoons.map((p) => (
              <CommandGroup key={p.id} heading={p.name}>
                {/* Platoon HQ synthetic option */}
                <CommandItem
                  value={`${p.name}-hq`}
                  onSelect={() => handleSelect(`platoon:${p.id}`)}
                >
                  <Check
                    className={cn('mr-2 h-4 w-4', isSelected(`platoon:${p.id}`) ? 'opacity-100' : 'opacity-0')}
                  />
                  <span className="italic text-muted-foreground">[Platoon HQ]</span>
                </CommandItem>
                {p.squads.map((s) => (
                  <CommandItem
                    key={`squad:${s.id}`}
                    value={`${p.name}-${s.name}`}
                    onSelect={() => handleSelect(`squad:${s.id}`)}
                  >
                    <Check
                      className={cn('mr-2 h-4 w-4', isSelected(`squad:${s.id}`) ? 'opacity-100' : 'opacity-0')}
                    />
                    {s.name}
                  </CommandItem>
                ))}
              </CommandGroup>
            ))}

            {/* Unassign */}
            {(player.squadId || player.platoonId) && (
              <CommandGroup>
                <CommandItem
                  value="unassign"
                  onSelect={() => handleSelect('')}
                  className="text-muted-foreground"
                >
                  Unassign
                </CommandItem>
              </CommandGroup>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
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
