import { useState } from 'react';
import { useParams } from 'react-router';
import { ArrowLeft } from 'lucide-react';
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

type TabId = 'overview' | 'roster' | 'briefing' | 'maps' | 'change-request';

const NAV_TABS: { id: TabId; label: string }[] = [
  { id: 'overview',  label: 'Overview'  },
  { id: 'roster',   label: 'Roster'    },
  { id: 'briefing', label: 'Briefing'  },
  { id: 'maps',     label: 'Maps'      },
];

interface ChangeRequestDto {
  id: string;
  note: string;
  status: string;
  commanderNote: string | null;
  createdAt: string;
}

export function PlayerEventPage() {
  const { id: eventId } = useParams<{ id: string }>();
  const [activeTab, setActiveTab] = useState<TabId>('overview');

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
          <PlayerOverviewTab
            eventId={eventId!}
            onNavigate={(tab) => setActiveTab(tab)}
          />
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

  return (
    <div className="flex flex-col">
      {/* ── Desktop top nav — change-request tab is hidden from nav bar ── */}
      <nav className="hidden md:flex border-b bg-background shrink-0">
        {NAV_TABS.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === tab.id
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </nav>

      {/* ── Content ──────────────────────────────────────────────────────── */}
      <div className="pb-16 md:pb-0">
        {renderContent()}
      </div>

      {/* ── Mobile bottom tab bar — change-request hidden here too ───────── */}
      <nav className="md:hidden fixed bottom-0 left-0 right-0 border-t bg-background z-50 flex">
        {NAV_TABS.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`flex-1 flex flex-col items-center justify-center min-h-[56px] text-xs font-medium transition-colors ${
              activeTab === tab.id
                ? 'text-primary'
                : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </nav>
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
        <h1 className="text-2xl font-bold">Change Request</h1>
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
