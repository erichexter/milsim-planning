import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FrequencyDisplay } from '../components/frequency/FrequencyDisplay';
import type { FrequencyVisibilityDto } from '../lib/api';

const SQUAD_ID = 'b1b2c3d4-0000-0000-0000-000000000001';
const PLATOON_ID = 'c1b2c3d4-0000-0000-0000-000000000001';
const FACTION_ID = 'd1b2c3d4-0000-0000-0000-000000000001';

describe('FrequencyDisplay', () => {
  describe('player role (squad only)', () => {
    it('renders squad section when squad is present', () => {
      const frequencies: FrequencyVisibilityDto = {
        squad: { squadId: SQUAD_ID, squadName: 'Alpha Squad', primary: '43.325', backup: null },
        platoon: null,
        command: null,
      };

      render(<FrequencyDisplay frequencies={frequencies} />);

      expect(screen.getByRole('region', { name: 'Squad Frequencies' })).toBeInTheDocument();
      expect(screen.getByText('Alpha Squad')).toBeInTheDocument();
      expect(screen.getByText('43.325')).toBeInTheDocument();
    });

    it('does not render platoon or command sections when null', () => {
      const frequencies: FrequencyVisibilityDto = {
        squad: { squadId: SQUAD_ID, squadName: 'Alpha Squad', primary: '43.325', backup: null },
        platoon: null,
        command: null,
      };

      render(<FrequencyDisplay frequencies={frequencies} />);

      expect(screen.queryByRole('region', { name: 'Platoon Frequencies' })).not.toBeInTheDocument();
      expect(screen.queryByRole('region', { name: 'Command Frequencies' })).not.toBeInTheDocument();
    });

    it('shows "no frequency set" for null backup', () => {
      const frequencies: FrequencyVisibilityDto = {
        squad: { squadId: SQUAD_ID, squadName: 'Alpha Squad', primary: '43.325', backup: null },
        platoon: null,
        command: null,
      };

      render(<FrequencyDisplay frequencies={frequencies} />);

      const noFreqElements = screen.getAllByText('no frequency set');
      expect(noFreqElements.length).toBeGreaterThanOrEqual(1);
    });
  });

  describe('squad_leader role (squad + platoon)', () => {
    it('renders both squad and platoon sections', () => {
      const frequencies: FrequencyVisibilityDto = {
        squad: { squadId: SQUAD_ID, squadName: 'Alpha Squad', primary: '43.325', backup: null },
        platoon: { platoonId: PLATOON_ID, platoonName: '1st Platoon', primary: '41.000', backup: '42.000' },
        command: null,
      };

      render(<FrequencyDisplay frequencies={frequencies} />);

      expect(screen.getByRole('region', { name: 'Squad Frequencies' })).toBeInTheDocument();
      expect(screen.getByRole('region', { name: 'Platoon Frequencies' })).toBeInTheDocument();
      expect(screen.getByText('1st Platoon')).toBeInTheDocument();
      expect(screen.getByText('41.000')).toBeInTheDocument();
      expect(screen.getByText('42.000')).toBeInTheDocument();
    });
  });

  describe('platoon_leader role (platoon + command)', () => {
    it('renders platoon and command sections, no squad', () => {
      const frequencies: FrequencyVisibilityDto = {
        squad: null,
        platoon: { platoonId: PLATOON_ID, platoonName: '1st Platoon', primary: '41.000', backup: null },
        command: { factionId: FACTION_ID, primary: '40.000', backup: null },
      };

      render(<FrequencyDisplay frequencies={frequencies} />);

      expect(screen.queryByRole('region', { name: 'Squad Frequencies' })).not.toBeInTheDocument();
      expect(screen.getByRole('region', { name: 'Platoon Frequencies' })).toBeInTheDocument();
      expect(screen.getByRole('region', { name: 'Command Frequencies' })).toBeInTheDocument();
      expect(screen.getByText('40.000')).toBeInTheDocument();
    });
  });

  describe('commander role (all sections)', () => {
    it('renders all three sections', () => {
      const frequencies: FrequencyVisibilityDto = {
        squad: { squadId: SQUAD_ID, squadName: 'Alpha Squad', primary: '43.325', backup: null },
        platoon: { platoonId: PLATOON_ID, platoonName: '1st Platoon', primary: '41.000', backup: null },
        command: { factionId: FACTION_ID, primary: '40.000', backup: null },
      };

      render(<FrequencyDisplay frequencies={frequencies} />);

      expect(screen.getByRole('region', { name: 'Squad Frequencies' })).toBeInTheDocument();
      expect(screen.getByRole('region', { name: 'Platoon Frequencies' })).toBeInTheDocument();
      expect(screen.getByRole('region', { name: 'Command Frequencies' })).toBeInTheDocument();
    });
  });

  describe('empty state', () => {
    it('shows fallback message when all sections are null', () => {
      const frequencies: FrequencyVisibilityDto = {
        squad: null,
        platoon: null,
        command: null,
      };

      render(<FrequencyDisplay frequencies={frequencies} />);

      expect(screen.getByText('No frequency data available.')).toBeInTheDocument();
    });
  });

  describe('null frequency values', () => {
    it('shows "no frequency set" for all null primary and backup', () => {
      const frequencies: FrequencyVisibilityDto = {
        squad: { squadId: SQUAD_ID, squadName: 'Alpha Squad', primary: null, backup: null },
        platoon: null,
        command: null,
      };

      render(<FrequencyDisplay frequencies={frequencies} />);

      const noFreqElements = screen.getAllByText('no frequency set');
      expect(noFreqElements).toHaveLength(2); // primary and backup both null
    });
  });
});
