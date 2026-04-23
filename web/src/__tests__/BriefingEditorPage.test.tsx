import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Routes, Route } from 'react-router';
import { BriefingEditorPage } from '../pages/briefings/BriefingEditorPage';
import type { BriefingDto } from '../lib/api';

const BRIEFING_ID = 'aaaaaaaa-0000-0000-0000-000000000001';

const mockBriefing: BriefingDto = {
  id: BRIEFING_ID,
  title: 'Op Nightfall Brief',
  description: 'Night operation briefing',
  channelIdentifier: 'bbbbbbbb-0000-0000-0000-000000000001',
  publicationState: 'Draft',
  versionETag: 'etag-v1',
  createdAt: '2026-04-20T10:00:00Z',
  updatedAt: '2026-04-20T10:00:00Z',
};

function renderWithProviders(briefingId = BRIEFING_ID) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[`/briefings/${briefingId}/edit`]}>
        <Routes>
          <Route path="/briefings" element={<div>Briefings List</div>} />
          <Route path="/briefings/:id/edit" element={<BriefingEditorPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('BriefingEditorPage', () => {
  beforeEach(() => {
    server.use(
      http.get(`/api/v1/briefings/${BRIEFING_ID}`, () => HttpResponse.json(mockBriefing))
    );
  });

  it('renders briefing title and description', async () => {
    renderWithProviders();

    await waitFor(() =>
      expect(screen.getByText('Op Nightfall Brief')).toBeInTheDocument()
    );
    expect(screen.getByText('Night operation briefing')).toBeInTheDocument();
  });

  it('renders the channel identifier', async () => {
    renderWithProviders();

    await waitFor(() =>
      expect(screen.getByText(/bbbbbbbb-0000-0000-0000-000000000001/i)).toBeInTheDocument()
    );
  });

  it('renders the publication state badge', async () => {
    renderWithProviders();

    await waitFor(() =>
      expect(screen.getByText('Draft')).toBeInTheDocument()
    );
  });

  it('renders the Map Image section with the upload zone', async () => {
    renderWithProviders();

    await waitFor(() =>
      expect(screen.getByText('Map Image')).toBeInTheDocument()
    );
    expect(screen.getByText(/Drag & drop a map image here/i)).toBeInTheDocument();
  });

  it('renders a back link to briefings list', async () => {
    renderWithProviders();

    await waitFor(() =>
      expect(screen.getByRole('link', { name: /Briefing Channels/i })).toBeInTheDocument()
    );
  });

  it('shows an error state when briefing fails to load', async () => {
    server.use(
      http.get(`/api/v1/briefings/${BRIEFING_ID}`, () =>
        HttpResponse.json({ error: 'Not found' }, { status: 404 })
      )
    );

    renderWithProviders();

    await waitFor(() =>
      expect(screen.getByText(/Failed to load briefing/i)).toBeInTheDocument()
    );
  });
});
