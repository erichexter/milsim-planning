import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FrequencyEditor } from '../components/frequency/FrequencyEditor';
import type { FrequencyVisibilityDto } from '../lib/api';

const SQUAD_ID = 'b1b2c3d4-0000-0000-0000-000000000001';
const PLATOON_ID = 'c1b2c3d4-0000-0000-0000-000000000001';
const FACTION_ID = 'd1b2c3d4-0000-0000-0000-000000000001';

describe('FrequencyEditor', () => {
  it('renders squad editor when squad data and handler provided', () => {
    const frequencies: FrequencyVisibilityDto = {
      squad: { squadId: SQUAD_ID, squadName: 'Alpha Squad', primary: '43.325', backup: null },
      platoon: null,
      command: null,
    };

    render(
      <FrequencyEditor
        frequencies={frequencies}
        factionId={FACTION_ID}
        onUpdateSquad={vi.fn()}
      />
    );

    expect(screen.getByText('Alpha Squad')).toBeInTheDocument();
    expect(screen.getByLabelText('Alpha Squad primary frequency')).toHaveValue('43.325');
    expect(screen.getByLabelText('Alpha Squad backup frequency')).toHaveValue('');
  });

  it('renders platoon editor when platoon data and handler provided', () => {
    const frequencies: FrequencyVisibilityDto = {
      squad: null,
      platoon: { platoonId: PLATOON_ID, platoonName: '1st Platoon', primary: '41.000', backup: '42.000' },
      command: null,
    };

    render(
      <FrequencyEditor
        frequencies={frequencies}
        factionId={FACTION_ID}
        onUpdatePlatoon={vi.fn()}
      />
    );

    expect(screen.getByText('1st Platoon')).toBeInTheDocument();
    expect(screen.getByLabelText('1st Platoon primary frequency')).toHaveValue('41.000');
    expect(screen.getByLabelText('1st Platoon backup frequency')).toHaveValue('42.000');
  });

  it('renders command editor when command data and handler provided', () => {
    const frequencies: FrequencyVisibilityDto = {
      squad: null,
      platoon: null,
      command: { factionId: FACTION_ID, primary: '40.000', backup: null },
    };

    render(
      <FrequencyEditor
        frequencies={frequencies}
        factionId={FACTION_ID}
        onUpdateFaction={vi.fn()}
      />
    );

    expect(screen.getByText('Command')).toBeInTheDocument();
    expect(screen.getByLabelText('Command primary frequency')).toHaveValue('40.000');
  });

  it('does not render section when handler is missing', () => {
    const frequencies: FrequencyVisibilityDto = {
      squad: { squadId: SQUAD_ID, squadName: 'Alpha Squad', primary: '43.325', backup: null },
      platoon: null,
      command: null,
    };

    render(
      <FrequencyEditor
        frequencies={frequencies}
        factionId={FACTION_ID}
      />
    );

    expect(screen.queryByText('Alpha Squad')).not.toBeInTheDocument();
  });

  it('calls onUpdateSquad with correct body shape on save', async () => {
    const user = userEvent.setup();
    const onUpdateSquad = vi.fn().mockResolvedValue(undefined);
    const frequencies: FrequencyVisibilityDto = {
      squad: { squadId: SQUAD_ID, squadName: 'Alpha Squad', primary: '43.325', backup: null },
      platoon: null,
      command: null,
    };

    render(
      <FrequencyEditor
        frequencies={frequencies}
        factionId={FACTION_ID}
        onUpdateSquad={onUpdateSquad}
      />
    );

    const backupInput = screen.getByLabelText('Alpha Squad backup frequency');
    await user.type(backupInput, '44.000');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onUpdateSquad).toHaveBeenCalledWith(SQUAD_ID, {
      primary: '43.325',
      backup: '44.000',
    });
  });

  it('calls onUpdatePlatoon with correct body shape on save', async () => {
    const user = userEvent.setup();
    const onUpdatePlatoon = vi.fn().mockResolvedValue(undefined);
    const frequencies: FrequencyVisibilityDto = {
      squad: null,
      platoon: { platoonId: PLATOON_ID, platoonName: '1st Platoon', primary: '41.000', backup: '42.000' },
      command: null,
    };

    render(
      <FrequencyEditor
        frequencies={frequencies}
        factionId={FACTION_ID}
        onUpdatePlatoon={onUpdatePlatoon}
      />
    );

    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onUpdatePlatoon).toHaveBeenCalledWith(PLATOON_ID, {
      primary: '41.000',
      backup: '42.000',
    });
  });

  it('calls onUpdateFaction with correct body shape on save', async () => {
    const user = userEvent.setup();
    const onUpdateFaction = vi.fn().mockResolvedValue(undefined);
    const frequencies: FrequencyVisibilityDto = {
      squad: null,
      platoon: null,
      command: { factionId: FACTION_ID, primary: '40.000', backup: null },
    };

    render(
      <FrequencyEditor
        frequencies={frequencies}
        factionId={FACTION_ID}
        onUpdateFaction={onUpdateFaction}
      />
    );

    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onUpdateFaction).toHaveBeenCalledWith(FACTION_ID, {
      primary: '40.000',
      backup: null,
    });
  });

  it('disables save button while saving', async () => {
    const user = userEvent.setup();
    let resolvePromise: () => void;
    const savePromise = new Promise<void>((resolve) => { resolvePromise = resolve; });
    const onUpdateSquad = vi.fn().mockReturnValue(savePromise);

    const frequencies: FrequencyVisibilityDto = {
      squad: { squadId: SQUAD_ID, squadName: 'Alpha Squad', primary: '43.325', backup: null },
      platoon: null,
      command: null,
    };

    render(
      <FrequencyEditor
        frequencies={frequencies}
        factionId={FACTION_ID}
        onUpdateSquad={onUpdateSquad}
      />
    );

    await user.click(screen.getByRole('button', { name: 'Save' }));
    expect(screen.getByRole('button', { name: 'Saving\u2026' })).toBeDisabled();

    resolvePromise!();
  });
});
