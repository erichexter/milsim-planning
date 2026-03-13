import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Routes, Route } from 'react-router';
import { HierarchyBuilder } from '../pages/roster/HierarchyBuilder';

const mockRoster = {
  platoons: [
    {
      id: 'plat-1',
      name: 'Alpha',
      squads: [
        {
          id: 'sq-1',
          name: 'Alpha-1',
          players: [
            {
              id: 'p1',
              name: 'John Smith',
              callsign: 'GHOST',
              teamAffiliation: 'Alpha Team',
            },
          ],
        },
      ],
    },
  ],
  unassignedPlayers: [
    { id: 'p2', name: 'Jane Doe', callsign: 'NOVA', teamAffiliation: 'Alpha Team' },
  ],
};

describe('HierarchyBuilder', () => {
  beforeEach(() => {
    server.use(
      http.get('/api/events/evt-1/roster', () => HttpResponse.json(mockRoster))
    );
  });

  it('groups players by team affiliation', async () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={qc}>
        <MemoryRouter initialEntries={['/events/evt-1/hierarchy']}>
          <Routes>
            <Route path="/events/:id/hierarchy" element={<HierarchyBuilder />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );
    await waitFor(() => expect(screen.getByText('Alpha Team')).toBeInTheDocument());
    expect(screen.getByText('John Smith')).toBeInTheDocument();
    expect(screen.getByText('Jane Doe')).toBeInTheDocument();
  });

  it('squad cell shows current assignment for assigned player', async () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={qc}>
        <MemoryRouter initialEntries={['/events/evt-1/hierarchy']}>
          <Routes>
            <Route path="/events/:id/hierarchy" element={<HierarchyBuilder />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );
    await waitFor(() => screen.getByText('John Smith'));
    // p1 is assigned to sq-1 "Alpha-1" — combobox shows the squad name
    expect(screen.getByText('Alpha-1')).toBeInTheDocument();
  });

  it('callsign is displayed prominently', async () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={qc}>
        <MemoryRouter initialEntries={['/events/evt-1/hierarchy']}>
          <Routes>
            <Route path="/events/:id/hierarchy" element={<HierarchyBuilder />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );
    await waitFor(() => screen.getByText('John Smith'));
    // Callsigns appear in [GHOST] format
    expect(screen.getByText('[GHOST]')).toBeInTheDocument();
    expect(screen.getByText('[NOVA]')).toBeInTheDocument();
  });
});
