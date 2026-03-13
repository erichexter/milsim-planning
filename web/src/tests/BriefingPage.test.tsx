import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { BriefingPage } from '../pages/events/BriefingPage';

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
      <MemoryRouter initialEntries={['/events/evt-1/briefing']}>
        <Routes>
          <Route path="/events/:id/briefing" element={<BriefingPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('BriefingPage', () => {
  it('renders section list from API', async () => {
    setRoleToken('Player');
    server.use(
      http.get('/api/events/evt-1/info-sections', () =>
        HttpResponse.json([
          {
            id: '1',
            title: 'Comms Plan',
            bodyMarkdown: 'body text',
            order: 0,
            attachments: [],
          },
        ])
      )
    );

    renderPage();
    expect(await screen.findByText('Comms Plan')).toBeInTheDocument();
  });

  it('renders Add Section button for commander role', async () => {
    setRoleToken('Commander');
    server.use(http.get('/api/events/evt-1/info-sections', () => HttpResponse.json([])));

    renderPage();
    expect(await screen.findByRole('button', { name: 'Add Section' })).toBeInTheDocument();
  });

  it('starts collapsed to title-only view', async () => {
    setRoleToken('Player');
    server.use(
      http.get('/api/events/evt-1/info-sections', () =>
        HttpResponse.json([
          {
            id: '1',
            title: 'Collapsed Section',
            bodyMarkdown: 'Hidden body',
            order: 0,
            attachments: [],
          },
        ])
      )
    );

    renderPage();
    expect(await screen.findByText('Collapsed Section')).toBeInTheDocument();
    expect(screen.queryByText('Hidden body')).not.toBeInTheDocument();
  });
});
