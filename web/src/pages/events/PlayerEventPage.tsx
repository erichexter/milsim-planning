import { useState } from 'react';
import { useParams } from 'react-router';
import { MyAssignmentTab } from '../../components/player/MyAssignmentTab';
import { RosterView } from '../roster/RosterView';
import { BriefingPage } from './BriefingPage';
import { MapResourcesPage } from './MapResourcesPage';

type TabId = 'assignment' | 'roster' | 'briefing' | 'maps';

const TABS: { id: TabId; label: string }[] = [
  { id: 'assignment', label: 'My Assignment' },
  { id: 'roster', label: 'Roster' },
  { id: 'briefing', label: 'Briefing' },
  { id: 'maps', label: 'Maps' },
];

export function PlayerEventPage() {
  const { id: eventId } = useParams<{ id: string }>();
  const [activeTab, setActiveTab] = useState<TabId>('assignment');

  function renderContent() {
    switch (activeTab) {
      case 'assignment':
        return (
          <div className="mx-auto max-w-4xl space-y-6 p-6">
            <MyAssignmentTab eventId={eventId!} />
          </div>
        );
      case 'roster':
        // RosterView uses useParams internally (reads 'id') — works because we're under /events/:id
        return <RosterView />;
      case 'briefing':
        // BriefingPage reads useParams for 'id' or 'eventId'
        return <BriefingPage />;
      case 'maps':
        return <MapResourcesPage />;
    }
  }

  return (
    <div className="flex flex-col">
      {/* ── Desktop top nav (hidden on mobile) ─────────────────────────── */}
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

      {/* ── Content area ─────────────────────────────────────────────────── */}
      <div className="pb-16 md:pb-0">
        {renderContent()}
      </div>

      {/* ── Mobile bottom tab bar (hidden on desktop) ─────────────────── */}
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
