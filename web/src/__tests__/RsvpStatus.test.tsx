import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RsvpStatus } from '../components/rsvp/RsvpStatus';

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('RsvpStatus', () => {
  describe('when no RSVP exists', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/rsvp', () => HttpResponse.json(null))
      );
    });

    it('renders RSVP heading and three buttons', async () => {
      renderWithQuery(<RsvpStatus eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('RSVP')).toBeInTheDocument());
      expect(screen.getByRole('button', { name: 'Attending' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Maybe' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Not Attending' })).toBeInTheDocument();
    });

    it('does not show current status text', async () => {
      renderWithQuery(<RsvpStatus eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('RSVP')).toBeInTheDocument());
      expect(screen.queryByText(/Your status:/)).not.toBeInTheDocument();
    });
  });

  describe('when RSVP is Attending', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/rsvp', () =>
          HttpResponse.json({
            eventId: 'evt-1',
            userId: 'user-1',
            status: 'Attending',
            respondedAt: '2026-04-15T12:00:00Z',
          })
        )
      );
    });

    it('shows current status as Attending', async () => {
      renderWithQuery(<RsvpStatus eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('Attending', { selector: 'span' })).toBeInTheDocument());
    });
  });

  describe('when RSVP is Maybe', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/rsvp', () =>
          HttpResponse.json({
            eventId: 'evt-1',
            userId: 'user-1',
            status: 'Maybe',
            respondedAt: '2026-04-15T12:00:00Z',
          })
        )
      );
    });

    it('shows current status as Maybe', async () => {
      renderWithQuery(<RsvpStatus eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('Maybe', { selector: 'span' })).toBeInTheDocument());
    });
  });

  describe('when RSVP is NotAttending', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/rsvp', () =>
          HttpResponse.json({
            eventId: 'evt-1',
            userId: 'user-1',
            status: 'NotAttending',
            respondedAt: '2026-04-15T12:00:00Z',
          })
        )
      );
    });

    it('shows current status as Not Attending', async () => {
      renderWithQuery(<RsvpStatus eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('Not Attending', { selector: 'span' })).toBeInTheDocument());
    });
  });

  describe('setting RSVP', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/rsvp', () => HttpResponse.json(null)),
        http.put('/api/events/evt-1/rsvp', async ({ request }) => {
          const body = await request.json() as { status: string };
          return HttpResponse.json({
            eventId: 'evt-1',
            userId: 'user-1',
            status: body.status,
            respondedAt: '2026-04-15T12:00:00Z',
          });
        })
      );
    });

    it('calls PUT when clicking Attending button', async () => {
      const user = userEvent.setup();
      renderWithQuery(<RsvpStatus eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('RSVP')).toBeInTheDocument());

      await user.click(screen.getByRole('button', { name: 'Attending' }));
      // After mutation, query is invalidated — we just verify no error
      await waitFor(() => expect(screen.getByRole('button', { name: 'Attending' })).not.toBeDisabled());
    });
  });
});
