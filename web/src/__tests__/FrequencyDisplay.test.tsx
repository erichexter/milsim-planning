import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { FrequencyDisplay } from '../components/FrequencyDisplay';
import type { EventFrequenciesDto } from '../lib/api';

const commanderData: EventFrequenciesDto = {
  command: { primary: '148.000', backup: '149.000' },
  platoons: [
    { platoonId: 'p1', platoonName: 'Alpha Platoon', primary: '150.000', backup: '151.000' },
  ],
  squads: [
    { squadId: 's1', squadName: 'Alpha 1', platoonId: 'p1', primary: '152.000', backup: null },
    { squadId: 's2', squadName: 'Alpha 2', platoonId: 'p1', primary: null, backup: null },
  ],
};

const playerData: EventFrequenciesDto = {
  command: null,
  platoons: [],
  squads: [
    { squadId: 's1', squadName: 'Alpha 1', platoonId: 'p1', primary: '152.000', backup: null },
  ],
};

const emptyData: EventFrequenciesDto = {
  command: null,
  platoons: [],
  squads: [],
};

describe('FrequencyDisplay', () => {
  it('renders all tiers for commander view', () => {
    render(<FrequencyDisplay data={commanderData} />);
    expect(screen.getByText('Radio Frequencies')).toBeInTheDocument();
    expect(screen.getByText('Command')).toBeInTheDocument();
    expect(screen.getByText('Platoons')).toBeInTheDocument();
    expect(screen.getByText('Squads')).toBeInTheDocument();
    expect(screen.getByText(/148\.000/)).toBeInTheDocument();
    expect(screen.getByText(/150\.000/)).toBeInTheDocument();
    expect(screen.getByText(/152\.000/)).toBeInTheDocument();
  });

  it('renders only squad frequency for player view', () => {
    render(<FrequencyDisplay data={playerData} />);
    expect(screen.getByText(/152\.000/)).toBeInTheDocument();
    expect(screen.queryByText('Command')).not.toBeInTheDocument();
    expect(screen.queryByText('Platoons')).not.toBeInTheDocument();
  });

  it('renders empty state when no frequencies visible', () => {
    render(<FrequencyDisplay data={emptyData} />);
    expect(screen.getByText(/no frequencies available/i)).toBeInTheDocument();
  });

  it('shows edit buttons for commander with edit permissions', () => {
    render(
      <FrequencyDisplay
        data={commanderData}
        canEditCommand
        canEditPlatoon
        canEditSquad
        onUpdateCommand={vi.fn()}
        onUpdatePlatoon={vi.fn()}
        onUpdateSquad={vi.fn()}
      />
    );
    const editButtons = screen.getAllByText('Edit');
    // command + 1 platoon + 2 squads = 4 edit buttons
    expect(editButtons).toHaveLength(4);
  });

  it('does not show edit buttons without edit permissions', () => {
    render(<FrequencyDisplay data={commanderData} />);
    expect(screen.queryByText('Edit')).not.toBeInTheDocument();
  });

  it('opens edit form and calls onUpdateSquad on save', () => {
    const onUpdateSquad = vi.fn();
    render(
      <FrequencyDisplay
        data={playerData}
        canEditSquad
        onUpdateSquad={onUpdateSquad}
      />
    );

    fireEvent.click(screen.getByText('Edit'));
    expect(screen.getByLabelText('Primary')).toBeInTheDocument();
    expect(screen.getByLabelText('Backup')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Primary'), { target: { value: '155.000' } });
    fireEvent.change(screen.getByLabelText('Backup'), { target: { value: '156.000' } });
    fireEvent.click(screen.getByText('Save'));

    expect(onUpdateSquad).toHaveBeenCalledWith('s1', {
      primary: '155.000',
      backup: '156.000',
    });
  });

  it('cancels edit without calling save', () => {
    const onUpdateSquad = vi.fn();
    render(
      <FrequencyDisplay
        data={playerData}
        canEditSquad
        onUpdateSquad={onUpdateSquad}
      />
    );

    fireEvent.click(screen.getByText('Edit'));
    fireEvent.change(screen.getByLabelText('Primary'), { target: { value: '999.000' } });
    fireEvent.click(screen.getByText('Cancel'));

    expect(onUpdateSquad).not.toHaveBeenCalled();
    // Should be back to display mode
    expect(screen.getByText('Edit')).toBeInTheDocument();
  });

  it('shows "Not set" for null frequencies', () => {
    const data: EventFrequenciesDto = {
      command: null,
      platoons: [],
      squads: [
        { squadId: 's2', squadName: 'Alpha 2', platoonId: 'p1', primary: null, backup: null },
      ],
    };
    render(<FrequencyDisplay data={data} />);
    expect(screen.getByText(/Not set/)).toBeInTheDocument();
  });
});
