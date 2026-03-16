import { useMemo, useState, useRef, useEffect } from 'react';
import { useParams } from 'react-router';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Check, ChevronsUpDown, ChevronRight, ChevronDown } from 'lucide-react';
import { api } from '../../lib/api';
import type { PlatoonDto } from '../../lib/api';
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

// ── Types ──────────────────────────────────────────────────────────────────────

type EnrichedPlayer = {
  id: string;
  name: string;
  callsign: string | null;
  teamAffiliation: string | null;
  role: string | null;
  squadId: string | null;
  platoonId: string | null;
  eventId: string;
};

// ── Fill dots component ────────────────────────────────────────────────────────

function FillDots({ count }: { count: number }) {
  const DOTS = 10;
  const filled = Math.min(count, DOTS);
  const empty = Math.max(0, DOTS - count);
  const overflow = count > DOTS ? count - DOTS : 0;

  return (
    <span className="inline-flex items-center gap-0.5 font-mono text-[10px]">
      {Array.from({ length: filled }).map((_, i) => (
        <span key={`f${i}`} style={{ color: 'oklch(var(--primary))' }}>●</span>
      ))}
      {Array.from({ length: empty }).map((_, i) => (
        <span key={`e${i}`} className="text-muted-foreground opacity-25">●</span>
      ))}
      {overflow > 0 && (
        <span className="ml-0.5 text-muted-foreground">+{overflow}</span>
      )}
      <span className="ml-1.5 text-muted-foreground">{count}</span>
    </span>
  );
}

// ── Main component ─────────────────────────────────────────────────────────────

export function HierarchyBuilder() {
  const { id: eventId } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const { data: roster } = useQuery({
    queryKey: ['roster', eventId],
    queryFn: () => api.getRoster(eventId!),
  });

  // ── Create platoon ──────────────────────────────────────────────────────────
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

  // ── Set player role ─────────────────────────────────────────────────────────
  const setRoleMutation = useMutation({
    mutationFn: ({ playerId, role }: { playerId: string; role: string | null }) =>
      api.setPlayerRole(playerId, role),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['roster', eventId] }),
  });

  // ── Create squad ────────────────────────────────────────────────────────────
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

  // ── Derived data ────────────────────────────────────────────────────────────
  const allPlayers = useMemo((): EnrichedPlayer[] => {
    if (!roster) return [];
    const assigned = roster.platoons.flatMap((p) => [
      ...p.hqPlayers.map((pl) => ({
        ...pl,
        squadId: null as string | null,
        platoonId: p.id,
        eventId: eventId!,
      })),
      ...p.squads.flatMap((s) =>
        s.players.map((pl) => ({ ...pl, squadId: s.id, platoonId: p.id, eventId: eventId! }))
      ),
    ]);
    const unassigned = roster.unassignedPlayers.map((pl) => ({
      ...pl,
      squadId: null as string | null,
      platoonId: null as string | null,
      eventId: eventId!,
    }));
    return [...assigned, ...unassigned];
  }, [roster, eventId]);

  const unassignedPlayers = useMemo(
    () => allPlayers.filter((p) => p.squadId === null && p.platoonId === null),
    [allPlayers]
  );

  const assignedPlayers = useMemo(
    () => allPlayers.filter((p) => p.squadId !== null || p.platoonId !== null),
    [allPlayers]
  );

  // Unassigned grouped by team affiliation
  const unassignedByAffiliation = useMemo(() => {
    const groups = new Map<string, EnrichedPlayer[]>();
    for (const player of unassignedPlayers) {
      const key = player.teamAffiliation ?? '(No Team)';
      const group = groups.get(key) ?? [];
      group.push(player);
      groups.set(key, group);
    }
    return groups;
  }, [unassignedPlayers]);

  if (!roster) return <div className="p-6">Loading hierarchy...</div>;

  const platoons = roster.platoons;
  const commandElements = platoons.filter((p) => p.isCommandElement);
  const regularPlatoons = platoons.filter((p) => !p.isCommandElement);

  const handleRoleSave = (playerId: string, role: string | null) =>
    setRoleMutation.mutate({ playerId, role });

  return (
    <div className="p-6 max-w-5xl mx-auto space-y-8">
      <EventBreadcrumb eventId={eventId!} page="Hierarchy" />
      <h1 className="text-xl font-semibold">Hierarchy Builder</h1>

      {/* ── Structure panel ──────────────────────────────────────────────── */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">

        {/* Create platoon / command element */}
        <div className="border rounded-[12px] p-4 space-y-3">
          <h2 className="font-medium text-sm">Create Platoon / Command Element</h2>
          <div className="flex gap-2">
            <Input
              placeholder={isCommandElement ? 'e.g. Battalion HQ' : 'e.g. Alpha Platoon'}
              value={newPlatoonName}
              onChange={(e) => setNewPlatoonName(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && newPlatoonName.trim())
                  createPlatoonMutation.mutate({ name: newPlatoonName.trim(), isCommandElement });
              }}
            />
            <Button
              onClick={() => createPlatoonMutation.mutate({ name: newPlatoonName.trim(), isCommandElement })}
              disabled={!newPlatoonName.trim() || createPlatoonMutation.isPending}
            >
              Add
            </Button>
          </div>
          <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
            <input
              type="checkbox"
              checked={isCommandElement}
              onChange={(e) => setIsCommandElement(e.target.checked)}
              className="rounded"
            />
            <span>Command Element <span className="text-muted-foreground text-xs">(no squads — for CO, XO, etc.)</span></span>
          </label>

          {commandElements.length > 0 && (
            <div className="space-y-1">
              <p className="rp0-label">Command Elements</p>
              <ul className="text-sm space-y-1">
                {commandElements.map((p) => (
                  <li key={p.id} className="font-medium" style={{ color: 'oklch(var(--accent))' }}>
                    ★ {p.name}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {regularPlatoons.length > 0 && (
            <div className="space-y-1">
              {commandElements.length > 0 && <p className="rp0-label">Platoons</p>}
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

        {/* Create squad */}
        <div className="border rounded-[12px] p-4 space-y-3">
          <h2 className="font-medium text-sm">Create Squad</h2>
          {regularPlatoons.length === 0 ? (
            <p className="text-sm text-muted-foreground">Create a platoon first.</p>
          ) : (
            <>
              <select
                className="w-full rounded-[8px] border border-input bg-background px-3 py-2 text-sm"
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
                    if (e.key === 'Enter' && newSquadName.trim() && selectedPlatoonId)
                      createSquadMutation.mutate({ platoonId: selectedPlatoonId, name: newSquadName.trim() });
                  }}
                />
                <Button
                  onClick={() => createSquadMutation.mutate({ platoonId: selectedPlatoonId, name: newSquadName.trim() })}
                  disabled={!newSquadName.trim() || !selectedPlatoonId || createSquadMutation.isPending}
                >
                  Add
                </Button>
              </div>
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

      {/* ── Unassigned players ────────────────────────────────────────────── */}
      <section className="space-y-4">
        <div className="flex items-center gap-3">
          <h2 className="text-base font-semibold">Unassigned</h2>
          <span className="font-mono text-xs text-muted-foreground">
            {unassignedPlayers.length} player{unassignedPlayers.length !== 1 ? 's' : ''}
          </span>
        </div>

        {allPlayers.length === 0 && (
          <p className="text-sm text-muted-foreground">No players yet. Import a roster first.</p>
        )}

        {allPlayers.length > 0 && platoons.length === 0 && (
          <p className="text-sm mb-2" style={{ color: 'oklch(var(--accent))' }}>
            Create platoons and squads above before assigning players.
          </p>
        )}

        {unassignedPlayers.length === 0 && allPlayers.length > 0 ? (
          <p className="text-sm text-muted-foreground">All players have been assigned.</p>
        ) : (
          [...unassignedByAffiliation.entries()].map(([affiliation, players]) => (
            <div key={affiliation}>
              <div className="flex items-center gap-2 mb-1">
                <span className="text-sm font-semibold">{affiliation}</span>
                <span className="font-mono text-xs text-muted-foreground">{players.length}</span>
              </div>
              <PlayerTable
                players={players}
                platoons={platoons}
                eventId={eventId!}
                onRoleSave={handleRoleSave}
              />
            </div>
          ))
        )}
      </section>

      {/* ── Assigned players — platoon / squad tree ───────────────────────── */}
      {assignedPlayers.length > 0 && (
        <section className="space-y-4">
          <div className="flex items-center gap-3">
            <h2 className="text-base font-semibold">Assigned</h2>
            <span className="font-mono text-xs text-muted-foreground">
              {assignedPlayers.length} player{assignedPlayers.length !== 1 ? 's' : ''}
            </span>
          </div>

          <div className="space-y-5">
            {platoons.map((platoon) => {
              const hqPlayers: EnrichedPlayer[] = assignedPlayers.filter(
                (p) => p.platoonId === platoon.id && p.squadId === null
              );
              const platoonTotal = assignedPlayers.filter(
                (p) => p.platoonId === platoon.id
              ).length;

              if (platoonTotal === 0) return null;

              return (
                <div key={platoon.id}>
                  {/* Platoon header */}
                  <div className="flex items-center gap-3 pb-2 border-b mb-3">
                    {platoon.isCommandElement ? (
                      <span className="text-sm font-semibold" style={{ color: 'oklch(var(--accent))' }}>
                        ★ {platoon.name}
                      </span>
                    ) : (
                      <span className="text-sm font-semibold">{platoon.name}</span>
                    )}
                    <span className="font-mono text-xs text-muted-foreground">
                      {platoonTotal} player{platoonTotal !== 1 ? 's' : ''}
                    </span>
                  </div>

                  <div className="space-y-2 pl-3">
                    {/* HQ / command element players — collapsible */}
                    {hqPlayers.length > 0 && (
                      <SquadBlock
                        label={platoon.isCommandElement ? platoon.name : `${platoon.name} HQ`}
                        players={hqPlayers}
                        platoons={platoons}
                        eventId={eventId!}
                        onRoleSave={handleRoleSave}
                      />
                    )}

                    {/* Squad blocks — collapsible */}
                    {platoon.squads.map((squad) => {
                      const squadPlayers = assignedPlayers.filter(
                        (p) => p.squadId === squad.id
                      );
                      return (
                        <SquadBlock
                          key={squad.id}
                          label={squad.name}
                          players={squadPlayers}
                          platoons={platoons}
                          eventId={eventId!}
                          onRoleSave={handleRoleSave}
                        />
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>
        </section>
      )}
    </div>
  );
}

// ── SquadBlock — collapsible squad/HQ row with fill dots + player table ────────

function SquadBlock({
  label,
  players,
  platoons,
  eventId,
  onRoleSave,
}: {
  label: string;
  players: EnrichedPlayer[];
  platoons: PlatoonDto[];
  eventId: string;
  onRoleSave: (playerId: string, role: string | null) => void;
}) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="border rounded-[10px] overflow-hidden">
      {/* Squad header row — always visible */}
      <button
        type="button"
        className="w-full flex items-center gap-3 px-4 py-2.5 text-left hover:bg-muted/40 transition-colors"
        onClick={() => setExpanded((v) => !v)}
      >
        {expanded
          ? <ChevronDown className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
          : <ChevronRight className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
        }
        <span className="text-sm font-medium min-w-[120px]">{label}</span>
        <FillDots count={players.length} />
      </button>

      {/* Expanded player table */}
      {expanded && players.length > 0 && (
        <div className="border-t">
          <PlayerTable
            players={players}
            platoons={platoons}
            eventId={eventId}
            onRoleSave={onRoleSave}
            compact
          />
        </div>
      )}

      {expanded && players.length === 0 && (
        <p className="text-xs text-muted-foreground px-4 py-3 border-t">No players assigned.</p>
      )}
    </div>
  );
}

// ── PlayerTable — shared by both unassigned and assigned sections ──────────────

function PlayerTable({
  players,
  platoons,
  eventId,
  onRoleSave,
  compact = false,
}: {
  players: EnrichedPlayer[];
  platoons: PlatoonDto[];
  eventId: string;
  onRoleSave: (playerId: string, role: string | null) => void;
  compact?: boolean;
}) {
  return (
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
          <TableRow key={player.id} className={compact ? 'text-xs' : ''}>
            <TableCell className="font-mono font-bold" style={{ color: 'oklch(var(--primary))' }}>
              [{player.callsign ?? '—'}]
            </TableCell>
            <TableCell>{player.name}</TableCell>
            <TableCell>
              <RoleCell
                playerId={player.id}
                role={player.role}
                onSave={(role) => onRoleSave(player.id, role)}
              />
            </TableCell>
            <TableCell>
              <AssignmentCell
                player={player}
                platoons={platoons}
                eventId={eventId}
              />
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

// ── AssignmentCell ─────────────────────────────────────────────────────────────

function AssignmentCell({
  player,
  platoons,
  eventId,
}: {
  player: EnrichedPlayer;
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
      assignPlatoonMutation.mutate(null);
    } else if (value.startsWith('platoon:')) {
      assignPlatoonMutation.mutate(value.slice('platoon:'.length));
    } else if (value.startsWith('squad:')) {
      assignSquadMutation.mutate(value.slice('squad:'.length));
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
          className="w-[180px] justify-between text-sm font-normal"
        >
          {currentLabel ?? <span className="text-muted-foreground">Assign…</span>}
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[220px] p-0">
        <Command>
          <CommandInput placeholder="Search…" />
          <CommandList>
            {!hasAnyOptions && <CommandEmpty>No platoons or squads yet.</CommandEmpty>}

            {commandElements.length > 0 && (
              <CommandGroup heading="Command">
                {commandElements.map((p) => (
                  <CommandItem
                    key={`platoon:${p.id}`}
                    value={`cmd-${p.name}`}
                    onSelect={() => handleSelect(`platoon:${p.id}`)}
                  >
                    <Check className={cn('mr-2 h-4 w-4', isSelected(`platoon:${p.id}`) ? 'opacity-100' : 'opacity-0')} />
                    <span className="font-medium" style={{ color: 'oklch(var(--accent))' }}>★ {p.name}</span>
                  </CommandItem>
                ))}
              </CommandGroup>
            )}

            {regularPlatoons.map((p) => (
              <CommandGroup key={p.id} heading={p.name}>
                <CommandItem
                  value={`${p.name}-hq`}
                  onSelect={() => handleSelect(`platoon:${p.id}`)}
                >
                  <Check className={cn('mr-2 h-4 w-4', isSelected(`platoon:${p.id}`) ? 'opacity-100' : 'opacity-0')} />
                  <span className="italic text-muted-foreground">[Platoon HQ]</span>
                </CommandItem>
                {p.squads.map((s) => (
                  <CommandItem
                    key={`squad:${s.id}`}
                    value={`${p.name}-${s.name}`}
                    onSelect={() => handleSelect(`squad:${s.id}`)}
                  >
                    <Check className={cn('mr-2 h-4 w-4', isSelected(`squad:${s.id}`) ? 'opacity-100' : 'opacity-0')} />
                    {s.name}
                  </CommandItem>
                ))}
              </CommandGroup>
            ))}

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

// ── RoleCell ───────────────────────────────────────────────────────────────────

function RoleCell({
  playerId: _playerId,
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
      onClick={() => setEditing(true)}
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
