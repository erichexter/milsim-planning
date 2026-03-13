import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Routes, Route } from 'react-router';
import { CsvImportPage } from '../pages/roster/CsvImportPage';

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={['/events/evt-1/roster/import']}>
        <Routes>
          <Route path="/events/:id/roster/import" element={<CsvImportPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('CsvImportPage', () => {
  it('shows error-only rows and valid count summary', async () => {
    server.use(
      http.post('/api/events/evt-1/roster/validate', () =>
        HttpResponse.json({
          fatalError: null,
          validCount: 5,
          errorCount: 1,
          warningCount: 0,
          errors: [
            { row: 3, field: 'email', message: 'Invalid or missing email', severity: 'Error' },
          ],
        })
      )
    );
    renderPage();

    // Simulate file drop via the hidden file input
    const file = new File(['name,email\ntest,bad-email'], 'roster.csv', { type: 'text/csv' });
    const input = document.querySelector('input[type="file"]')!;
    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => {
      expect(screen.getByText(/5 valid/)).toBeInTheDocument();
      expect(screen.getByText(/1 errors/)).toBeInTheDocument();
    });

    // Error row appears
    expect(screen.getByText('Invalid or missing email')).toBeInTheDocument();

    // Commit button is disabled when errors exist
    const commitBtn = screen.getByRole('button', { name: /fix errors/i });
    expect(commitBtn).toBeDisabled();
  });

  it('commit button enabled when no errors (warnings ok)', async () => {
    server.use(
      http.post('/api/events/evt-1/roster/validate', () =>
        HttpResponse.json({
          fatalError: null,
          validCount: 3,
          errorCount: 0,
          warningCount: 1,
          errors: [
            { row: 2, field: 'callsign', message: 'Callsign is missing', severity: 'Warning' },
          ],
        })
      )
    );
    renderPage();

    const file = new File(['name,email\ntest,test@test.com'], 'roster.csv', { type: 'text/csv' });
    const input = document.querySelector('input[type="file"]')!;
    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => expect(screen.getByText(/0 errors/)).toBeInTheDocument());
    const commitBtn = screen.getByRole('button', { name: /import 3 players/i });
    expect(commitBtn).not.toBeDisabled();
  });

  it('shows fatal error as full-width message', async () => {
    server.use(
      http.post('/api/events/evt-1/roster/validate', () =>
        HttpResponse.json({
          fatalError: 'CSV file is malformed',
          validCount: 0,
          errorCount: 0,
          warningCount: 0,
          errors: [],
        })
      )
    );
    renderPage();

    const file = new File(['not valid csv'], 'bad.csv', { type: 'text/csv' });
    const input = document.querySelector('input[type="file"]')!;
    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() =>
      expect(screen.getByText('CSV file is malformed')).toBeInTheDocument()
    );
  });
});
