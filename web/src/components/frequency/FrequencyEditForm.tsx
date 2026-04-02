import { useForm } from 'react-hook-form';
import { useMutation } from '@tanstack/react-query';
import { api, UpdateFrequencyRequest } from '../../lib/api';
import { Button } from '../ui/button';

interface FrequencyEditFormProps {
  levelId: string;
  levelType: 'squad' | 'platoon' | 'command';
  levelName: string;
  onSuccess: () => void;
}

interface FormValues {
  primary: string;
  backup: string;
}

export function FrequencyEditForm({ levelId, levelType, levelName, onSuccess }: FrequencyEditFormProps) {
  const { register, handleSubmit } = useForm<FormValues>({
    defaultValues: { primary: '', backup: '' },
  });

  const mutation = useMutation({
    mutationFn: (values: FormValues) => {
      const body: UpdateFrequencyRequest = {
        primary: values.primary.trim() || null,
        backup: values.backup.trim() || null,
      };
      if (levelType === 'squad') return api.updateSquadFrequencies(levelId, body);
      if (levelType === 'platoon') return api.updatePlatoonFrequencies(levelId, body);
      return api.updateCommandFrequencies(levelId, body);
    },
    onSuccess,
  });

  return (
    <form
      onSubmit={handleSubmit((values) => mutation.mutate(values))}
      className="space-y-3"
      aria-label={`Edit ${levelName} frequencies`}
    >
      <div className="space-y-2">
        <label className="text-sm font-medium" htmlFor={`primary-${levelId}`}>
          Primary Frequency
        </label>
        <input
          id={`primary-${levelId}`}
          {...register('primary')}
          className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          placeholder="e.g. 148.000 MHz"
        />
      </div>
      <div className="space-y-2">
        <label className="text-sm font-medium" htmlFor={`backup-${levelId}`}>
          Backup Frequency
        </label>
        <input
          id={`backup-${levelId}`}
          {...register('backup')}
          className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          placeholder="e.g. 149.000 MHz"
        />
      </div>
      {mutation.isError && (
        <p className="text-sm text-destructive">Failed to save frequencies. Please try again.</p>
      )}
      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={mutation.isPending}>
          {mutation.isPending ? 'Saving...' : 'Save'}
        </Button>
        <Button type="button" variant="outline" size="sm" onClick={onSuccess}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
