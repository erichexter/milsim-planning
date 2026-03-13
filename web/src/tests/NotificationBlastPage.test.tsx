import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { NotificationBlastPage } from '../pages/events/NotificationBlastPage';

function setRoleToken(role: string) {
  const payload = {
    sub: 'user-1',
    email: 'tester@example.com',
    role,
    exp: Math.floor(Date.now() / 1000) + 3600,
  };
  localStorage.setItem('milsim_token', `x.${btoa(JSON.stringify(payload))}.x`);
}

function renderPage() {
  const queryClient = new QueryClient();
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/events/evt-1/notifications']}>
        <Routes>
          <Route path="/events/:id/notifications" element={<NotificationBlastPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('NotificationBlastPage', () => {
  it('renders subject and body inputs', async () => {
    setRoleToken('Commander');
    server.use(
      http.get('/api/events/evt-1/notification-blasts', () => HttpResponse.json([]))
    );

    renderPage();
    expect(await screen.findByPlaceholderText('Subject')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Message body')).toBeInTheDocument();
  });

  it('keeps Send button disabled when subject is empty', async () => {
    setRoleToken('Commander');
    server.use(
      http.get('/api/events/evt-1/notification-blasts', () => HttpResponse.json([]))
    );

    renderPage();
    const sendButton = await screen.findByRole('button', { name: 'Send' });
    expect(sendButton).toBeDisabled();
  });

  it('renders blast history table with data', async () => {
    setRoleToken('Commander');
    server.use(
      http.get('/api/events/evt-1/notification-blasts', () =>
        HttpResponse.json([
          {
            id: 'blast-1',
            subject: 'Operation update',
            sentAt: '2026-03-13T10:00:00Z',
            recipientCount: 42,
          },
        ])
      )
    );

    renderPage();
    expect(await screen.findByText('Operation update')).toBeInTheDocument();
    expect(screen.getByText('42')).toBeInTheDocument();
  });
});
