import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Routes, Route } from 'react-router';
import { EventList } from '../pages/events/EventList';

function renderWithProviders(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={['/events']}>
        <Routes>
          <Route path="/events" element={ui} />
          <Route path="/events/:id" element={<div>Event Detail</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

const mockEvents = [
  {
    id: 'evt-1',
    name: 'Op Thunder',
    location: 'Forest',
    description: null,
    startDate: '2026-06-01',
    endDate: null,
    status: 'Draft' as const,
  },
  {
    id: 'evt-2',
    name: 'Op Storm',
    location: null,
    description: null,
    startDate: null,
    endDate: null,
    status: 'Published' as const,
  },
];

describe('EventList', () => {
  beforeEach(() => {
    server.use(http.get('/api/events', () => HttpResponse.json(mockEvents)));
  });

  it('renders list of commander events', async () => {
    renderWithProviders(<EventList />);
    await waitFor(() => expect(screen.getByText('Op Thunder')).toBeInTheDocument());
    expect(screen.getByText('Op Storm')).toBeInTheDocument();
  });

  it('shows Draft and Published badges', async () => {
    renderWithProviders(<EventList />);
    await waitFor(() => expect(screen.getByText('Draft')).toBeInTheDocument());
    expect(screen.getByText('Published')).toBeInTheDocument();
  });

  it('shows empty state when no events', async () => {
    server.use(http.get('/api/events', () => HttpResponse.json([])));
    renderWithProviders(<EventList />);
    await waitFor(() =>
      expect(screen.getByText(/no events yet/i)).toBeInTheDocument()
    );
  });

  it('duplicate event dialog sends copyInfoSectionIds array', async () => {
    let capturedBody: { copyInfoSectionIds: string[] } | undefined;
    server.use(
      http.post('/api/events/evt-1/duplicate', async ({ request }) => {
        capturedBody = (await request.json()) as { copyInfoSectionIds: string[] };
        return HttpResponse.json(
          {
            id: 'dup-1',
            name: 'Op Thunder (Copy)',
            location: null,
            description: null,
            startDate: null,
            endDate: null,
            status: 'Draft',
          },
          { status: 201 }
        );
      })
    );
    renderWithProviders(<EventList />);
    await waitFor(() => screen.getByText('Op Thunder'));

    // Open the duplicate dialog for the first event
    fireEvent.click(screen.getAllByText('Duplicate')[0]);
    await waitFor(() => screen.getByRole('dialog')); // dialog opened

    // Submit duplicate (sends copyInfoSectionIds: [] since no sections)
    fireEvent.click(screen.getByRole('button', { name: 'Duplicate Event' }));

    await waitFor(() => {
      expect(capturedBody).toBeDefined();
      expect(Array.isArray(capturedBody!.copyInfoSectionIds)).toBe(true);
    });
  });
});
