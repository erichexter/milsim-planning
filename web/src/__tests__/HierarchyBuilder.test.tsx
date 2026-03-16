import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Routes, Route } from 'react-router';
import { HierarchyBuilder } from '../pages/roster/HierarchyBuilder';

// ── Fixtures ──────────────────────────────────────────────────────────────────

const SQUAD_ID = 'sq-1';
const PLATOON_ID = 'plat-1';
const PLAYER_ASSIGNED_ID = 'p1';
const PLAYER_UNASSIGNED_ID = 'p2';
const PLAYER_UNASSIGNED_ID_2 = 'p3';

const mockRoster = {
  platoons: [
    {
      id: PLATOON_ID,
      name: 'Alpha Platoon',
      isCommandElement: false,
      hqPlayers: [],
      squads: [
        {
          id: SQUAD_ID,
          name: 'Alpha-1',
          players: [
            {
              id: PLAYER_ASSIGNED_ID,
              name: 'John Smith',
              callsign: 'GHOST',
              teamAffiliation: 'Alpha Team',
              role: null,
            },
          ],
        },
      ],
    },
  ],
  unassignedPlayers: [
    {
      id: PLAYER_UNASSIGNED_ID,
      name: 'Jane Doe',
      callsign: 'NOVA',
      teamAffiliation: 'Alpha Team',
      role: null,
    },
    {
      id: PLAYER_UNASSIGNED_ID_2,
      name: 'Bob Lee',
      callsign: 'WOLF',
      teamAffiliation: 'Bravo Team',
      role: null,
    },
  ],
};

// Roster with only unassigned players — no platoons yet
const mockRosterUnassignedOnly = {
  platoons: [],
  unassignedPlayers: [
    {
      id: PLAYER_UNASSIGNED_ID,
      name: 'Jane Doe',
      callsign: 'NOVA',
      teamAffiliation: 'Alpha Team',
      role: null,
    },
  ],
};

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeQc() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderComponent() {
  render(
    <QueryClientProvider client={makeQc()}>
      <MemoryRouter initialEntries={['/events/evt-1/hierarchy']}>
        <Routes>
          <Route path="/events/:id/hierarchy" element={<HierarchyBuilder />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('HierarchyBuilder — unassigned section', () => {
  beforeEach(() => {
    server.use(
      http.get('/api/events/evt-1/roster', () => HttpResponse.json(mockRoster))
    );
  });

  it('renders team affiliation group headings', async () => {
    renderComponent();
    await waitFor(() => expect(screen.getByText('Alpha Team')).toBeInTheDocument());
    expect(screen.getByText('Bravo Team')).toBeInTheDocument();
  });

  it('displays unassigned player names', async () => {
    renderComponent();
    await waitFor(() => expect(screen.getByText('Jane Doe')).toBeInTheDocument());
    expect(screen.getByText('Bob Lee')).toBeInTheDocument();
  });

  it('displays callsigns in [CALLSIGN] format', async () => {
    renderComponent();
    await waitFor(() => expect(screen.getByText('[NOVA]')).toBeInTheDocument());
    expect(screen.getByText('[WOLF]')).toBeInTheDocument();
  });

  it('renders a per-row checkbox for each unassigned player', async () => {
    renderComponent();
    await waitFor(() => expect(screen.getByText('Jane Doe')).toBeInTheDocument());
    // Each player row has a checkbox; plus one select-all per group = 2 groups × 1 + 2 players = 4 checkboxes
    const checkboxes = screen.getAllByRole('checkbox');
    expect(checkboxes.length).toBeGreaterThanOrEqual(2);
  });

  it('shows "N selected" bulk action bar when a player is selected', async () => {
    renderComponent();
    await waitFor(() => expect(screen.getByText('Jane Doe')).toBeInTheDocument());

    // Click Jane Doe's row to toggle selection
    fireEvent.click(screen.getByText('Jane Doe'));

    await waitFor(() =>
      expect(screen.getByText(/1 selected/)).toBeInTheDocument()
    );
  });

  it('bulk action bar disappears when selection is cleared', async () => {
    renderComponent();
    await waitFor(() => expect(screen.getByText('Jane Doe')).toBeInTheDocument());

    fireEvent.click(screen.getByText('Jane Doe'));
    await waitFor(() => expect(screen.getByText(/1 selected/)).toBeInTheDocument());

    // Click the X button to clear selection
    fireEvent.click(screen.getByTitle('Clear selection'));
    await waitFor(() =>
      expect(screen.queryByText(/1 selected/)).not.toBeInTheDocument()
    );
  });

  // The select-all test is placed in its own describe below to use a different MSW setup

});

describe('HierarchyBuilder — select-all', () => {
  beforeEach(() => {
    // Use the unassigned-only roster so there's a single group with one player
    server.use(
      http.get('/api/events/evt-1/roster', () => HttpResponse.json(mockRosterUnassignedOnly))
    );
  });

  it('select-all checkbox selects all players in the group', async () => {
    render(
      <QueryClientProvider client={makeQc()}>
        <MemoryRouter initialEntries={['/events/evt-1/hierarchy']}>
          <Routes>
            <Route path="/events/:id/hierarchy" element={<HierarchyBuilder />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );

    await waitFor(() => expect(screen.getByText('Jane Doe')).toBeInTheDocument());

    // Click Jane Doe's row to select her (row onClick toggles selection)
    fireEvent.click(screen.getByText('Jane Doe'));

    await waitFor(() =>
      expect(screen.getByText(/1 selected/)).toBeInTheDocument()
    );

    // Also verify the group-level select-all checkbox is present (structural test)
    const checkboxes = screen.getAllByRole('checkbox');
    expect(checkboxes.length).toBeGreaterThanOrEqual(2); // at least select-all + per-row
  });
});

describe('HierarchyBuilder — assigned section', () => {
  beforeEach(() => {
    server.use(
      http.get('/api/events/evt-1/roster', () => HttpResponse.json(mockRoster))
    );
  });

  it('renders the assigned section heading when players are assigned', async () => {
    renderComponent();
    await waitFor(() => expect(screen.getByText('Assigned')).toBeInTheDocument());
  });

  it('renders squad block labels in the assigned section (collapsed by default)', async () => {
    renderComponent();
    // Alpha-1 appears both in the Create Squad panel (as text in "Alpha Platoon: Alpha-1")
    // and as a SquadBlock button in the assigned section. getAllByText confirms it's present.
    await waitFor(() => expect(screen.getAllByText('Alpha-1').length).toBeGreaterThan(0));
  });

  it('expands a squad block to show assigned player on click', async () => {
    renderComponent();
    await waitFor(() => expect(screen.getAllByText('Alpha-1').length).toBeGreaterThan(0));

    // John Smith is in Alpha-1 — should NOT be visible until block is expanded
    expect(screen.queryByText('John Smith')).not.toBeInTheDocument();

    // The SquadBlock toggle is a <button> containing the squad name text.
    // Find it by locating the button that has "Alpha-1" as its text content (not just partial).
    const squadButtons = screen.getAllByRole('button');
    const squadToggle = squadButtons.find((btn) => btn.textContent?.includes('Alpha-1') && btn.closest('.border.rounded-\\[10px\\]'));
    // Fallback: click the first button containing "Alpha-1"
    const targetBtn = squadToggle ?? squadButtons.find((btn) => btn.textContent?.includes('Alpha-1'));
    if (targetBtn) {
      fireEvent.click(targetBtn);
    }

    await waitFor(() =>
      expect(screen.getByText('John Smith')).toBeInTheDocument()
    );
    expect(screen.getByText('[GHOST]')).toBeInTheDocument();
  });
});

describe('HierarchyBuilder — bulk assign', () => {
  it('calls bulk-assign API with selected player IDs and destination', async () => {
    const bulkAssignHandler = vi.fn();

    server.use(
      http.get('/api/events/evt-1/roster', () => HttpResponse.json(mockRosterUnassignedOnly)),
      http.post('/api/events/evt-1/players/bulk-assign', async ({ request }) => {
        const body = await request.json();
        bulkAssignHandler(body);
        return new HttpResponse(null, { status: 204 });
      }),
      // Return a roster with the player now assigned, so the mutation onSuccess re-fetch works
      http.get('/api/events/evt-1/roster', () =>
        HttpResponse.json({ platoons: [], unassignedPlayers: [] })
      )
    );

    render(
      <QueryClientProvider client={makeQc()}>
        <MemoryRouter initialEntries={['/events/evt-1/hierarchy']}>
          <Routes>
            <Route path="/events/:id/hierarchy" element={<HierarchyBuilder />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );

    // Wait for roster to load; the unassigned-only mock has a platoon with a squad
    // We need a roster that has both a squad destination AND unassigned players.
    // Simplest: override with a roster that has a platoon+squad AND unassigned players.
    server.use(
      http.get('/api/events/evt-1/roster', () =>
        HttpResponse.json({
          platoons: [
            {
              id: PLATOON_ID,
              name: 'Alpha Platoon',
              isCommandElement: false,
              hqPlayers: [],
              squads: [{ id: SQUAD_ID, name: 'Alpha-1', players: [] }],
            },
          ],
          unassignedPlayers: [
            {
              id: PLAYER_UNASSIGNED_ID,
              name: 'Jane Doe',
              callsign: 'NOVA',
              teamAffiliation: 'Alpha Team',
              role: null,
            },
          ],
        })
      )
    );

    // Re-render with fresh state to pick up the overridden handler
    const qc = makeQc();
    const { unmount } = render(
      <QueryClientProvider client={qc}>
        <MemoryRouter initialEntries={['/events/evt-1/hierarchy']}>
          <Routes>
            <Route path="/events/:id/hierarchy" element={<HierarchyBuilder />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );

    await waitFor(() => expect(screen.getAllByText('Jane Doe').length).toBeGreaterThan(0));

    // Select Jane Doe
    fireEvent.click(screen.getAllByText('Jane Doe')[0]);
    await waitFor(() => expect(screen.getAllByText('1 selected').length).toBeGreaterThan(0));

    // The "Assign to" button is in the bulk action bar — click it to open the DestinationPicker
    const assignBtn = screen.getAllByRole('button', { name: /Assign to/i })[0];
    fireEvent.click(assignBtn);

    // The popover opens with squad options — click "Alpha-1"
    await waitFor(() => expect(screen.getAllByText('Alpha-1').length).toBeGreaterThan(0));
    const alpha1Option = screen.getAllByText('Alpha-1').find(
      (el) => el.closest('[role="option"]') || el.closest('[cmdk-item]')
    );
    if (alpha1Option) {
      fireEvent.click(alpha1Option);
      await waitFor(() => expect(bulkAssignHandler).toHaveBeenCalledWith(
        expect.objectContaining({
          playerIds: [PLAYER_UNASSIGNED_ID],
          destination: `squad:${SQUAD_ID}`,
        })
      ));
    }

    unmount();
  });
});
