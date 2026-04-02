import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FrequencyDisplay } from '../../components/frequency/FrequencyDisplay';
import type { EventFrequenciesDto } from '../../lib/api';

const squadLevel = {
  id: 'sq-1',
  name: 'Alpha Squad',
  primary: '148.500',
  backup: '148.600',
};

const platoonLevel = {
  id: 'pl-1',
  name: 'Alpha Platoon',
  primary: '149.000',
  backup: null,
};


describe('FrequencyDisplay', () => {
  it('FrequencyDisplay_WithSquadOnly_RendersSquadSection', () => {
    const frequencies: EventFrequenciesDto = {
      squad: squadLevel,
      platoon: null,
      command: null,
    };

    render(<FrequencyDisplay frequencies={frequencies} />);

    expect(screen.getByText('Alpha Squad')).toBeInTheDocument();
    expect(screen.getByText('148.500')).toBeInTheDocument();
    expect(screen.queryByText('Alpha Platoon')).not.toBeInTheDocument();
    expect(screen.queryByText('Command')).not.toBeInTheDocument();
  });

  it('FrequencyDisplay_WithAllNull_RendersEmptyState', () => {
    const frequencies: EventFrequenciesDto = {
      squad: null,
      platoon: null,
      command: null,
    };

    render(<FrequencyDisplay frequencies={frequencies} />);

    expect(screen.getByText('No frequencies assigned')).toBeInTheDocument();
  });

  it('FrequencyDisplay_WithSquadAndPlatoon_RendersBothSections', () => {
    const frequencies: EventFrequenciesDto = {
      squad: squadLevel,
      platoon: platoonLevel,
      command: null,
    };

    render(<FrequencyDisplay frequencies={frequencies} />);

    expect(screen.getByText('Alpha Squad')).toBeInTheDocument();
    expect(screen.getByText('Alpha Platoon')).toBeInTheDocument();
    expect(screen.queryByText('Command')).not.toBeInTheDocument();
  });
});
