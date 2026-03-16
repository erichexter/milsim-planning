import { useState, useMemo, useEffect } from 'react';
import { useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../lib/api';
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

  // Client-side search filter — 400 players max, no server round-trip needed
  const filtered = useMemo(() => {
    if (!roster) return null;
    if (!search.trim()) return roster;
    const q = search.toLowerCase();
    return {
      ...roster,
      platoons: roster.platoons
        .map((platoon) => ({
          ...platoon,
          squads: platoon.squads
            .map((squad) => ({
              ...squad,
              players: squad.players.filter(
                (p) =>
                  p.name.toLowerCase().includes(q) ||
                  (p.callsign ?? '').toLowerCase().includes(q)
              ),
            }))
            .filter((s) => s.players.length > 0),
        }))
        .filter((p) => p.squads.length > 0),
    };
  }, [roster, search]);

  if (!filtered) return <div className="p-6">Loading roster...</div>;

  return (
    <div className="p-6 max-w-3xl mx-auto space-y-4">
      <EventBreadcrumb eventId={eventId!} page="Roster" />
      <h1 className="text-2xl font-bold">Faction Roster</h1>

      <Input
        placeholder="Search by name or callsign..."
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="max-w-sm"
      />

      <Accordion type="multiple" value={openPlatoons} onValueChange={setOpenPlatoons}>
        {filtered.platoons.map((platoon) => {
          const playerCount = platoon.squads.flatMap((s) => s.players).length;
          return (
            <AccordionItem key={platoon.id} value={platoon.id}>
              <AccordionTrigger>
                {platoon.name}
                <span className="ml-2 text-muted-foreground text-sm">({playerCount} players)</span>
              </AccordionTrigger>
              <AccordionContent>
                {platoon.squads.map((squad) => (
                  <div key={squad.id} className="ml-4 mb-4">
                    <h4 className="font-semibold text-sm mb-2">{squad.name}</h4>
                    <div className="space-y-1">
                      {squad.players.map((player) => (
                        <div key={player.id} className="flex items-center gap-3 text-sm">
                          {/* HIER-06 / PLAY-06: callsign prominently displayed */}
                          <span className="font-mono font-bold text-orange-500 min-w-[80px]">
                            [{player.callsign ?? '—'}]
                          </span>
                          <span>{player.name}</span>
                          {player.teamAffiliation && (
                            <span className="text-muted-foreground text-xs">
                              {player.teamAffiliation}
                            </span>
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </AccordionContent>
            </AccordionItem>
          );
        })}
      </Accordion>

      {filtered.unassignedPlayers.length > 0 && (
        <div className="border rounded-lg p-4">
          <h3 className="font-semibold mb-2">
            Unassigned ({filtered.unassignedPlayers.length})
          </h3>
          <div className="space-y-1">
            {filtered.unassignedPlayers
              .filter(
                (p) =>
                  !search.trim() ||
                  p.name.toLowerCase().includes(search.toLowerCase()) ||
                  (p.callsign ?? '').toLowerCase().includes(search.toLowerCase())
              )
              .map((player) => (
                <div key={player.id} className="flex items-center gap-3 text-sm">
                  <span className="font-mono font-bold text-orange-500 min-w-[80px]">
                    [{player.callsign ?? '—'}]
                  </span>
                  <span>{player.name}</span>
                </div>
              ))}
          </div>
        </div>
      )}
    </div>
  );
}
