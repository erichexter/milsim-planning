import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { FrequencyEditor } from '../../components/frequency/FrequencyEditor';

const level = { id: 'squad-1', name: 'Alpha Squad', primary: '148.500', backup: null };

function renderEditor(role: string, scope: 'squad' | 'platoon' | 'faction' = 'squad') {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <FrequencyEditor
        eventId="evt-1"
        scope={scope}
        entityId="squad-1"
        level={level}
        role={role}
      />
    </QueryClientProvider>
  );
}

describe('FrequencyEditor', () => {
  describe('visibility by role', () => {
    it('renders edit button for squad_leader on squad scope', () => {
      renderEditor('squad_leader', 'squad');
      expect(screen.getByTestId('freq-edit-btn-squad')).toBeInTheDocument();
    });

    it('renders edit button for faction_commander on squad scope', () => {
      renderEditor('faction_commander', 'squad');
      expect(screen.getByTestId('freq-edit-btn-squad')).toBeInTheDocument();
    });

    it('does not render edit button for player on squad scope', () => {
      renderEditor('player', 'squad');
      expect(screen.queryByTestId('freq-edit-btn-squad')).not.toBeInTheDocument();
    });

    it('does not render edit button for squad_leader on platoon scope', () => {
      renderEditor('squad_leader', 'platoon');
      expect(screen.queryByTestId('freq-edit-btn-platoon')).not.toBeInTheDocument();
    });

    it('renders edit button for platoon_leader on platoon scope', () => {
      renderEditor('platoon_leader', 'platoon');
      expect(screen.getByTestId('freq-edit-btn-platoon')).toBeInTheDocument();
    });
  });

  describe('edit form interaction', () => {
    it('opens form when edit button is clicked', () => {
      renderEditor('squad_leader', 'squad');
      fireEvent.click(screen.getByTestId('freq-edit-btn-squad'));
      expect(screen.getByTestId('freq-editor-squad')).toBeInTheDocument();
    });

    it('submits PATCH with primary and backup values', async () => {
      let capturedBody: { primary: string | null; backup: string | null } | undefined;
      server.use(
        http.patch('/api/squads/squad-1/frequencies', async ({ request }) => {
          capturedBody = (await request.json()) as { primary: string | null; backup: string | null };
          return new HttpResponse(null, { status: 204 });
        })
      );

      renderEditor('squad_leader', 'squad');
      fireEvent.click(screen.getByTestId('freq-edit-btn-squad'));

      const primaryInput = screen.getByLabelText('Primary');
      const backupInput = screen.getByLabelText('Backup');

      fireEvent.change(primaryInput, { target: { value: '149.000' } });
      fireEvent.change(backupInput, { target: { value: '149.500' } });

      fireEvent.submit(screen.getByTestId('freq-editor-squad'));

      await waitFor(() => {
        expect(capturedBody).toBeDefined();
        expect(capturedBody!.primary).toBe('149.000');
        expect(capturedBody!.backup).toBe('149.500');
      });
    });

    it('submits null when fields are cleared', async () => {
      let capturedBody: { primary: string | null; backup: string | null } | undefined;
      server.use(
        http.patch('/api/squads/squad-1/frequencies', async ({ request }) => {
          capturedBody = (await request.json()) as { primary: string | null; backup: string | null };
          return new HttpResponse(null, { status: 204 });
        })
      );

      renderEditor('squad_leader', 'squad');
      fireEvent.click(screen.getByTestId('freq-edit-btn-squad'));

      const primaryInput = screen.getByLabelText('Primary');
      fireEvent.change(primaryInput, { target: { value: '' } });

      fireEvent.submit(screen.getByTestId('freq-editor-squad'));

      await waitFor(() => {
        expect(capturedBody).toBeDefined();
        expect(capturedBody!.primary).toBeNull();
      });
    });

    it('closes form when Cancel is clicked', () => {
      renderEditor('squad_leader', 'squad');
      fireEvent.click(screen.getByTestId('freq-edit-btn-squad'));
      expect(screen.getByTestId('freq-editor-squad')).toBeInTheDocument();

      fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
      expect(screen.queryByTestId('freq-editor-squad')).not.toBeInTheDocument();
    });
  });
});
