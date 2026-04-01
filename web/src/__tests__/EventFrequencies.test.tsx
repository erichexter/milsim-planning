import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { EventFrequencies } from '../components/EventFrequencies';

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('EventFrequencies', () => {
  describe('player role (squad only)', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/frequencies', () =>
          HttpResponse.json({
            squad: { primary: '143.000', backup: '144.000' },
            platoon: null,
            command: null,
          })
        )
      );
    });

    it('renders squad frequency', async () => {
      renderWithQuery(<EventFrequencies eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('Squad')).toBeInTheDocument());
      expect(screen.getByText('143.000')).toBeInTheDocument();
      expect(screen.getByText('144.000')).toBeInTheDocument();
    });

    it('does not render platoon or command sections', async () => {
      renderWithQuery(<EventFrequencies eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('Squad')).toBeInTheDocument());
      expect(screen.queryByText('Platoon')).not.toBeInTheDocument();
      expect(screen.queryByText('Command')).not.toBeInTheDocument();
    });
  });

  describe('squad_leader role (squad + platoon)', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/frequencies', () =>
          HttpResponse.json({
            squad: { primary: '143.000', backup: null },
            platoon: { primary: '145.000', backup: '146.000' },
            command: null,
          })
        )
      );
    });

    it('renders squad and platoon frequencies', async () => {
      renderWithQuery(<EventFrequencies eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('Squad')).toBeInTheDocument());
      expect(screen.getByText('Platoon')).toBeInTheDocument();
      expect(screen.getByText('145.000')).toBeInTheDocument();
    });

    it('does not render command section', async () => {
      renderWithQuery(<EventFrequencies eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('Squad')).toBeInTheDocument());
      expect(screen.queryByText('Command')).not.toBeInTheDocument();
    });

    it('shows "not set" for null backup', async () => {
      renderWithQuery(<EventFrequencies eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('Squad')).toBeInTheDocument());
      expect(screen.getByText('not set')).toBeInTheDocument();
    });
  });

  describe('platoon_leader role (platoon + command)', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/frequencies', () =>
          HttpResponse.json({
            squad: null,
            platoon: { primary: '145.000', backup: '146.000' },
            command: { primary: '147.000', backup: '148.000' },
          })
        )
      );
    });

    it('renders platoon and command frequencies', async () => {
      renderWithQuery(<EventFrequencies eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('Platoon')).toBeInTheDocument());
      expect(screen.getByText('Command')).toBeInTheDocument();
      expect(screen.getByText('147.000')).toBeInTheDocument();
    });

    it('does not render squad section', async () => {
      renderWithQuery(<EventFrequencies eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('Platoon')).toBeInTheDocument());
      expect(screen.queryByText('Squad')).not.toBeInTheDocument();
    });
  });

  describe('faction_commander role (command visible)', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/frequencies', () =>
          HttpResponse.json({
            squad: null,
            platoon: null,
            command: { primary: '147.000', backup: '148.000' },
          })
        )
      );
    });

    it('renders command frequency', async () => {
      renderWithQuery(<EventFrequencies eventId="evt-1" />);
      await waitFor(() => expect(screen.getByText('Command')).toBeInTheDocument());
      expect(screen.getByText('147.000')).toBeInTheDocument();
    });
  });

  describe('empty state', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/frequencies', () =>
          HttpResponse.json({ squad: null, platoon: null, command: null })
        )
      );
    });

    it('shows empty state message when no frequencies assigned', async () => {
      renderWithQuery(<EventFrequencies eventId="evt-1" />);
      await waitFor(() =>
        expect(screen.getByText('No frequencies assigned for your role.')).toBeInTheDocument()
      );
    });
  });

  describe('error state', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/frequencies', () => HttpResponse.error())
      );
    });

    it('shows error message on fetch failure', async () => {
      renderWithQuery(<EventFrequencies eventId="evt-1" />);
      await waitFor(() =>
        expect(screen.getByText('Failed to load frequencies.')).toBeInTheDocument()
      );
    });
  });
});
