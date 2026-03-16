import { useState } from 'react';
import { useParams } from 'react-router';
import { PlayerOverviewTab } from '../../components/player/PlayerOverviewTab';
import { RosterView } from '../roster/RosterView';
import { BriefingPage } from './BriefingPage';
import { MapResourcesPage } from './MapResourcesPage';

type TabId = 'overview' | 'roster' | 'briefing' | 'maps';

const TABS: { id: TabId; label: string }[] = [
  { id: 'overview',  label: 'Overview'  },
  { id: 'roster',   label: 'Roster'    },
  { id: 'briefing', label: 'Briefing'  },
  { id: 'maps',     label: 'Maps'      },
];

export function PlayerEventPage() {
  const { id: eventId } = useParams<{ id: string }>();
  const [activeTab, setActiveTab] = useState<TabId>('overview');

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
    }
  }

  return (
    <div className="flex flex-col">
      {/* ── Desktop top nav ──────────────────────────────────────────────── */}
      <nav className="hidden md:flex border-b bg-background shrink-0">
        {TABS.map((tab) => (
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

      {/* ── Mobile bottom tab bar ─────────────────────────────────────────── */}
      <nav className="md:hidden fixed bottom-0 left-0 right-0 border-t bg-background z-50 flex">
        {TABS.map((tab) => (
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
