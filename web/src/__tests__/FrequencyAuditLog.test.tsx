import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { FrequencyAuditLog } from '../components/FrequencyAuditLog';
import userEvent from '@testing-library/user-event';

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('FrequencyAuditLog', () => {
  const mockEventId = 'evt-123';

  const mockAuditEntries = [
    {
      id: 'log-1',
      eventId: mockEventId,
      channelName: 'Channel A',
      unitType: 'squad',
      unitId: 'unit-1',
      unitName: 'Squad 1',
      primaryFrequency: '36.500',
      alternateFrequency: '36.525',
      actionType: 'created',
      conflictingUnitName: null,
      performedByUserId: 'user-1',
      performedByDisplayName: 'John Doe',
      occurredAt: '2026-04-27T10:00:00Z',
    },
    {
      id: 'log-2',
      eventId: mockEventId,
      channelName: 'Channel A',
      unitType: 'squad',
      unitId: 'unit-2',
      unitName: 'Squad 2',
      primaryFrequency: '36.500',
      alternateFrequency: null,
      actionType: 'conflict_detected',
      conflictingUnitName: 'Squad 1',
      performedByUserId: 'user-1',
      performedByDisplayName: 'John Doe',
      occurredAt: '2026-04-27T10:15:00Z',
    },
  ];

  describe('rendering', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/:eventId/frequency-audit-log', () =>
          HttpResponse.json(mockAuditEntries)
        )
      );
    });

    it('renders audit log heading', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);
      expect(screen.getByTestId('frequency-audit-log')).toBeInTheDocument();
    });

    it('displays audit log entries', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);

      await waitFor(() => {
        expect(screen.getByText('Squad 1')).toBeInTheDocument();
        expect(screen.getByText('Squad 2')).toBeInTheDocument();
      });

      // Verify AC-03: each entry shows required fields
      expect(screen.getByText('Channel A')).toBeInTheDocument();
      expect(screen.getByText('36.500')).toBeInTheDocument();
      expect(screen.getByText('John Doe')).toBeInTheDocument();
    });

    it('renders action type badges', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);

      await waitFor(() => {
        expect(screen.getByText('Created')).toBeInTheDocument();
        expect(screen.getByText('Conflict Detected')).toBeInTheDocument();
      });
    });

    it('displays conflict information when present', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);

      await waitFor(() => {
        expect(screen.getByText(/Conflicting with:/)).toBeInTheDocument();
        expect(screen.getByText('Squad 1')).toBeInTheDocument();
      });
    });
  });

  describe('filtering', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/:eventId/frequency-audit-log', ({ request }) => {
          const url = new URL(request.url);
          const unitFilter = url.searchParams.get('unitFilter');

          // AC-07: Filter by unit name
          if (unitFilter) {
            const filtered = mockAuditEntries.filter(e =>
              e.unitName.toLowerCase().includes(unitFilter.toLowerCase())
            );
            return HttpResponse.json(filtered);
          }

          return HttpResponse.json(mockAuditEntries);
        })
      );
    });

    it('filters by unit name when user enters text', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);

      const input = screen.getByPlaceholderText('Search unit...');
      await userEvent.type(input, 'Squad 1');

      await waitFor(() => {
        expect(screen.getByText('Squad 1')).toBeInTheDocument();
        expect(screen.queryByText('Squad 2')).not.toBeInTheDocument();
      });
    });

    it('shows all entries when filter is cleared', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);

      const input = screen.getByPlaceholderText('Search unit...') as HTMLInputElement;

      // Type a filter
      await userEvent.type(input, 'Squad 1');

      await waitFor(() => {
        expect(screen.getByText('Squad 1')).toBeInTheDocument();
      });

      // Clear the filter
      await userEvent.clear(input);

      await waitFor(() => {
        expect(screen.getByText('Squad 1')).toBeInTheDocument();
        expect(screen.getByText('Squad 2')).toBeInTheDocument();
      });
    });
  });

  describe('sorting', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/:eventId/frequency-audit-log', ({ request }) => {
          const url = new URL(request.url);
          const newestFirst = url.searchParams.get('newestFirst') !== 'false';

          // AC-02: Chronological sorting (newest first or oldest first)
          const sorted = [...mockAuditEntries];
          if (newestFirst) {
            sorted.reverse();
          }

          return HttpResponse.json(sorted);
        })
      );
    });

    it('displays sort order dropdown', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);

      const select = screen.getByLabelText('Sort') as HTMLSelectElement;
      expect(select).toBeInTheDocument();
      expect(select.value).toBe('newest');
    });

    it('changes sort order when dropdown is changed', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);

      const select = screen.getByLabelText('Sort') as HTMLSelectElement;
      fireEvent.change(select, { target: { value: 'oldest' } });

      await waitFor(() => {
        expect(select.value).toBe('oldest');
      });
    });
  });

  describe('empty state', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/:eventId/frequency-audit-log', () =>
          HttpResponse.json([])
        )
      );
    });

    it('displays message when no entries found', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);

      await waitFor(() => {
        expect(screen.getByText('No audit log entries found.')).toBeInTheDocument();
      });
    });
  });

  describe('error state', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/:eventId/frequency-audit-log', () =>
          HttpResponse.error()
        )
      );
    });

    it('displays error message on fetch failure', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);

      await waitFor(() => {
        expect(screen.getByText('Failed to load audit log.')).toBeInTheDocument();
      });
    });
  });

  describe('read-only behavior', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/:eventId/frequency-audit-log', () =>
          HttpResponse.json(mockAuditEntries)
        )
      );
    });

    it('does not render edit or delete buttons', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);

      await waitFor(() => {
        expect(screen.getByText('Squad 1')).toBeInTheDocument();
      });

      // AC-05: Log is read-only (no edit/delete buttons)
      expect(screen.queryByText(/edit|delete|remove/i)).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /edit|delete|remove/i })).not.toBeInTheDocument();
    });
  });

  describe('frequency formatting', () => {
    beforeEach(() => {
      server.use(
        http.get('/api/events/:eventId/frequency-audit-log', () =>
          HttpResponse.json(mockAuditEntries)
        )
      );
    });

    it('displays frequencies with MHz suffix', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);

      await waitFor(() => {
        expect(screen.getByText('36.500 MHz')).toBeInTheDocument();
        expect(screen.getByText('36.525 MHz')).toBeInTheDocument();
      });
    });

    it('displays dash for null frequencies', async () => {
      renderWithQuery(<FrequencyAuditLog eventId={mockEventId} />);

      await waitFor(() => {
        // The component shows entries with null alternateFrequency
        const entries = screen.getAllByText('—');
        expect(entries.length).toBeGreaterThan(0);
      });
    });
  });
});
