import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { FrequencyDisplay } from '../components/frequency/FrequencyDisplay';

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('FrequencyDisplay', () => {
  describe('player role — squad only', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/frequencies', () =>
          HttpResponse.json({
            command: null,
            platoons: [],
            squads: [
              { squadId: 'sq-1', name: 'Alpha 1', platoonId: 'pl-1', primary: '157.000', backup: '157.500' },
            ],
          })
        )
      );
    });

    it('renders squad frequency for player role', async () => {
      renderWithQuery(<FrequencyDisplay eventId="evt-1" />);
      await waitFor(() => expect(screen.getByTestId('frequency-display')).toBeInTheDocument());
      expect(screen.getByText('Alpha 1')).toBeInTheDocument();
      expect(screen.getByText('157.000')).toBeInTheDocument();
      expect(screen.getByText('157.500')).toBeInTheDocument();
    });

    it('hides command and platoon sections for player role', async () => {
      renderWithQuery(<FrequencyDisplay eventId="evt-1" />);
      await waitFor(() => expect(screen.getByTestId('frequency-display')).toBeInTheDocument());
      expect(screen.queryByText('Command')).not.toBeInTheDocument();
      expect(screen.queryByText('Platoons')).not.toBeInTheDocument();
    });
  });

  describe('faction_commander role — all three levels', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/frequencies', () =>
          HttpResponse.json({
            command: { primary: '155.000', backup: '155.500' },
            platoons: [
              { platoonId: 'pl-1', name: '1st Platoon', primary: '156.000', backup: '156.500' },
            ],
            squads: [
              { squadId: 'sq-1', name: 'Alpha 1', platoonId: 'pl-1', primary: '157.000', backup: '157.500' },
            ],
          })
        )
      );
    });

    it('renders all three levels for faction_commander role', async () => {
      renderWithQuery(<FrequencyDisplay eventId="evt-1" />);
      await waitFor(() => expect(screen.getByTestId('frequency-display')).toBeInTheDocument());
      expect(screen.getByText('Command')).toBeInTheDocument();
      expect(screen.getByText('155.000')).toBeInTheDocument();
      expect(screen.getByText('Platoons')).toBeInTheDocument();
      expect(screen.getByText('1st Platoon')).toBeInTheDocument();
      expect(screen.getByText('156.000')).toBeInTheDocument();
      expect(screen.getByText('Squads')).toBeInTheDocument();
      expect(screen.getByText('Alpha 1')).toBeInTheDocument();
      expect(screen.getByText('157.000')).toBeInTheDocument();
    });
  });

  describe('empty state', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/frequencies', () =>
          HttpResponse.json({ command: null, platoons: [], squads: [] })
        )
      );
    });

    it('shows empty message when no frequencies assigned', async () => {
      renderWithQuery(<FrequencyDisplay eventId="evt-1" />);
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
      renderWithQuery(<FrequencyDisplay eventId="evt-1" />);
      await waitFor(() =>
        expect(screen.getByText('Failed to load frequencies.')).toBeInTheDocument()
      );
    });
  });

  describe('edit buttons', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/evt-1/frequencies', () =>
          HttpResponse.json({
            command: { primary: '155.000', backup: '155.500' },
            platoons: [],
            squads: [
              { squadId: 'sq-1', name: 'Alpha 1', platoonId: 'pl-1', primary: '157.000', backup: null },
            ],
          })
        )
      );
    });

    it('shows edit buttons when canEdit is true', async () => {
      const onEdit = () => {};
      renderWithQuery(<FrequencyDisplay eventId="evt-1" canEdit onEdit={onEdit} />);
      await waitFor(() => expect(screen.getByTestId('frequency-display')).toBeInTheDocument());
      const editButtons = screen.getAllByText('Edit');
      expect(editButtons.length).toBeGreaterThanOrEqual(2);
    });

    it('hides edit buttons when canEdit is false', async () => {
      renderWithQuery(<FrequencyDisplay eventId="evt-1" />);
      await waitFor(() => expect(screen.getByTestId('frequency-display')).toBeInTheDocument());
      expect(screen.queryByText('Edit')).not.toBeInTheDocument();
    });
  });
});
