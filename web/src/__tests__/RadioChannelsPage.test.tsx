import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { RadioChannelsPage } from '../pages/events/RadioChannelsPage';
import type { RadioChannelListDto } from '../lib/api';

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
      http.get('/api/events/:eventId/radio-channels', () => HttpResponse.json([]))
    );

    renderPage('faction_commander');

    await waitFor(() => {
      expect(screen.getByText('No radio channels yet.')).toBeDefined();
    });
  });
});
