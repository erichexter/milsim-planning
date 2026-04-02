import { useState } from 'react';
import { useParams } from 'react-router';
import {
  LayoutDashboard,
  Users,
  BookOpen,
  Map,
  ArrowLeft,
} from 'lucide-react';
import { PlayerOverviewTab } from '../../components/player/PlayerOverviewTab';
import { ChangeRequestForm } from '../../components/player/ChangeRequestForm';
import { PendingRequestCard } from '../../components/player/PendingRequestCard';
import { RosterView } from '../roster/RosterView';
import { BriefingPage } from './BriefingPage';
import { MapResourcesPage } from './MapResourcesPage';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../lib/api';
import { Button } from '../../components/ui/button';
import { EventBreadcrumb } from '../../components/EventBreadcrumb';
import { FrequencyPanel } from '../../components/frequency/FrequencyPanel';

type TabId = 'overview' | 'roster' | 'briefing' | 'maps' | 'change-request';

const NAV_TABS: { id: TabId; label: string; Icon: React.ComponentType<{ className?: string }> }[] = [
  { id: 'overview',  label: 'Overview',  Icon: LayoutDashboard },
  { id: 'roster',   label: 'Roster',    Icon: Users },
  { id: 'briefing', label: 'Briefing',  Icon: BookOpen },
  { id: 'maps',     label: 'Maps',      Icon: Map },
];

interface ChangeRequestDto {
  id: string;
  note: string;
  status: string;
  commanderNote: string | null;
  createdAt: string;
}

interface AssignmentDto {
  id: string;
  name: string;
  callsign: string | null;
  teamAffiliation: string | null;
  role: string | null;
  platoon: { id: string; name: string } | null;
  squad: { id: string; name: string } | null;
  faction: { id: string; name: string } | null;
  isAssigned: boolean;
}

export function PlayerEventPage() {
  const { id: eventId } = useParams<{ id: string }>();
  const [activeTab, setActiveTab] = useState<TabId>('overview');

  const { data: assignment } = useQuery<AssignmentDto>({
    queryKey: ['events', eventId, 'my-assignment'],
    queryFn: async () => {
      try {
        return await api.get<AssignmentDto>(`/events/${eventId!}/my-assignment`);
      } catch (e: unknown) {
        const err = e as { status?: number };
        if (err?.status === 404)
          return { id: '', name: '', callsign: null, teamAffiliation: null, role: null, platoon: null, squad: null, faction: null, isAssigned: false };
        throw e;
      }
    },
    enabled: !!eventId,
  });

  const { data: myRequest } = useQuery<ChangeRequestDto | null>({
    queryKey: ['events', eventId, 'roster-change-requests', 'mine'],
    queryFn: async () => {
      try {
        return await api.get<ChangeRequestDto>(`/events/${eventId!}/roster-change-requests/mine`);
      } catch (e: unknown) {
        const err = e as { status?: number };
        if (err?.status === 204 || err?.status === 404) return null;
        throw e;
      }
    },
    enabled: !!eventId,
  });

  function renderContent() {
    switch (activeTab) {
      case 'overview':
        return (
          <>
            <PlayerOverviewTab
              eventId={eventId!}
              onNavigate={(tab) => setActiveTab(tab)}
            />
            {assignment?.isAssigned && (
              <div className="mx-auto max-w-3xl lg:max-w-5xl px-5 pb-5">
                <FrequencyPanel
                  role={assignment.role ?? 'player'}
                  squadId={assignment.squad?.id ?? null}
                  platoonId={assignment.platoon?.id ?? null}
                  factionId={assignment.faction?.id ?? null}
                />
              </div>
            )}
          </>
        );
      case 'roster':
        return <RosterView />;
      case 'briefing':
        return <BriefingPage />;
      case 'maps':
        return <MapResourcesPage />;
      case 'change-request':
        return <ChangeRequestTab eventId={eventId!} request={myRequest ?? null} onBack={() => setActiveTab('overview')} />;
    }
  }

  // Only show icon sidebar for non-change-request tabs
  const showSidebar = activeTab !== 'change-request';

  return (
    <div className="flex h-full min-h-0">
      {/* ── Desktop icon sidebar (52px) ─────────────────────────────────── */}
      {showSidebar && (
        <nav
          className="hidden md:flex flex-col items-center gap-1 border-r bg-card shrink-0 py-3"
          style={{ width: 52 }}
        >
          {NAV_TABS.map(({ id, label, Icon }) => {
            const active = activeTab === id;
            return (
              <button
                key={id}
                title={label}
                onClick={() => setActiveTab(id)}
                className={`
                  flex h-10 w-10 items-center justify-center rounded-[10px] transition-colors
                  ${active
                    ? 'text-primary'
                    : 'text-muted-foreground hover:text-foreground hover:bg-muted'
                  }
                `}
                style={active ? { backgroundColor: 'oklch(var(--primary-soft))' } : undefined}
                aria-current={active ? 'page' : undefined}
                aria-label={label}
              >
                <Icon className="h-4 w-4" />
              </button>
            );
          })}
        </nav>
      )}

      {/* ── Content area ──────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto pb-16 md:pb-0 rp0-scroll">
        {renderContent()}
      </div>

      {/* ── Mobile bottom tab bar ─────────────────────────────────────────── */}
      {showSidebar && (
        <nav className="md:hidden fixed bottom-0 left-0 right-0 border-t bg-card z-50 flex">
          {NAV_TABS.map(({ id, label, Icon }) => {
            const active = activeTab === id;
            return (
              <button
                key={id}
                onClick={() => setActiveTab(id)}
                className={`flex-1 flex flex-col items-center justify-center gap-0.5 min-h-[56px] text-[10px] font-mono transition-colors ${
                  active
                    ? 'text-primary'
                    : 'text-muted-foreground hover:text-foreground'
                }`}
              >
                <Icon className="h-4 w-4" />
                {label}
              </button>
            );
          })}
        </nav>
      )}
    </div>
  );
}

// ── Change Request sub-page ───────────────────────────────────────────────

interface ChangeRequestTabProps {
  eventId: string;
  request: ChangeRequestDto | null;
  onBack: () => void;
}

function ChangeRequestTab({ eventId, request, onBack }: ChangeRequestTabProps) {
  const hasPending = request?.status === 'Pending';

  return (
    <div className="mx-auto max-w-2xl space-y-6 p-6">
      <EventBreadcrumb eventId={eventId} page="Change Request" />

      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" className="gap-1.5 -ml-2" onClick={onBack}>
          <ArrowLeft className="h-4 w-4" />
          Back to Overview
        </Button>
      </div>

      <div>
        <h1 className="text-xl font-semibold">Change Request</h1>
        <p className="text-sm text-muted-foreground mt-1">
          Use this form to request a change to your assignment. Your commander will review it.
        </p>
      </div>

      {hasPending ? (
        <PendingRequestCard eventId={eventId} request={request!} />
      ) : (
        <ChangeRequestForm eventId={eventId} isUnassigned={false} />
      )}
    </div>
  );
}
