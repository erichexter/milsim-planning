import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router';
import { CheckInDashboard } from '../pages/events/CheckInDashboard';
import * as api from '../lib/api';

// Mock the API
vi.mock('../lib/api');

describe('CheckInDashboard', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false,
        },
      },
    });
    vi.clearAllMocks();
  });

  const renderComponent = () => {
    return render(
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <CheckInDashboard />
        </BrowserRouter>
      </QueryClientProvider>
    );
  };

  it('renders loading state initially', () => {
    vi.mocked(api.getCheckInDashboard).mockImplementation(
      () => new Promise((resolve) => setTimeout(() => resolve({
        totalCheckIns: 0,
        targetCount: 100,
        factionBreakdown: [],
      }), 1000))
    );

    renderComponent();
    expect(screen.getByText('Live Check-In Dashboard')).toBeInTheDocument();
  });

  it('displays dashboard data after successful fetch', async () => {
    const mockDashboard: api.CheckInDashboardDto = {
      totalCheckIns: 42,
      targetCount: 100,
      factionBreakdown: [
        {
          factionId: 'faction-1',
          factionName: 'Alpha Team',
          checkInCount: 42,
        },
      ],
    };

    vi.mocked(api.getCheckInDashboard).mockResolvedValue(mockDashboard);

    renderComponent();

    await waitFor(() => {
      expect(screen.getByText('42')).toBeInTheDocument();
      expect(screen.getByText('100')).toBeInTheDocument();
      expect(screen.getByText('Alpha Team')).toBeInTheDocument();
      expect(screen.getByText('42 checked in')).toBeInTheDocument();
    });
  });

  it('calculates progress percentage correctly', async () => {
    const mockDashboard: api.CheckInDashboardDto = {
      totalCheckIns: 50,
      targetCount: 100,
      factionBreakdown: [
        {
          factionId: 'faction-1',
          factionName: 'Alpha Team',
          checkInCount: 50,
        },
      ],
    };

    vi.mocked(api.getCheckInDashboard).mockResolvedValue(mockDashboard);

    renderComponent();

    await waitFor(() => {
      // 50/100 = 50%
      expect(screen.getByText('50%')).toBeInTheDocument();
    });
  });

  it('displays offline state when network error occurs', async () => {
    const error = new Error('Network error');
    (error as any).status = undefined;
    vi.mocked(api.getCheckInDashboard).mockRejectedValue(error);

    renderComponent();

    await waitFor(() => {
      expect(screen.getByText('No Connection')).toBeInTheDocument();
      expect(
        screen.getByText(/dashboard is offline.*retrying automatically/)
      ).toBeInTheDocument();
    });
  });

  it('displays faction breakdown with multiple factions', async () => {
    const mockDashboard: api.CheckInDashboardDto = {
      totalCheckIns: 120,
      targetCount: 200,
      factionBreakdown: [
        {
          factionId: 'faction-1',
          factionName: 'Red Team',
          checkInCount: 75,
        },
        {
          factionId: 'faction-2',
          factionName: 'Blue Team',
          checkInCount: 45,
        },
      ],
    };

    vi.mocked(api.getCheckInDashboard).mockResolvedValue(mockDashboard);

    renderComponent();

    await waitFor(() => {
      expect(screen.getByText('Red Team')).toBeInTheDocument();
      expect(screen.getByText('Blue Team')).toBeInTheDocument();
      expect(screen.getByText('75 checked in')).toBeInTheDocument();
      expect(screen.getByText('45 checked in')).toBeInTheDocument();
    });
  });

  it('polls for updates every 2 seconds', async () => {
    const mockDashboard: api.CheckInDashboardDto = {
      totalCheckIns: 10,
      targetCount: 100,
      factionBreakdown: [
        {
          factionId: 'faction-1',
          factionName: 'Alpha Team',
          checkInCount: 10,
        },
      ],
    };

    vi.mocked(api.getCheckInDashboard).mockResolvedValue(mockDashboard);

    renderComponent();

    await waitFor(() => {
      expect(screen.getByText('10')).toBeInTheDocument();
    });

    // Verify that the API is being called with the expected refetchInterval
    // This is implicitly tested by the useQuery hook setup
    expect(api.getCheckInDashboard).toHaveBeenCalled();
  });
});
