import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FrequencyDisplay } from '../../components/frequency/FrequencyDisplay';
import type { FrequenciesDto } from '../../lib/api';

const squadFreq = { id: 'squad-1', name: 'Alpha Squad', primary: '148.500', backup: null };
const platoonFreq = { id: 'plat-1', name: '1 Platoon', primary: '148.100', backup: '148.150' };
const commandFreq = { id: 'faction-1', name: 'BLUFOR', primary: '147.000', backup: '147.500' };

const playerFrequencies: FrequenciesDto = {
  squad: squadFreq,
  platoon: null,
  command: null,
  allPlatoons: null,
  allSquads: null,
};

const squadLeaderFrequencies: FrequenciesDto = {
  squad: squadFreq,
  platoon: platoonFreq,
  command: null,
  allPlatoons: null,
  allSquads: null,
};

const platoonLeaderFrequencies: FrequenciesDto = {
  squad: null,
  platoon: platoonFreq,
  command: commandFreq,
  allPlatoons: null,
  allSquads: null,
};

const commanderFrequencies: FrequenciesDto = {
  squad: null,
  platoon: null,
  command: commandFreq,
  allPlatoons: [platoonFreq],
  allSquads: [squadFreq],
};

describe('FrequencyDisplay', () => {
  describe('player role', () => {
    it('renders squad section', () => {
      render(<FrequencyDisplay frequencies={playerFrequencies} role="player" />);
      expect(screen.getByTestId('freq-squad')).toBeInTheDocument();
      expect(screen.getByText('148.500')).toBeInTheDocument();
    });

    it('does not render platoon section', () => {
      render(<FrequencyDisplay frequencies={playerFrequencies} role="player" />);
      expect(screen.queryByTestId('freq-platoon')).not.toBeInTheDocument();
    });

    it('does not render command section', () => {
      render(<FrequencyDisplay frequencies={playerFrequencies} role="player" />);
      expect(screen.queryByTestId('freq-command')).not.toBeInTheDocument();
    });
  });

  describe('squad_leader role', () => {
    it('renders squad and platoon sections', () => {
      render(<FrequencyDisplay frequencies={squadLeaderFrequencies} role="squad_leader" />);
      expect(screen.getByTestId('freq-squad')).toBeInTheDocument();
      expect(screen.getByTestId('freq-platoon')).toBeInTheDocument();
    });

    it('does not render command section', () => {
      render(<FrequencyDisplay frequencies={squadLeaderFrequencies} role="squad_leader" />);
      expect(screen.queryByTestId('freq-command')).not.toBeInTheDocument();
    });
  });

  describe('platoon_leader role', () => {
    it('renders platoon and command sections', () => {
      render(<FrequencyDisplay frequencies={platoonLeaderFrequencies} role="platoon_leader" />);
      expect(screen.getByTestId('freq-platoon')).toBeInTheDocument();
      expect(screen.getByTestId('freq-command')).toBeInTheDocument();
    });

    it('does not render squad section', () => {
      render(<FrequencyDisplay frequencies={platoonLeaderFrequencies} role="platoon_leader" />);
      expect(screen.queryByTestId('freq-squad')).not.toBeInTheDocument();
    });
  });

  describe('faction_commander role', () => {
    it('renders command section', () => {
      render(<FrequencyDisplay frequencies={commanderFrequencies} role="faction_commander" />);
      expect(screen.getByTestId('freq-command')).toBeInTheDocument();
    });

    it('renders all platoons and all squads sections', () => {
      render(<FrequencyDisplay frequencies={commanderFrequencies} role="faction_commander" />);
      expect(screen.getByTestId('freq-all-platoons')).toBeInTheDocument();
      expect(screen.getByTestId('freq-all-squads')).toBeInTheDocument();
    });

    it('does not render squad or platoon own sections', () => {
      render(<FrequencyDisplay frequencies={commanderFrequencies} role="faction_commander" />);
      expect(screen.queryByTestId('freq-squad')).not.toBeInTheDocument();
      expect(screen.queryByTestId('freq-platoon')).not.toBeInTheDocument();
    });
  });

  describe('null frequency data', () => {
    it('renders dash when primary is null', () => {
      const freqs: FrequenciesDto = {
        squad: { id: 'sq-1', name: 'Bravo Squad', primary: null, backup: null },
        platoon: null, command: null, allPlatoons: null, allSquads: null,
      };
      render(<FrequencyDisplay frequencies={freqs} role="player" />);
      expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(1);
    });
  });
});
