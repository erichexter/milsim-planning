import { useState, useMemo, useEffect } from 'react';
import { useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../lib/api';
import type { PlatoonDto, PlayerDto } from '../../lib/api';
import { EventBreadcrumb } from '../../components/EventBreadcrumb';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '../../components/ui/accordion';
import { Input } from '../../components/ui/input';

export function RosterView() {
  const { id: eventId } = useParams<{ id: string }>();
  const [search, setSearch] = useState('');
  // type="multiple" with controlled state — prevents accordion collapse on re-render
  const [openPlatoons, setOpenPlatoons] = useState<string[]>([]);

  const { data: roster } = useQuery({
    queryKey: ['roster', eventId],
    queryFn: () => api.getRoster(eventId!),
  });

  // Initialize all platoons open once data loads
  useEffect(() => {
    if (roster && openPlatoons.length === 0) {
      setOpenPlatoons(roster.platoons.map((p) => p.id));
    }
  }, [roster]); // eslint-disable-line react-hooks/exhaustive-deps

  const q = search.toLowerCase().trim();

  const playerMatchesSearch = (p: PlayerDto) =>
    !q ||
    p.name.toLowerCase().includes(q) ||
    (p.callsign ?? '').toLowerCase().includes(q) ||
    (p.role ?? '').toLowerCase().includes(q);

  // Client-side search filter — 400 players max, no server round-trip needed
  const filtered = useMemo(() => {
    if (!roster) return null;
    if (!q) return roster;
    return {
      ...roster,
      platoons: roster.platoons
        .map((platoon) => ({
          ...platoon,
          hqPlayers: platoon.hqPlayers.filter(playerMatchesSearch),
          squads: platoon.squads
            .map((squad) => ({
              ...squad,
              players: squad.players.filter(playerMatchesSearch),
            }))
            .filter((s) => s.players.length > 0),
        }))
        .filter((p) => p.hqPlayers.length > 0 || p.squads.length > 0),
    };
  }, [roster, q]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!filtered) return <div className="p-6">Loading roster...</div>;

  const commandElements = filtered.platoons.filter((p) => p.isCommandElement);
  const regularPlatoons = filtered.platoons.filter((p) => !p.isCommandElement);

  return (
    <div className="p-6 max-w-3xl mx-auto space-y-4">
      <EventBreadcrumb eventId={eventId!} page="Roster" />
      <h1 className="text-2xl font-bold">Faction Roster</h1>

      <Input
        placeholder="Search by name, callsign, or role..."
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="max-w-sm"
      />

      {/* ── Command Element section — always at top ───────────────────────── */}
      {commandElements.map((platoon) => (
        <CommandElementSection key={platoon.id} platoon={platoon} />
      ))}

      {/* ── Regular platoon accordion ──────────────────────────────────────── */}
      <Accordion type="multiple" value={openPlatoons} onValueChange={setOpenPlatoons}>
        {regularPlatoons.map((platoon) => {
          const playerCount =
            platoon.hqPlayers.length +
            platoon.squads.flatMap((s) => s.players).length;
          return (
            <AccordionItem key={platoon.id} value={platoon.id}>
              <AccordionTrigger>
                {platoon.name}
                <span className="ml-2 text-muted-foreground text-sm">({playerCount} players)</span>
              </AccordionTrigger>
              <AccordionContent>
                {/* Platoon HQ players */}
                {platoon.hqPlayers.length > 0 && (
                  <div className="ml-4 mb-4">
                    <h4 className="font-semibold text-sm mb-2 text-muted-foreground">Platoon HQ</h4>
                    <div className="space-y-1">
                      {platoon.hqPlayers.map((player) => (
                        <PlayerRow key={player.id} player={player} />
                      ))}
                    </div>
                  </div>
                )}
                {/* Squads */}
                {platoon.squads.map((squad) => (
                  <div key={squad.id} className="ml-4 mb-4">
                    <h4 className="font-semibold text-sm mb-2">{squad.name}</h4>
                    <div className="space-y-1">
                      {squad.players.map((player) => (
                        <PlayerRow key={player.id} player={player} />
                      ))}
                    </div>
                  </div>
                ))}
              </AccordionContent>
            </AccordionItem>
          );
        })}
      </Accordion>

      {/* ── Unassigned ────────────────────────────────────────────────────── */}
      {filtered.unassignedPlayers.filter(playerMatchesSearch).length > 0 && (
        <div className="border rounded-lg p-4">
          <h3 className="font-semibold mb-2">
            Unassigned ({filtered.unassignedPlayers.filter(playerMatchesSearch).length})
          </h3>
          <div className="space-y-1">
            {filtered.unassignedPlayers
              .filter(playerMatchesSearch)
              .map((player) => (
                <PlayerRow key={player.id} player={player} />
              ))}
          </div>
        </div>
      )}
    </div>
  );
}

// ── Command Element section ────────────────────────────────────────────────────

function CommandElementSection({ platoon }: { platoon: PlatoonDto }) {
  return (
    <div className="border-2 border-amber-500/40 rounded-lg p-4 bg-amber-50/30 dark:bg-amber-950/10">
      <h3 className="font-bold text-sm uppercase tracking-wide text-amber-600 mb-3">
        ★ {platoon.name}
      </h3>
      {platoon.hqPlayers.length === 0 ? (
        <p className="text-sm text-muted-foreground italic">No players assigned.</p>
      ) : (
        <div className="space-y-1">
          {platoon.hqPlayers.map((player) => (
            <PlayerRow key={player.id} player={player} />
          ))}
        </div>
      )}
    </div>
  );
}

// ── Shared player row ─────────────────────────────────────────────────────────

function PlayerRow({ player }: { player: PlayerDto }) {
  return (
    <div className="flex items-center gap-3 text-sm">
      {/* HIER-06 / PLAY-06: callsign prominently displayed */}
      <span className="font-mono font-bold text-orange-500 min-w-[80px]">
        [{player.callsign ?? '—'}]
      </span>
      <span>{player.name}</span>
      {player.role && (
        <span className="text-muted-foreground text-xs italic">
          {player.role}
        </span>
      )}
      {player.teamAffiliation && (
        <span className="text-muted-foreground text-xs">
          {player.teamAffiliation}
        </span>
      )}
    </div>
  );
}
