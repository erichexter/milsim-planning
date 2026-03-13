import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Routes, Route } from 'react-router';
import { RosterView } from '../pages/roster/RosterView';

const mockHierarchy = {
  platoons: [
    {
      id: 'plat-1',
      name: 'Alpha Platoon',
      squads: [
        {
          id: 'squad-1',
          name: 'Alpha 1',
          players: [
            { id: 'p1', name: 'John Smith', callsign: 'GHOST', teamAffiliation: null },
            { id: 'p2', name: 'Jane Doe', callsign: 'NOVA', teamAffiliation: null },
          ],
        },
      ],
    },
  ],
  unassignedPlayers: [],
};

describe('RosterView', () => {
  beforeEach(() => {
    server.use(
      http.get('/api/events/evt-1/roster', () => HttpResponse.json(mockHierarchy))
    );
  });

  it('renders platoon accordion sections', async () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={qc}>
        <MemoryRouter initialEntries={['/events/evt-1/roster']}>
          <Routes>
            <Route path="/events/:id/roster" element={<RosterView />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );
    await waitFor(() => expect(screen.getByText('Alpha Platoon')).toBeInTheDocument());
    expect(screen.getByText('Alpha 1')).toBeInTheDocument();
  });

  it('search filters players by callsign', async () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={qc}>
        <MemoryRouter initialEntries={['/events/evt-1/roster']}>
          <Routes>
            <Route path="/events/:id/roster" element={<RosterView />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );
    await waitFor(() => screen.getByText('John Smith'));

    fireEvent.change(screen.getByPlaceholderText('Search by name or callsign...'), {
      target: { value: 'GHOST' },
    });

    expect(screen.getByText('John Smith')).toBeInTheDocument();
    expect(screen.queryByText('Jane Doe')).not.toBeInTheDocument();
  });

  it('search filters players by name', async () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={qc}>
        <MemoryRouter initialEntries={['/events/evt-1/roster']}>
          <Routes>
            <Route path="/events/:id/roster" element={<RosterView />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );
    await waitFor(() => screen.getByText('Jane Doe'));

    fireEvent.change(screen.getByPlaceholderText('Search by name or callsign...'), {
      target: { value: 'Jane' },
    });

    expect(screen.getByText('Jane Doe')).toBeInTheDocument();
    expect(screen.queryByText('John Smith')).not.toBeInTheDocument();
  });

  it('callsign displayed prominently in roster', async () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={qc}>
        <MemoryRouter initialEntries={['/events/evt-1/roster']}>
          <Routes>
            <Route path="/events/:id/roster" element={<RosterView />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );
    await waitFor(() => screen.getByText('[GHOST]'));
    expect(screen.getByText('[NOVA]')).toBeInTheDocument();
  });
});
