import { useForm } from 'react-hook-form';
import { useAuth } from '../../hooks/useAuth';
import {
  useFrequencies,
  useUpdateCommandFrequency,
  useUpdatePlatoonFrequency,
  useUpdateSquadFrequency,
} from '../../hooks/useFrequencies';
import { Card, CardHeader, CardTitle, CardContent } from '../ui/card';
import { Input } from '../ui/input';
import { Label } from '../ui/label';
import { Button } from '../ui/button';
import type { UpdateFrequencyRequest } from '../../lib/api';

interface FrequencyFormValues {
  primary: string;
  backup: string;
}

interface FrequencyFormProps {
  label: string;
  defaultPrimary: string | null;
  defaultBackup: string | null;
  onSubmit: (req: UpdateFrequencyRequest) => void;
  isPending: boolean;
}

function FrequencyForm({ label, defaultPrimary, defaultBackup, onSubmit, isPending }: FrequencyFormProps) {
  const { register, handleSubmit } = useForm<FrequencyFormValues>({
    defaultValues: {
      primary: defaultPrimary ?? '',
      backup: defaultBackup ?? '',
    },
  });

  const submit = (values: FrequencyFormValues) => {
    onSubmit({
      primary: values.primary.trim() || null,
      backup: values.backup.trim() || null,
    });
  };

  return (
    <form onSubmit={handleSubmit(submit)} className="space-y-2">
      <div className="text-sm font-medium">{label}</div>
      <div className="grid grid-cols-2 gap-2">
        <div>
          <Label htmlFor={`${label}-primary`}>Primary</Label>
          <Input id={`${label}-primary`} {...register('primary')} placeholder="e.g. 160.000" />
        </div>
        <div>
          <Label htmlFor={`${label}-backup`}>Backup</Label>
          <Input id={`${label}-backup`} {...register('backup')} placeholder="e.g. 161.000" />
        </div>
      </div>
      <Button type="submit" size="sm" disabled={isPending}>
        {isPending ? 'Saving...' : 'Save'}
      </Button>
    </form>
  );
}

interface FrequencyEditorProps {
  eventId: string;
}

export function FrequencyEditor({ eventId }: FrequencyEditorProps) {
  const { user } = useAuth();
  const { data, isLoading, error } = useFrequencies(eventId);
  const commandMutation = useUpdateCommandFrequency(eventId);
  const platoonMutation = useUpdatePlatoonFrequency(eventId);
  const squadMutation = useUpdateSquadFrequency(eventId);

  const role = user?.role;

  if (!role || role === 'player') return null;

  if (isLoading) {
    return <div className="text-sm text-muted-foreground">Loading frequencies...</div>;
  }

  if (error) {
    return <div className="text-sm text-destructive">Failed to load frequencies.</div>;
  }

  if (!data) return null;

  const canEditCommand = role === 'faction_commander' || role === 'system_admin';
  const canEditPlatoons = role === 'faction_commander' || role === 'system_admin' || role === 'platoon_leader';
  const canEditSquads = role === 'faction_commander' || role === 'system_admin' || role === 'platoon_leader' || role === 'squad_leader';

  return (
    <div className="space-y-4">
      {canEditCommand && data.command && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Edit Command Frequency</CardTitle>
          </CardHeader>
          <CardContent>
            <FrequencyForm
              label="Command"
              defaultPrimary={data.command.primary}
              defaultBackup={data.command.backup}
              onSubmit={(req) => commandMutation.mutate(req)}
              isPending={commandMutation.isPending}
            />
          </CardContent>
        </Card>
      )}

      {canEditPlatoons && data.platoons.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Edit Platoon Frequencies</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {data.platoons.map((p) => (
              <FrequencyForm
                key={p.platoonId}
                label={p.platoonName}
                defaultPrimary={p.primary}
                defaultBackup={p.backup}
                onSubmit={(req) => platoonMutation.mutate({ platoonId: p.platoonId, ...req })}
                isPending={platoonMutation.isPending}
              />
            ))}
          </CardContent>
        </Card>
      )}

      {canEditSquads && data.squads.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Edit Squad Frequencies</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {data.squads.map((s) => (
              <FrequencyForm
                key={s.squadId}
                label={s.squadName}
                defaultPrimary={s.primary}
                defaultBackup={s.backup}
                onSubmit={(req) => squadMutation.mutate({ squadId: s.squadId, ...req })}
                isPending={squadMutation.isPending}
              />
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
