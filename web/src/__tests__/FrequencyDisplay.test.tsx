import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Routes, Route } from 'react-router';
import { FrequencyDisplay } from '../components/FrequencyDisplay';

// ── Fixtures ──────────────────────────────────────────────────────────────────

const EVENT_ID = 'evt-1';
const SQUAD_ID = 'sq-1';
const PLATOON_ID = 'plat-1';
const FACTION_ID = 'fac-1';

const playerResponse = {
  squad: { squadId: SQUAD_ID, squadName: 'Alpha', primary: '151.000', backup: '151.250' },
  platoon: null,
  command: null,
  allFrequencies: null,
};

const squadLeaderResponse = {
  squad: { squadId: SQUAD_ID, squadName: 'Alpha', primary: '151.000', backup: '151.250' },
  platoon: { platoonId: PLATOON_ID, platoonName: '1st Platoon', primary: '148.000', backup: '148.250' },
  command: null,
  allFrequencies: null,
};

const platoonLeaderResponse = {
  squad: null,
  platoon: { platoonId: PLATOON_ID, platoonName: '1st Platoon', primary: '148.000', backup: '148.250' },
  command: { factionId: FACTION_ID, factionName: 'Test Faction', primary: '155.500', backup: '155.750' },
  allFrequencies: null,
};

const commanderResponse = {
  squad: null,
  platoon: null,
  command: { factionId: FACTION_ID, factionName: 'Test Faction', primary: '155.500', backup: '155.750' },
  allFrequencies: {
    command: { factionId: FACTION_ID, factionName: 'Test Faction', primary: '155.500', backup: '155.750' },
    platoons: [
      { platoonId: PLATOON_ID, platoonName: '1st Platoon', primary: '148.000', backup: '148.250' },
    ],
    squads: [
      { squadId: SQUAD_ID, squadName: 'Alpha', platoonName: '1st Platoon', primary: '151.000', backup: '151.250' },
    ],
  },
};

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeQc() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

// Mock useAuth to return different roles
const mockUser = { id: 'u1', email: 'test@test.com', role: 'player', callsign: 'TEST' };
vi.mock('../hooks/useAuth', () => ({
  useAuth: () => ({ user: mockUser, isAuthenticated: true, login: vi.fn(), logout: vi.fn() }),
}));

function renderComponent(apiResponse: unknown = playerResponse) {
  server.use(
    http.get('/api/events/:eventId/frequencies', () => HttpResponse.json(apiResponse))
  );

  render(
    <QueryClientProvider client={makeQc()}>
      <MemoryRouter initialEntries={[`/events/${EVENT_ID}/frequencies`]}>
        <Routes>
          <Route path="/events/:id/frequencies" element={<FrequencyDisplay />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('FrequencyDisplay', () => {
  it('renders squad frequency for player role', async () => {
    mockUser.role = 'player';
    renderComponent(playerResponse);

    await waitFor(() => {
      expect(screen.getByTestId('freq-squad')).toBeInTheDocument();
    });
    expect(screen.getByText('Squad: Alpha')).toBeInTheDocument();
    expect(screen.getByText('151.000 / 151.250')).toBeInTheDocument();
    expect(screen.queryByTestId('freq-platoon')).not.toBeInTheDocument();
    expect(screen.queryByTestId('freq-command')).not.toBeInTheDocument();
    expect(screen.queryByTestId('freq-all')).not.toBeInTheDocument();
  });

  it('renders squad + platoon for squad_leader', async () => {
    mockUser.role = 'squad_leader';
    renderComponent(squadLeaderResponse);

    await waitFor(() => {
      expect(screen.getByTestId('freq-squad')).toBeInTheDocument();
    });
    expect(screen.getByTestId('freq-platoon')).toBeInTheDocument();
    expect(screen.getByText('Platoon: 1st Platoon')).toBeInTheDocument();
    expect(screen.queryByTestId('freq-command')).not.toBeInTheDocument();
    expect(screen.queryByTestId('freq-all')).not.toBeInTheDocument();
  });

  it('renders platoon + command for platoon_leader, no squad', async () => {
    mockUser.role = 'platoon_leader';
    renderComponent(platoonLeaderResponse);

    await waitFor(() => {
      expect(screen.getByTestId('freq-platoon')).toBeInTheDocument();
    });
    expect(screen.getByTestId('freq-command')).toBeInTheDocument();
    expect(screen.getByText('Command: Test Faction')).toBeInTheDocument();
    expect(screen.queryByTestId('freq-squad')).not.toBeInTheDocument();
    expect(screen.queryByTestId('freq-all')).not.toBeInTheDocument();
  });

  it('renders all levels with full overview for faction_commander', async () => {
    mockUser.role = 'faction_commander';
    renderComponent(commanderResponse);

    await waitFor(() => {
      expect(screen.getByTestId('freq-command')).toBeInTheDocument();
    });
    expect(screen.getByTestId('freq-all')).toBeInTheDocument();
    expect(screen.getByText('All Frequencies Overview')).toBeInTheDocument();
    expect(screen.queryByTestId('freq-squad')).not.toBeInTheDocument();
    expect(screen.queryByTestId('freq-platoon')).not.toBeInTheDocument();
  });

  it('shows edit button for squad leader on squad frequency', async () => {
    mockUser.role = 'squad_leader';
    renderComponent(squadLeaderResponse);

    await waitFor(() => {
      expect(screen.getByTestId('freq-squad')).toBeInTheDocument();
    });
    // Squad leader can edit squad
    const editButtons = screen.getAllByText('Edit');
    expect(editButtons.length).toBeGreaterThanOrEqual(1);
  });

  it('does not show edit buttons for player role', async () => {
    mockUser.role = 'player';
    renderComponent(playerResponse);

    await waitFor(() => {
      expect(screen.getByTestId('freq-squad')).toBeInTheDocument();
    });
    expect(screen.queryByText('Edit')).not.toBeInTheDocument();
  });

  it('shows edit form when Edit is clicked', async () => {
    mockUser.role = 'faction_commander';
    renderComponent(commanderResponse);

    await waitFor(() => {
      expect(screen.getByTestId('freq-command')).toBeInTheDocument();
    });

    const editButtons = screen.getAllByText('Edit');
    fireEvent.click(editButtons[0]);

    expect(screen.getByText('Save')).toBeInTheDocument();
    expect(screen.getByText('Cancel')).toBeInTheDocument();
  });

  it('commander sees edit controls for command and all frequencies', async () => {
    mockUser.role = 'faction_commander';
    renderComponent(commanderResponse);

    await waitFor(() => {
      expect(screen.getByTestId('freq-all')).toBeInTheDocument();
    });

    // Commander can edit: command + each platoon in overview + each squad in overview
    const editButtons = screen.getAllByText('Edit');
    // command edit + 1 platoon edit + 1 squad edit = 3 minimum
    expect(editButtons.length).toBeGreaterThanOrEqual(3);
  });

  it('platoon_leader does not see command edit controls', async () => {
    mockUser.role = 'platoon_leader';
    renderComponent(platoonLeaderResponse);

    await waitFor(() => {
      expect(screen.getByTestId('freq-command')).toBeInTheDocument();
    });

    // Platoon leader can edit platoon, NOT command
    const freqCommand = screen.getByTestId('freq-command');
    const freqPlatoon = screen.getByTestId('freq-platoon');

    // platoon has edit button
    expect(freqPlatoon.querySelector('button')).not.toBeNull();
    // command does NOT have edit button
    expect(freqCommand.querySelector('button')).toBeNull();
  });

  it('shows loading state initially', () => {
    server.use(
      http.get('/api/events/:eventId/frequencies', async () => {
        await new Promise(() => {}); // never resolves
      })
    );

    render(
      <QueryClientProvider client={makeQc()}>
        <MemoryRouter initialEntries={[`/events/${EVENT_ID}/frequencies`]}>
          <Routes>
            <Route path="/events/:id/frequencies" element={<FrequencyDisplay />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );

    expect(screen.getByTestId('freq-loading')).toBeInTheDocument();
  });
});
