import { Radio } from 'lucide-react';
import { Card, CardContent } from '../ui/card';
import { FrequencyEditor } from './FrequencyEditor';
import {
  useSquadFrequency,
  usePlatoonFrequency,
  useFactionFrequency,
  useUpdateSquadFrequency,
  useUpdatePlatoonFrequency,
  useUpdateFactionFrequency,
} from '../../hooks/useFrequencies';

interface Props {
  role: string;
  squadId: string | null;
  platoonId: string | null;
  factionId: string | null;
}

// Roles that can write squad frequencies
const SQUAD_WRITERS = new Set(['squad_leader', 'platoon_leader', 'faction_commander', 'system_admin']);
// Roles that can write platoon frequencies
const PLATOON_WRITERS = new Set(['platoon_leader', 'faction_commander', 'system_admin']);
// Roles that can write faction frequencies
const FACTION_WRITERS = new Set(['faction_commander', 'system_admin']);

// Role visibility rules
function showsSquad(role: string) {
  return ['player', 'squad_leader', 'platoon_leader', 'faction_commander', 'system_admin'].includes(role);
}
function showsPlatoon(role: string) {
  return ['squad_leader', 'platoon_leader', 'faction_commander', 'system_admin'].includes(role);
}
function showsFaction(role: string) {
  return ['platoon_leader', 'faction_commander', 'system_admin'].includes(role);
}

// ── Sub-component: one frequency row ────────────────────────────────────────

interface FrequencyRowProps {
  label: string;
  primary: string | null | undefined;
  backup: string | null | undefined;
  isLoading: boolean;
  canEdit: boolean;
  editControl: React.ReactNode;
}

function FrequencyRow({ label, primary, backup, isLoading, canEdit, editControl }: FrequencyRowProps) {
  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between gap-2">
        <span className="rp0-label">{label}</span>
        {canEdit && editControl}
      </div>
      {isLoading ? (
        <p className="text-xs text-muted-foreground">Loading…</p>
      ) : (
        <div className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1">
          <span className="rp0-label self-center text-[10px]">Primary</span>
          <span className="font-mono text-sm">{primary ?? <span className="text-muted-foreground">—</span>}</span>
          <span className="rp0-label self-center text-[10px]">Backup</span>
          <span className="font-mono text-sm">{backup ?? <span className="text-muted-foreground">—</span>}</span>
        </div>
      )}
    </div>
  );
}

// ── Squad frequency row ──────────────────────────────────────────────────────

function SquadRow({ squadId, canEdit }: { squadId: string; canEdit: boolean }) {
  const { data, isLoading } = useSquadFrequency(squadId);
  const mutation = useUpdateSquadFrequency(squadId);

  return (
    <FrequencyRow
      label="Squad"
      primary={data?.primary}
      backup={data?.backup}
      isLoading={isLoading}
      canEdit={canEdit}
      editControl={
        <FrequencyEditor
          label="Squad"
          currentPrimary={data?.primary ?? null}
          currentBackup={data?.backup ?? null}
          mutation={mutation}
        />
      }
    />
  );
}

// ── Platoon frequency row ────────────────────────────────────────────────────

function PlatoonRow({ platoonId, canEdit }: { platoonId: string; canEdit: boolean }) {
  const { data, isLoading } = usePlatoonFrequency(platoonId);
  const mutation = useUpdatePlatoonFrequency(platoonId);

  return (
    <FrequencyRow
      label="Platoon"
      primary={data?.primary}
      backup={data?.backup}
      isLoading={isLoading}
      canEdit={canEdit}
      editControl={
        <FrequencyEditor
          label="Platoon"
          currentPrimary={data?.primary ?? null}
          currentBackup={data?.backup ?? null}
          mutation={mutation}
        />
      }
    />
  );
}

// ── Faction frequency row ────────────────────────────────────────────────────

function FactionRow({ factionId, canEdit }: { factionId: string; canEdit: boolean }) {
  const { data, isLoading } = useFactionFrequency(factionId);
  const mutation = useUpdateFactionFrequency(factionId);

  return (
    <FrequencyRow
      label="Faction"
      primary={data?.primary}
      backup={data?.backup}
      isLoading={isLoading}
      canEdit={canEdit}
      editControl={
        <FrequencyEditor
          label="Faction"
          currentPrimary={data?.primary ?? null}
          currentBackup={data?.backup ?? null}
          mutation={mutation}
        />
      }
    />
  );
}

// ── Main panel ───────────────────────────────────────────────────────────────

export function FrequencyPanel({ role, squadId, platoonId, factionId }: Props) {
  const hasSquad = showsSquad(role) && !!squadId;
  const hasPlatoon = showsPlatoon(role) && !!platoonId;
  const hasFaction = showsFaction(role) && !!factionId;

  if (!hasSquad && !hasPlatoon && !hasFaction) return null;

  return (
    <Card>
      <CardContent className="p-4 space-y-4">
        {/* Card header */}
        <div className="flex items-center gap-2">
          <span
            className="h-7 w-7 rounded-[8px] flex items-center justify-center shrink-0"
            style={{
              backgroundColor: 'oklch(var(--primary-soft))',
              border: '1px solid oklch(var(--primary-border))',
            }}
          >
            <Radio className="h-3.5 w-3.5" style={{ color: 'oklch(var(--primary))' }} />
          </span>
          <span className="rp0-label">Radio Frequencies</span>
        </div>

        {/* Squad row */}
        {hasSquad && (
          <SquadRow
            squadId={squadId}
            canEdit={SQUAD_WRITERS.has(role)}
          />
        )}

        {/* Divider between rows */}
        {hasSquad && hasPlatoon && <div className="border-t" />}

        {/* Platoon row */}
        {hasPlatoon && (
          <PlatoonRow
            platoonId={platoonId}
            canEdit={PLATOON_WRITERS.has(role)}
          />
        )}

        {/* Divider between rows */}
        {(hasSquad || hasPlatoon) && hasFaction && <div className="border-t" />}

        {/* Faction row */}
        {hasFaction && (
          <FactionRow
            factionId={factionId}
            canEdit={FACTION_WRITERS.has(role)}
          />
        )}
      </CardContent>
    </Card>
  );
}
