import { describe, it, expect } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { FrequencyDisplay } from '../components/frequency/FrequencyDisplay';
import { FrequencyEditor } from '../components/frequency/FrequencyEditor';

function setRoleToken(role: string) {
  const payload = {
    sub: 'user-1',
    email: 'tester@example.com',
    role,
    exp: Math.floor(Date.now() / 1000) + 3600,
  };
  localStorage.setItem('milsim_token', `x.${btoa(JSON.stringify(payload))}.x`);
}

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/events/evt-1']}>
        <Routes>
          <Route path="/events/:id" element={ui} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

const fullFrequencyData = {
  command: { primary: '180.000', backup: '181.000' },
  platoons: [
    { platoonId: 'plt-1', platoonName: 'Alpha Platoon', primary: '170.000', backup: '171.000' },
  ],
  squads: [
    { squadId: 'sqd-1', squadName: 'Alpha-1', platoonId: 'plt-1', primary: '160.000', backup: '161.000' },
  ],
};

const playerFrequencyData = {
  command: null,
  platoons: [],
  squads: [
    { squadId: 'sqd-1', squadName: 'Alpha-1', platoonId: 'plt-1', primary: '160.000', backup: '161.000' },
  ],
};

describe('FrequencyDisplay', () => {
  it('renders all sections for faction_commander role', async () => {
    setRoleToken('faction_commander');
    server.use(
      http.get('/api/events/evt-1/frequencies', () => HttpResponse.json(fullFrequencyData))
    );

    renderWithProviders(<FrequencyDisplay eventId="evt-1" />);

    await waitFor(() => expect(screen.getByText('Command')).toBeInTheDocument());
    expect(screen.getByText('Platoons')).toBeInTheDocument();
    expect(screen.getByText('Squads')).toBeInTheDocument();
    expect(screen.getByText('180.000')).toBeInTheDocument();
    expect(screen.getByText('170.000')).toBeInTheDocument();
    expect(screen.getByText('160.000')).toBeInTheDocument();
  });

  it('renders only squad section for player role', async () => {
    setRoleToken('player');
    server.use(
      http.get('/api/events/evt-1/frequencies', () => HttpResponse.json(playerFrequencyData))
    );

    renderWithProviders(<FrequencyDisplay eventId="evt-1" />);

    await waitFor(() => expect(screen.getByText('Squads')).toBeInTheDocument());
    expect(screen.queryByText('Command')).not.toBeInTheDocument();
    expect(screen.queryByText('Platoons')).not.toBeInTheDocument();
    expect(screen.getByText('Alpha-1')).toBeInTheDocument();
    expect(screen.getByText('160.000')).toBeInTheDocument();
  });

  it('renders loading state', () => {
    setRoleToken('faction_commander');
    server.use(
      http.get('/api/events/evt-1/frequencies', () => new Promise(() => {})) // never resolves
    );

    renderWithProviders(<FrequencyDisplay eventId="evt-1" />);
    expect(screen.getByText('Loading frequencies...')).toBeInTheDocument();
  });

  it('renders error state on API failure', async () => {
    setRoleToken('faction_commander');
    server.use(
      http.get('/api/events/evt-1/frequencies', () => HttpResponse.json({ error: 'fail' }, { status: 500 }))
    );

    renderWithProviders(<FrequencyDisplay eventId="evt-1" />);
    await waitFor(() => expect(screen.getByText('Failed to load frequencies.')).toBeInTheDocument());
  });

  it('renders dash for null frequencies', async () => {
    setRoleToken('faction_commander');
    server.use(
      http.get('/api/events/evt-1/frequencies', () =>
        HttpResponse.json({
          command: { primary: null, backup: null },
          platoons: [],
          squads: [],
        })
      )
    );

    renderWithProviders(<FrequencyDisplay eventId="evt-1" />);
    await waitFor(() => expect(screen.getByText('Command')).toBeInTheDocument());
    const dashes = screen.getAllByText('—');
    expect(dashes.length).toBe(2);
  });
});

describe('FrequencyEditor', () => {
  it('renders edit forms for faction_commander', async () => {
    setRoleToken('faction_commander');
    server.use(
      http.get('/api/events/evt-1/frequencies', () => HttpResponse.json(fullFrequencyData))
    );

    renderWithProviders(<FrequencyEditor eventId="evt-1" />);

    await waitFor(() => expect(screen.getByText('Edit Command Frequency')).toBeInTheDocument());
    expect(screen.getByText('Edit Platoon Frequencies')).toBeInTheDocument();
    expect(screen.getByText('Edit Squad Frequencies')).toBeInTheDocument();
  });

  it('renders nothing for player role', () => {
    setRoleToken('player');

    const { container } = renderWithProviders(<FrequencyEditor eventId="evt-1" />);
    expect(container.innerHTML).toBe('');
  });

  it('renders squad edit only for squad_leader', async () => {
    setRoleToken('squad_leader');
    server.use(
      http.get('/api/events/evt-1/frequencies', () => HttpResponse.json(playerFrequencyData))
    );

    renderWithProviders(<FrequencyEditor eventId="evt-1" />);

    await waitFor(() => expect(screen.getByText('Edit Squad Frequencies')).toBeInTheDocument());
    expect(screen.queryByText('Edit Command Frequency')).not.toBeInTheDocument();
    expect(screen.queryByText('Edit Platoon Frequencies')).not.toBeInTheDocument();
  });
});
