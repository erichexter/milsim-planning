import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from './ui/card';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { Label } from './ui/label';
import type {
  EventFrequenciesDto,
  FrequencyPairDto,
  PlatoonFrequencyDto,
  SquadFrequencyDto,
  UpdateFrequencyRequest,
} from '../lib/api';

interface FrequencyDisplayProps {
  data: EventFrequenciesDto;
  canEditCommand?: boolean;
  canEditPlatoon?: boolean;
  canEditSquad?: boolean;
  onUpdateCommand?: (body: UpdateFrequencyRequest) => void;
  onUpdatePlatoon?: (platoonId: string, body: UpdateFrequencyRequest) => void;
  onUpdateSquad?: (squadId: string, body: UpdateFrequencyRequest) => void;
}

function FrequencyPair({ label, pair }: { label: string; pair: FrequencyPairDto }) {
  return (
    <div className="text-sm">
      <span className="font-medium">{label}:</span>{' '}
      <span className="text-muted-foreground">
        {pair.primary ?? 'Not set'}
        {pair.backup ? ` / ${pair.backup}` : ''}
      </span>
    </div>
  );
}

function EditableFrequency({
  label,
  primary,
  backup,
  onSave,
}: {
  label: string;
  primary: string | null;
  backup: string | null;
  onSave: (body: UpdateFrequencyRequest) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [primaryVal, setPrimaryVal] = useState(primary ?? '');
  const [backupVal, setBackupVal] = useState(backup ?? '');

  if (!editing) {
    return (
      <div className="flex items-center justify-between text-sm">
        <div>
          <span className="font-medium">{label}:</span>{' '}
          <span className="text-muted-foreground">
            {primary ?? 'Not set'}
            {backup ? ` / ${backup}` : ''}
          </span>
        </div>
        <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
          Edit
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-2 border rounded-md p-3">
      <div className="font-medium text-sm">{label}</div>
      <div className="grid grid-cols-2 gap-2">
        <div>
          <Label htmlFor={`${label}-primary`}>Primary</Label>
          <Input
            id={`${label}-primary`}
            value={primaryVal}
            onChange={(e) => setPrimaryVal(e.target.value)}
            placeholder="e.g. 148.000"
          />
        </div>
        <div>
          <Label htmlFor={`${label}-backup`}>Backup</Label>
          <Input
            id={`${label}-backup`}
            value={backupVal}
            onChange={(e) => setBackupVal(e.target.value)}
            placeholder="e.g. 149.000"
          />
        </div>
      </div>
      <div className="flex gap-2">
        <Button
          size="sm"
          onClick={() => {
            onSave({
              primary: primaryVal || null,
              backup: backupVal || null,
            });
            setEditing(false);
          }}
        >
          Save
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            setPrimaryVal(primary ?? '');
            setBackupVal(backup ?? '');
            setEditing(false);
          }}
        >
          Cancel
        </Button>
      </div>
    </div>
  );
}

export function FrequencyDisplay({
  data,
  canEditCommand = false,
  canEditPlatoon = false,
  canEditSquad = false,
  onUpdateCommand,
  onUpdatePlatoon,
  onUpdateSquad,
}: FrequencyDisplayProps) {
  const hasAny =
    data.command !== null || data.platoons.length > 0 || data.squads.length > 0;

  if (!hasAny) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Radio Frequencies</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">No frequencies available for your role.</p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Radio Frequencies</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {data.command !== null && (
          <div>
            <h4 className="text-sm font-semibold mb-1">Command</h4>
            {canEditCommand && onUpdateCommand ? (
              <EditableFrequency
                label="Command"
                primary={data.command.primary}
                backup={data.command.backup}
                onSave={onUpdateCommand}
              />
            ) : (
              <FrequencyPair label="Command" pair={data.command} />
            )}
          </div>
        )}

        {data.platoons.length > 0 && (
          <div>
            <h4 className="text-sm font-semibold mb-1">Platoons</h4>
            <div className="space-y-2">
              {data.platoons.map((p: PlatoonFrequencyDto) =>
                canEditPlatoon && onUpdatePlatoon ? (
                  <EditableFrequency
                    key={p.platoonId}
                    label={p.platoonName}
                    primary={p.primary}
                    backup={p.backup}
                    onSave={(body) => onUpdatePlatoon(p.platoonId, body)}
                  />
                ) : (
                  <FrequencyPair key={p.platoonId} label={p.platoonName} pair={p} />
                )
              )}
            </div>
          </div>
        )}

        {data.squads.length > 0 && (
          <div>
            <h4 className="text-sm font-semibold mb-1">Squads</h4>
            <div className="space-y-2">
              {data.squads.map((s: SquadFrequencyDto) =>
                canEditSquad && onUpdateSquad ? (
                  <EditableFrequency
                    key={s.squadId}
                    label={s.squadName}
                    primary={s.primary}
                    backup={s.backup}
                    onSave={(body) => onUpdateSquad(s.squadId, body)}
                  />
                ) : (
                  <FrequencyPair key={s.squadId} label={s.squadName} pair={s} />
                )
              )}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
