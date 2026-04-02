import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { FrequencyPanel } from '../../components/frequency/FrequencyPanel';

const SQUAD_ID = 'aaaaaaaa-0000-0000-0000-000000000001';
const PLATOON_ID = 'bbbbbbbb-0000-0000-0000-000000000002';
const FACTION_ID = 'cccccccc-0000-0000-0000-000000000003';

function renderPanel(props: { role: string; squadId: string | null; platoonId: string | null; factionId: string | null }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <FrequencyPanel {...props} />
    </QueryClientProvider>
  );
}

describe('FrequencyPanel', () => {
  beforeEach(() => {
    server.use(
      http.get(`/api/squads/${SQUAD_ID}/frequencies`, () =>
        HttpResponse.json({ squadId: SQUAD_ID, primary: '45.500 MHz', backup: null })
      ),
      http.get(`/api/platoons/${PLATOON_ID}/frequencies`, () =>
        HttpResponse.json({ platoonId: PLATOON_ID, primary: '46.750 MHz', backup: null })
      ),
      http.get(`/api/factions/${FACTION_ID}/frequencies`, () =>
        HttpResponse.json({ factionId: FACTION_ID, primary: '47.000 MHz', backup: '48.250 MHz' })
      ),
    );
  });

  it('renders squad frequency row for player role', async () => {
    renderPanel({ role: 'player', squadId: SQUAD_ID, platoonId: null, factionId: null });
    await waitFor(() => expect(screen.getByText('Squad')).toBeInTheDocument());
    expect(screen.queryByText('Platoon')).not.toBeInTheDocument();
    expect(screen.queryByText('Faction')).not.toBeInTheDocument();
  });

  it('renders squad + platoon rows for squad_leader role', async () => {
    renderPanel({ role: 'squad_leader', squadId: SQUAD_ID, platoonId: PLATOON_ID, factionId: null });
    await waitFor(() => expect(screen.getByText('Squad')).toBeInTheDocument());
    expect(screen.getByText('Platoon')).toBeInTheDocument();
    expect(screen.queryByText('Faction')).not.toBeInTheDocument();
  });

  it('renders platoon + faction rows for platoon_leader role', async () => {
    renderPanel({ role: 'platoon_leader', squadId: null, platoonId: PLATOON_ID, factionId: FACTION_ID });
    await waitFor(() => expect(screen.getByText('Platoon')).toBeInTheDocument());
    expect(screen.getByText('Faction')).toBeInTheDocument();
    expect(screen.queryByText('Squad')).not.toBeInTheDocument();
  });

  it('renders all three rows for faction_commander role', async () => {
    renderPanel({ role: 'faction_commander', squadId: SQUAD_ID, platoonId: PLATOON_ID, factionId: FACTION_ID });
    await waitFor(() => expect(screen.getByText('Squad')).toBeInTheDocument());
    expect(screen.getByText('Platoon')).toBeInTheDocument();
    expect(screen.getByText('Faction')).toBeInTheDocument();
  });

  it('does NOT render FrequencyEditor for player role (no Edit button)', async () => {
    renderPanel({ role: 'player', squadId: SQUAD_ID, platoonId: null, factionId: null });
    await waitFor(() => expect(screen.getByText('Squad')).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument();
  });

  it('renders FrequencyEditor for squad_leader role on squad row', async () => {
    renderPanel({ role: 'squad_leader', squadId: SQUAD_ID, platoonId: PLATOON_ID, factionId: null });
    await waitFor(() => expect(screen.getByText('Squad')).toBeInTheDocument());
    // squad row has an edit button; platoon row should NOT (squad_leader can't write platoon)
    const editButtons = screen.getAllByRole('button', { name: /edit squad frequency/i });
    expect(editButtons).toHaveLength(1);
    expect(screen.queryByRole('button', { name: /edit platoon frequency/i })).not.toBeInTheDocument();
  });

  it('returns null when role has no visible rows given null IDs', () => {
    const { container } = renderPanel({ role: 'player', squadId: null, platoonId: null, factionId: null });
    expect(container.firstChild).toBeNull();
  });

  it('displays fetched primary frequency value', async () => {
    renderPanel({ role: 'player', squadId: SQUAD_ID, platoonId: null, factionId: null });
    await waitFor(() => expect(screen.getByText('45.500 MHz')).toBeInTheDocument());
  });
});
