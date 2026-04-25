import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { RadioChannelsPage } from '../pages/events/RadioChannelsPage';
import type { RadioChannelListDto, ChannelAssignmentDto, ChannelAssignmentListDto } from '../lib/api';

// Mock auth so we can control commander vs. player role
vi.mock('../hooks/useAuth', () => ({
  useAuth: vi.fn(),
}));

import { useAuth } from '../hooks/useAuth';
const mockUseAuth = vi.mocked(useAuth);

const mockVhfChannel: RadioChannelListDto = {
  id: 'chan-1',
  name: 'Command Net',
  callSign: null,
  scope: 'VHF',
  assignmentCount: 2,
  conflictCount: 0,
};

const mockUhfChannel: RadioChannelListDto = {
  id: 'chan-2',
  name: 'Air Net',
  callSign: null,
  scope: 'UHF',
  assignmentCount: 0,
  conflictCount: 1,
};

const mockAssignment: ChannelAssignmentDto = {
  id: 'assign-1',
  radioChannelId: 'chan-1',
  channelName: 'Command Net',
  channelScope: 'VHF',
  squadId: 'squad-1',
  squadName: 'Alpha-1',
  primaryFrequency: 36.5,
  alternateFrequency: null,
  eventId: 'event-123',
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

const mockAssignmentList: ChannelAssignmentListDto = {
  total: 1,
  items: [mockAssignment],
};

const mockRoster = {
  platoons: [
    {
      id: 'platoon-1',
      name: 'Alpha Platoon',
      isCommandElement: false,
      hqPlayers: [],
      squads: [
        { id: 'squad-1', name: 'Alpha-1', players: [] },
        { id: 'squad-2', name: 'Alpha-2', players: [] },
      ],
    },
  ],
  unassignedPlayers: [],
};

function renderPage(role = 'faction_commander') {
  mockUseAuth.mockReturnValue({
    user: { id: 'user-1', role, email: 'test@test.com', callsign: 'TestUser' },
    isAuthenticated: true,
    login: vi.fn(),
    logout: vi.fn(),
  } as ReturnType<typeof useAuth>);

  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={['/events/event-123/radio-channels']}>
        <Routes>
          <Route path="/events/:id/radio-channels" element={<RadioChannelsPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('RadioChannelsPage', () => {
  beforeEach(() => {
    server.use(
      http.get('/api/events/:eventId/radio-channels', () =>
        HttpResponse.json([mockVhfChannel, mockUhfChannel])
      ),
      http.get('/api/events/:eventId/channel-assignments', () =>
        HttpResponse.json(mockAssignmentList)
      ),
      http.get('/api/events/:eventId/roster', () =>
        HttpResponse.json(mockRoster)
      )
    );
  });

  // AC-06: channel list shows name and scope
  it('renders channel list with name and scope badges', async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText('Command Net')).toBeDefined();
      expect(screen.getByText('Air Net')).toBeDefined();
    });

    // Scope badges present
    expect(screen.getAllByText('VHF').length).toBeGreaterThan(0);
    expect(screen.getAllByText('UHF').length).toBeGreaterThan(0);
  });

  // AC-03: frequency range info displayed
  it('shows frequency range info for each channel scope', async () => {
    renderPage();

    await waitFor(() => expect(screen.getByText('Command Net')).toBeDefined());

    expect(screen.getByText(/30\.0.87\.975 MHz/)).toBeDefined();
    expect(screen.getByText(/225.400 MHz/)).toBeDefined();
  });

  // AC-02: channel creation form with name field
  it('shows create form when New Channel clicked by commander', async () => {
    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('New Channel')).toBeDefined());
    fireEvent.click(screen.getByText('New Channel'));

    await waitFor(() => {
      expect(screen.getByLabelText('Channel name')).toBeDefined();
    });
  });

  // AC-04: VHF/UHF toggle visible in form
  it('create form shows VHF and UHF radio options', async () => {
    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('New Channel')).toBeDefined());
    fireEvent.click(screen.getByText('New Channel'));

    await waitFor(() => {
      // Radio buttons for VHF and UHF
      const vhfOption = screen.getByDisplayValue('VHF');
      const uhfOption = screen.getByDisplayValue('UHF');
      expect(vhfOption).toBeDefined();
      expect(uhfOption).toBeDefined();
    });
  });

  // Players cannot create channels (commander-only UI)
  it('does not show New Channel button for player role', async () => {
    renderPage('player');

    await waitFor(() => expect(screen.getByText('Command Net')).toBeDefined());

    expect(screen.queryByText('New Channel')).toBeNull();
  });

  // AC-05: duplicate name shows error message
  it('shows error message when channel name already exists', async () => {
    server.use(
      http.post('/api/events/:eventId/radio-channels', () =>
        HttpResponse.json({ detail: 'Channel name already exists in this operation.' }, { status: 409 })
      )
    );

    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('New Channel')).toBeDefined());
    fireEvent.click(screen.getByText('New Channel'));

    await waitFor(() => expect(screen.getByLabelText('Channel name')).toBeDefined());
    fireEvent.change(screen.getByLabelText('Channel name'), { target: { value: 'Command Net' } });
    fireEvent.click(screen.getByText('Save Channel'));

    await waitFor(() => {
      expect(screen.getByText(/Channel name already exists/)).toBeDefined();
    });
  });

  // AC-07: edit button visible for commander
  it('shows edit button for each channel when user is commander', async () => {
    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('Command Net')).toBeDefined());

    const editButtons = screen.getAllByRole('button', { name: /Edit channel/i });
    expect(editButtons.length).toBe(2);
  });

  // AC-07: edit button not visible for player
  it('does not show edit button for player role', async () => {
    renderPage('player');

    await waitFor(() => expect(screen.getByText('Command Net')).toBeDefined());

    const editButtons = screen.queryAllByRole('button', { name: /Edit channel/i });
    expect(editButtons.length).toBe(0);
  });

  // Conflict count displayed
  it('displays conflict badge when conflictCount > 0', async () => {
    renderPage();

    await waitFor(() => expect(screen.getByText('Air Net')).toBeDefined());

    expect(screen.getByText(/1 conflict/)).toBeDefined();
  });

  // Empty state
  it('shows empty state when no channels exist', async () => {
    server.use(
      http.get('/api/events/:eventId/radio-channels', () => HttpResponse.json([])),
      http.get('/api/events/:eventId/channel-assignments', () =>
        HttpResponse.json({ total: 0, items: [] })
      )
    );

    renderPage('faction_commander');

    await waitFor(() => {
      expect(screen.getByText('No radio channels yet.')).toBeDefined();
    });
  });

  // ── Story 2: Assignment section ───────────────────────────────────────────

  // AC-08: assignment list view per operation
  it('AC-08: shows assignment list with squad name, channel, frequency', async () => {
    renderPage('faction_commander');

    await waitFor(() => {
      expect(screen.getByText('Alpha-1')).toBeDefined();
      expect(screen.getByText('36.500 MHz')).toBeDefined();
    });
  });

  // AC-09: edit and delete controls per assignment
  it('AC-09: commander sees edit and delete buttons per assignment', async () => {
    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('Alpha-1')).toBeDefined());

    expect(screen.getByRole('button', { name: /Edit assignment for Alpha-1/i })).toBeDefined();
    expect(screen.getByRole('button', { name: /Delete assignment for Alpha-1/i })).toBeDefined();
  });

  // AC-09: player cannot edit/delete assignments
  it('AC-09: player does not see edit/delete for assignments', async () => {
    renderPage('player');

    await waitFor(() => expect(screen.getByText('Alpha-1')).toBeDefined());

    expect(screen.queryByRole('button', { name: /Edit assignment/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /Delete assignment/i })).toBeNull();
  });

  // AC-01: unit selector in create form
  it('AC-01: Assign Frequency form shows unit selector loaded from roster', async () => {
    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('Assign Frequency')).toBeDefined());
    fireEvent.click(screen.getByText('Assign Frequency'));

    await waitFor(() => {
      expect(screen.getByLabelText('Select unit')).toBeDefined();
    });

    // After roster loads, squads appear as options in the unit selector
    await waitFor(() => {
      const select = screen.getByLabelText('Select unit') as HTMLSelectElement;
      const options = Array.from(select.options).map((o) => o.text);
      expect(options.some((o) => o.includes('Alpha-1'))).toBe(true);
    });
  });

  // AC-02: channel selector in create form
  it('AC-02: Assign Frequency form shows channel selector', async () => {
    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('Assign Frequency')).toBeDefined());
    fireEvent.click(screen.getByText('Assign Frequency'));

    await waitFor(() => {
      expect(screen.getByLabelText('Select channel')).toBeDefined();
    });
  });

  // AC-03: frequency numeric input in create form
  it('AC-03: Assign Frequency form shows primary frequency input', async () => {
    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('Assign Frequency')).toBeDefined());
    fireEvent.click(screen.getByText('Assign Frequency'));

    await waitFor(() => {
      expect(screen.getByLabelText('Primary frequency (MHz)')).toBeDefined();
    });
  });

  // AC-10: real-time inline validation on frequency input
  it('AC-10: shows real-time validation error for out-of-range frequency', async () => {
    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('Assign Frequency')).toBeDefined());
    fireEvent.click(screen.getByText('Assign Frequency'));

    await waitFor(() => expect(screen.getByLabelText('Select channel')).toBeDefined());

    // Select VHF channel
    fireEvent.change(screen.getByLabelText('Select channel'), { target: { value: 'chan-1' } });

    // Enter out-of-range frequency
    await waitFor(() => expect(screen.getByLabelText('Primary frequency (MHz)')).toBeDefined());
    fireEvent.change(screen.getByLabelText('Primary frequency (MHz)'), { target: { value: '90.0' } });

    await waitFor(() => {
      expect(screen.getByText(/out of range|within VHF/i)).toBeDefined();
    });
  });

  // AC-10: real-time 25 kHz spacing validation
  it('AC-10: shows validation error for invalid 25 kHz spacing', async () => {
    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('Assign Frequency')).toBeDefined());
    fireEvent.click(screen.getByText('Assign Frequency'));

    await waitFor(() => expect(screen.getByLabelText('Select channel')).toBeDefined());
    fireEvent.change(screen.getByLabelText('Select channel'), { target: { value: 'chan-1' } });

    await waitFor(() => expect(screen.getByLabelText('Primary frequency (MHz)')).toBeDefined());
    fireEvent.change(screen.getByLabelText('Primary frequency (MHz)'), { target: { value: '36.012' } });

    await waitFor(() => {
      // The validation error specifically matches the alert role
      const alerts = screen.getAllByRole('alert');
      expect(alerts.some((a) => /25 kHz/i.test(a.textContent ?? ''))).toBe(true);
    });
  });

  // ── Story 3: Alternate Frequency ───────────────────────────────────────────

  // Alternate frequency column header appears in table
  it('Story 3: shows Alternate Frequency column in assignments table', async () => {
    renderPage('faction_commander');

    await waitFor(() => {
      expect(screen.getByText('Alternate Frequency')).toBeDefined();
    });
  });

  // Null alternate frequency shows em dash
  it('Story 3: shows em dash for null alternate frequency', async () => {
    renderPage('faction_commander');

    await waitFor(() => {
      expect(screen.getByText('Alpha-1')).toBeDefined();
      // The dash for null alternate frequency
      expect(screen.getByText('—')).toBeDefined();
    });
  });

  // Alternate frequency displayed when present
  it('Story 3: shows alternate frequency when present', async () => {
    const assignmentWithAlt: ChannelAssignmentDto = {
      ...mockAssignment,
      id: 'assign-alt',
      alternateFrequency: 36.525,
    };

    server.use(
      http.get('/api/events/:eventId/channel-assignments', () =>
        HttpResponse.json({ total: 1, items: [assignmentWithAlt] })
      )
    );

    renderPage('faction_commander');

    await waitFor(() => {
      expect(screen.getByText('36.525 MHz')).toBeDefined();
    });
  });

  // Alternate frequency input in create form
  it('Story 3: Assign Frequency form shows alternate frequency input', async () => {
    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('Assign Frequency')).toBeDefined());
    fireEvent.click(screen.getByText('Assign Frequency'));

    await waitFor(() => {
      expect(screen.getByLabelText('Alternate frequency (MHz)')).toBeDefined();
    });
  });

  // Alternate frequency real-time validation: out of range
  it('Story 3: shows validation error when alternate frequency is out of range', async () => {
    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('Assign Frequency')).toBeDefined());
    fireEvent.click(screen.getByText('Assign Frequency'));

    await waitFor(() => expect(screen.getByLabelText('Select channel')).toBeDefined());
    fireEvent.change(screen.getByLabelText('Select channel'), { target: { value: 'chan-1' } });

    await waitFor(() => expect(screen.getByLabelText('Alternate frequency (MHz)')).toBeDefined());
    fireEvent.change(screen.getByLabelText('Alternate frequency (MHz)'), { target: { value: '90.0' } });

    await waitFor(() => {
      const alerts = screen.getAllByRole('alert');
      expect(alerts.some((a) => /out of range|within VHF/i.test(a.textContent ?? ''))).toBe(true);
    });
  });

  // Alternate frequency real-time validation: same as primary
  it('Story 3: shows error when alternate frequency matches primary', async () => {
    renderPage('faction_commander');

    await waitFor(() => expect(screen.getByText('Assign Frequency')).toBeDefined());
    fireEvent.click(screen.getByText('Assign Frequency'));

    await waitFor(() => expect(screen.getByLabelText('Select channel')).toBeDefined());
    fireEvent.change(screen.getByLabelText('Select channel'), { target: { value: 'chan-1' } });

    await waitFor(() => expect(screen.getByLabelText('Primary frequency (MHz)')).toBeDefined());
    fireEvent.change(screen.getByLabelText('Primary frequency (MHz)'), { target: { value: '36.500' } });

    await waitFor(() => expect(screen.getByLabelText('Alternate frequency (MHz)')).toBeDefined());
    fireEvent.change(screen.getByLabelText('Alternate frequency (MHz)'), { target: { value: '36.500' } });

    await waitFor(() => {
      const alerts = screen.getAllByRole('alert');
      expect(
        alerts.some((a) => /cannot match primary/i.test(a.textContent ?? ''))
      ).toBe(true);
    });
  });

  // AC-04: Frequency conflict error from backend (409) is shown in create form
  it('Story 3 AC-04: shows conflict error when backend returns 409 on create', async () => {
    server.use(
      http.post('/api/events/:eventId/channel-assignments', () =>
        HttpResponse.json(
          { detail: "Frequency 36.500 MHz conflicts with primary frequency assigned to 'Alpha-1'." },
          { status: 409 }
        )
      )
    );

    renderPage('faction_commander');

    // Open the create form
    await waitFor(() => expect(screen.getByRole('button', { name: /Assign Frequency/i })).toBeDefined());
    fireEvent.click(screen.getByRole('button', { name: /Assign Frequency/i }));

    // Fill in required fields
    await waitFor(() => expect(screen.getByLabelText('Select channel')).toBeDefined());
    fireEvent.change(screen.getByLabelText('Select channel'), { target: { value: 'chan-1' } });
    fireEvent.change(screen.getByLabelText('Primary frequency (MHz)'), { target: { value: '36.500' } });
    fireEvent.change(screen.getByLabelText('Select unit'), { target: { value: 'squad-1' } });

    // Submit the form directly (avoids disabled-button timing issues)
    const assignForm = document.querySelector('form.border') as HTMLFormElement;
    fireEvent.submit(assignForm);

    await waitFor(() => {
      const alerts = screen.getAllByRole('alert');
      expect(alerts.some((a) => /conflict/i.test(a.textContent ?? ''))).toBe(true);
    });
  });
});
