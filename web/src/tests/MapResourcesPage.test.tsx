import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { MapResourcesPage } from '../pages/events/MapResourcesPage';

function setRoleToken(role: string) {
  const payload = {
    sub: 'user-1',
    email: 'tester@example.com',
    role,
    exp: Math.floor(Date.now() / 1000) + 3600,
  };
  localStorage.setItem('milsim_token', `x.${btoa(JSON.stringify(payload))}.x`);
}

function renderPage() {
  const queryClient = new QueryClient();
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/events/evt-1/maps']}>
        <Routes>
          <Route path="/events/:id/maps" element={<MapResourcesPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('MapResourcesPage', () => {
  it('renders external link card with URL and markdown instructions', async () => {
    setRoleToken('Player');
    server.use(
      http.get('/api/events/evt-1/map-resources', () =>
        HttpResponse.json([
          {
            id: 'res-link',
            externalUrl: 'https://example.com/map',
            instructions: 'Use **route alpha**',
            r2Key: null,
            friendlyName: 'Tac map',
            contentType: null,
            order: 0,
          },
        ])
      )
    );

    renderPage();

    expect(await screen.findByText('https://example.com/map')).toBeInTheDocument();
    expect(screen.getByText('route alpha')).toBeInTheDocument();
  });

  it('renders file resource card with download button', async () => {
    setRoleToken('Player');
    server.use(
      http.get('/api/events/evt-1/map-resources', () =>
        HttpResponse.json([
          {
            id: 'res-file',
            externalUrl: null,
            instructions: null,
            r2Key: null,
            friendlyName: 'satellite.jpg',
            contentType: 'image/jpeg',
            order: 0,
          },
        ])
      )
    );

    renderPage();
    const fileNameMatches = await screen.findAllByText('satellite.jpg');
    expect(fileNameMatches.length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'Download' })).toBeInTheDocument();
  });
});
