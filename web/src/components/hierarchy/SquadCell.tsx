import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '../ui/button';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '../ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '../ui/popover';
import { cn } from '../../lib/utils';
import { getToken } from '../../lib/auth';

interface Squad {
  id: string;
  name: string;
}

interface Props {
  player: { id: string; squadId: string | null; eventId: string };
  squads: Squad[];
}

export function SquadCell({ player, squads }: Props) {
  const [open, setOpen] = useState(false);
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: (squadId: string | null) => {
      const token = getToken();
      const apiBase = (import.meta.env.VITE_API_URL ?? '') + '/api';
      return fetch(`${apiBase}/event-players/${player.id}/squad`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: JSON.stringify({ squadId }),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['roster', player.eventId] });
      setOpen(false);
    },
  });

  const currentSquad = squads.find((s) => s.id === player.squadId);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          className="w-[180px] justify-between text-sm font-normal"
        >
          {currentSquad?.name ?? (
            <span className="text-muted-foreground">Assign squad…</span>
          )}
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[200px] p-0">
        <Command>
          <CommandInput placeholder="Search squads..." />
          <CommandList>
            <CommandEmpty>No squads found.</CommandEmpty>
            <CommandGroup>
              {squads.map((squad) => (
                <CommandItem
                  key={squad.id}
                  value={squad.name}
                  onSelect={() => mutation.mutate(squad.id)}
                >
                  <Check
                    className={cn(
                      'mr-2 h-4 w-4',
                      player.squadId === squad.id ? 'opacity-100' : 'opacity-0'
                    )}
                  />
                  {squad.name}
                </CommandItem>
              ))}
              {player.squadId && (
                <CommandItem
                  key="unassign"
                  value="unassign"
                  onSelect={() => mutation.mutate(null)}
                  className="text-muted-foreground"
                >
                  Unassign
                </CommandItem>
              )}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
