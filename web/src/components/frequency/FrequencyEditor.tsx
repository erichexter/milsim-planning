import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import type { EventFrequenciesDto } from '../../lib/api';
import {
  useUpdateSquadFrequency,
  useUpdatePlatoonFrequency,
  useUpdateFactionFrequency,
} from '../../hooks/useUpdateFrequency';
import { Button } from '../ui/button';
import { Input } from '../ui/input';
import { Label } from '../ui/label';

interface FrequencyEditorProps {
  eventId: string;
  frequencies: EventFrequenciesDto;
  onSaved?: () => void;
}

const frequencySchema = z.object({
  squadPrimary: z.string().nullable(),
  squadBackup: z.string().nullable(),
  platoonPrimary: z.string().nullable(),
  platoonBackup: z.string().nullable(),
  commandPrimary: z.string().nullable(),
  commandBackup: z.string().nullable(),
});

type FrequencyForm = z.infer<typeof frequencySchema>;

export function FrequencyEditor({ eventId, frequencies, onSaved }: FrequencyEditorProps) {
  const { squad, platoon, command } = frequencies;

  const updateSquad = useUpdateSquadFrequency(eventId);
  const updatePlatoon = useUpdatePlatoonFrequency(eventId);
  const updateFaction = useUpdateFactionFrequency(eventId);

  const {
    register,
    handleSubmit,
    formState: { isSubmitting },
  } = useForm<FrequencyForm>({
    resolver: zodResolver(frequencySchema),
    defaultValues: {
      squadPrimary: squad?.primary ?? null,
      squadBackup: squad?.backup ?? null,
      platoonPrimary: platoon?.primary ?? null,
      platoonBackup: platoon?.backup ?? null,
      commandPrimary: command?.primary ?? null,
      commandBackup: command?.backup ?? null,
    },
  });

  const onSubmit = async (data: FrequencyForm) => {
    const mutations: Promise<unknown>[] = [];

    if (squad) {
      mutations.push(
        updateSquad.mutateAsync({
          squadId: squad.id,
          req: { primary: data.squadPrimary || null, backup: data.squadBackup || null },
        })
      );
    }
    if (platoon) {
      mutations.push(
        updatePlatoon.mutateAsync({
          platoonId: platoon.id,
          req: { primary: data.platoonPrimary || null, backup: data.platoonBackup || null },
        })
      );
    }
    if (command) {
      mutations.push(
        updateFaction.mutateAsync({
          factionId: command.id,
          req: { primary: data.commandPrimary || null, backup: data.commandBackup || null },
        })
      );
    }

    await Promise.all(mutations);
    onSaved?.();
  };

  const mutationError =
    updateSquad.error?.message ||
    updatePlatoon.error?.message ||
    updateFaction.error?.message;

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      {squad && (
        <div className="space-y-3">
          <h4 className="font-medium text-sm">{squad.name}</h4>
          <div className="space-y-1">
            <Label htmlFor="squadPrimary">Primary Frequency</Label>
            <Input
              id="squadPrimary"
              type="text"
              {...register('squadPrimary')}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="squadBackup">Backup Frequency</Label>
            <Input
              id="squadBackup"
              type="text"
              {...register('squadBackup')}
            />
          </div>
        </div>
      )}

      {platoon && (
        <div className="space-y-3">
          <h4 className="font-medium text-sm">{platoon.name}</h4>
          <div className="space-y-1">
            <Label htmlFor="platoonPrimary">Primary Frequency</Label>
            <Input
              id="platoonPrimary"
              type="text"
              {...register('platoonPrimary')}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="platoonBackup">Backup Frequency</Label>
            <Input
              id="platoonBackup"
              type="text"
              {...register('platoonBackup')}
            />
          </div>
        </div>
      )}

      {command && (
        <div className="space-y-3">
          <h4 className="font-medium text-sm">{command.name}</h4>
          <div className="space-y-1">
            <Label htmlFor="commandPrimary">Primary Frequency</Label>
            <Input
              id="commandPrimary"
              type="text"
              {...register('commandPrimary')}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="commandBackup">Backup Frequency</Label>
            <Input
              id="commandBackup"
              type="text"
              {...register('commandBackup')}
            />
          </div>
        </div>
      )}

      {mutationError && (
        <p className="text-sm text-red-500">{mutationError}</p>
      )}

      <Button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Saving…' : 'Save Frequencies'}
      </Button>
    </form>
  );
}
