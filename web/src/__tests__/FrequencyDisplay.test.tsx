import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FrequencyDisplay } from '../components/frequency/FrequencyDisplay';
import type { FrequencyResponseDto } from '../lib/api';

// Mock the useFrequencies hook
const mockUseFrequencies = vi.fn();
vi.mock('../hooks/useFrequencies', () => ({
  useFrequencies: (...args: unknown[]) => mockUseFrequencies(...args),
}));

function mockReturn(data: FrequencyResponseDto | undefined, isLoading = false, error: Error | null = null) {
  mockUseFrequencies.mockReturnValue({ data, isLoading, error, refetch: vi.fn() });
}

describe('FrequencyDisplay', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders squad section when API returns squads data', () => {
    mockReturn({
      command: null,
      platoons: null,
      squads: [{ squadId: 'sq-1', squadName: 'Alpha', platoonId: 'pl-1', primary: '145.500', backup: '145.600' }],
    });

    render(<FrequencyDisplay eventId="evt-1" />);

    expect(screen.getByTestId('squad-section')).toBeInTheDocument();
    expect(screen.getByText('Alpha')).toBeInTheDocument();
    expect(screen.getByText('Pri: 145.500')).toBeInTheDocument();
    expect(screen.getByText('Bkp: 145.600')).toBeInTheDocument();
  });

  it('renders platoon section when API returns platoons data', () => {
    mockReturn({
      command: null,
      platoons: [{ platoonId: 'pl-1', platoonName: '1st Platoon', primary: '150.000', backup: null }],
      squads: null,
    });

    render(<FrequencyDisplay eventId="evt-1" />);

    expect(screen.getByTestId('platoon-section')).toBeInTheDocument();
    expect(screen.getByText('1st Platoon')).toBeInTheDocument();
    expect(screen.getByText('Pri: 150.000')).toBeInTheDocument();
    expect(screen.getByText(/Bkp:.*—/)).toBeInTheDocument();
  });

  it('renders command section when API returns command data', () => {
    mockReturn({
      command: { primary: '155.000', backup: '155.100' },
      platoons: null,
      squads: null,
    });

    render(<FrequencyDisplay eventId="evt-1" />);

    expect(screen.getByTestId('command-section')).toBeInTheDocument();
    expect(screen.getByText('Pri: 155.000')).toBeInTheDocument();
    expect(screen.getByText('Bkp: 155.100')).toBeInTheDocument();
  });

  it('hides squad section when API returns squads: null', () => {
    mockReturn({
      command: { primary: '155.000', backup: null },
      platoons: null,
      squads: null,
    });

    render(<FrequencyDisplay eventId="evt-1" />);

    expect(screen.queryByTestId('squad-section')).not.toBeInTheDocument();
  });

  it('hides platoon section when API returns platoons: null', () => {
    mockReturn({
      command: null,
      platoons: null,
      squads: [{ squadId: 'sq-1', squadName: 'Alpha', platoonId: 'pl-1', primary: '145.500', backup: null }],
    });

    render(<FrequencyDisplay eventId="evt-1" />);

    expect(screen.queryByTestId('platoon-section')).not.toBeInTheDocument();
  });

  it('hides command section when API returns command: null', () => {
    mockReturn({
      command: null,
      platoons: [{ platoonId: 'pl-1', platoonName: '1st Platoon', primary: '150.000', backup: null }],
      squads: null,
    });

    render(<FrequencyDisplay eventId="evt-1" />);

    expect(screen.queryByTestId('command-section')).not.toBeInTheDocument();
  });

  it('shows loading state while query is loading', () => {
    mockReturn(undefined, true);

    render(<FrequencyDisplay eventId="evt-1" />);

    expect(screen.getByTestId('frequency-loading')).toBeInTheDocument();
    expect(screen.queryByTestId('command-section')).not.toBeInTheDocument();
    expect(screen.queryByTestId('platoon-section')).not.toBeInTheDocument();
    expect(screen.queryByTestId('squad-section')).not.toBeInTheDocument();
  });
});
