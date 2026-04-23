import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Routes, Route } from 'react-router';
import { BriefingsListPage } from '../pages/briefings/BriefingsListPage';
import type { BriefingListDto } from '../lib/api';

function renderWithProviders(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={['/briefings']}>
        <Routes>
          <Route path="/briefings" element={ui} />
          <Route path="/briefings/:id" element={<div>Briefing Detail</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

const mockBriefingList: BriefingListDto = {
  items: [
    {
      id: 'aaaaaaaa-0000-0000-0000-000000000001',
      title: 'Op Nightfall Brief',
      description: 'Night operation briefing',
      channelIdentifier: 'bbbbbbbb-0000-0000-0000-000000000001',
      publicationState: 'Draft',
      updatedAt: '2026-04-20T10:00:00Z',
    },
    {
      id: 'aaaaaaaa-0000-0000-0000-000000000002',
      title: 'Op Storm Brief',
      description: null,
      channelIdentifier: 'bbbbbbbb-0000-0000-0000-000000000002',
      publicationState: 'Published',
      updatedAt: '2026-04-21T12:00:00Z',
    },
    {
      id: 'aaaaaaaa-0000-0000-0000-000000000003',
      title: 'Old Brief',
      description: 'Archived brief',
      channelIdentifier: 'bbbbbbbb-0000-0000-0000-000000000003',
      publicationState: 'Archived',
      updatedAt: '2026-03-15T08:00:00Z',
    },
  ],
  pagination: { limit: 20, offset: 0, total: 3 },
};

describe('BriefingsListPage', () => {
  beforeEach(() => {
    server.use(
      http.get('/api/v1/briefings', () => HttpResponse.json(mockBriefingList))
    );
  });

  it('renders channel list with all briefing titles', async () => {
    renderWithProviders(<BriefingsListPage />);

    await waitFor(() => expect(screen.getByText('Op Nightfall Brief')).toBeInTheDocument());
    expect(screen.getByText('Op Storm Brief')).toBeInTheDocument();
    expect(screen.getByText('Old Brief')).toBeInTheDocument();
  });

  it('renders Draft state badge for draft briefings', async () => {
    renderWithProviders(<BriefingsListPage />);

    await waitFor(() => expect(screen.getByText('Op Nightfall Brief')).toBeInTheDocument());
    expect(screen.getByText('Draft')).toBeInTheDocument();
  });

  it('renders Published state badge for published briefings', async () => {
    renderWithProviders(<BriefingsListPage />);

    await waitFor(() => expect(screen.getByText('Op Storm Brief')).toBeInTheDocument());
    expect(screen.getByText('Published')).toBeInTheDocument();
  });

  it('renders Archived state badge for archived briefings', async () => {
    renderWithProviders(<BriefingsListPage />);

    await waitFor(() => expect(screen.getByText('Old Brief')).toBeInTheDocument());
    expect(screen.getByText('Archived')).toBeInTheDocument();
  });

  it('renders last updated timestamp for each channel', async () => {
    renderWithProviders(<BriefingsListPage />);

    await waitFor(() => expect(screen.getByText('Op Nightfall Brief')).toBeInTheDocument());

    // Each item should have "Updated ..." text
    const updatedTexts = screen.getAllByText(/Updated/i);
    expect(updatedTexts.length).toBeGreaterThanOrEqual(3);
  });

  it('each channel row is clickable and links to editor/preview', async () => {
    renderWithProviders(<BriefingsListPage />);

    await waitFor(() => expect(screen.getByText('Op Nightfall Brief')).toBeInTheDocument());

    // Title links should point to /briefings/{id}
    const links = screen.getAllByRole('link', { name: /Op Nightfall Brief/i });
    expect(links.length).toBeGreaterThanOrEqual(1);
    expect(links[0]).toHaveAttribute('href', '/briefings/aaaaaaaa-0000-0000-0000-000000000001');
  });

  it('shows empty state when no briefings exist', async () => {
    server.use(
      http.get('/api/v1/briefings', () =>
        HttpResponse.json({ items: [], pagination: { limit: 20, offset: 0, total: 0 } })
      )
    );

    renderWithProviders(<BriefingsListPage />);

    await waitFor(() =>
      expect(screen.getByText(/No briefing channels yet/i)).toBeInTheDocument()
    );
  });

  it('shows total count in header', async () => {
    renderWithProviders(<BriefingsListPage />);

    await waitFor(() => expect(screen.getByText(/3 channels/i)).toBeInTheDocument());
  });

  it('renders description when present', async () => {
    renderWithProviders(<BriefingsListPage />);

    await waitFor(() => expect(screen.getByText('Night operation briefing')).toBeInTheDocument());
  });
});
