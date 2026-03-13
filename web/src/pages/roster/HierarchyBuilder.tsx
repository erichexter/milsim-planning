import { useMemo } from 'react';
import { useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../lib/api';
import { SquadCell } from '../../components/hierarchy/SquadCell';
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

  const { data: roster } = useQuery({
    queryKey: ['roster', eventId],
    queryFn: () => api.getRoster(eventId!),
  });

  // Collect all players (assigned + unassigned) with their current squad
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

  // Collect all squads flat
  const allSquads = useMemo(
    () => roster?.platoons.flatMap((p) => p.squads) ?? [],
    [roster]
  );

  // Group players by TeamAffiliation (raw string — no normalization)
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

  return (
    <div className="p-6 max-w-5xl mx-auto space-y-8">
      <h1 className="text-2xl font-bold">Hierarchy Builder</h1>
      <p className="text-sm text-muted-foreground">
        Players are grouped by their Team Affiliation from the CSV. Assign each player to a squad
        using the dropdown.
      </p>

      {[...playersByAffiliation.entries()].map(([affiliation, players]) => (
        <div key={affiliation}>
          <h2 className="text-lg font-semibold mb-2">{affiliation}</h2>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Callsign</TableHead>
                <TableHead>Name</TableHead>
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
                    <SquadCell player={player} squads={allSquads} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ))}
    </div>
  );
}
