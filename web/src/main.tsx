import React from 'react';
import ReactDOM from 'react-dom/client';
import { createBrowserRouter, RouterProvider, Navigate } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Toaster } from 'sonner';
import { ProtectedRoute } from './components/ProtectedRoute';
import { AppLayout } from './components/AppLayout';
import { LoginPage } from './pages/auth/LoginPage';
import { MagicLinkRequestPage } from './pages/auth/MagicLinkRequestPage';
import { MagicLinkConfirmPage } from './pages/auth/MagicLinkConfirmPage';
import { PasswordResetRequestPage } from './pages/auth/PasswordResetRequestPage';
import { PasswordResetConfirmPage } from './pages/auth/PasswordResetConfirmPage';
import { RegisterPage } from './pages/auth/RegisterPage';
import { DashboardPage } from './pages/DashboardPage';
import { ProfilePage } from './pages/ProfilePage';
import { EventList } from './pages/events/EventList';
import { EventDetail } from './pages/events/EventDetail';
import { PlayerEventPage } from './pages/events/PlayerEventPage';
import { ChangeRequestsPage } from './pages/events/ChangeRequestsPage';
import { BriefingPage } from './pages/events/BriefingPage';
import { MapResourcesPage } from './pages/events/MapResourcesPage';
import { NotificationBlastPage } from './pages/events/NotificationBlastPage';
import { CsvImportPage } from './pages/roster/CsvImportPage';
import { HierarchyBuilder } from './pages/roster/HierarchyBuilder';
import { RosterView } from './pages/roster/RosterView';
import './index.css';

const queryClient = new QueryClient();

const router = createBrowserRouter([
  // ── Public auth routes ────────────────────────────────────────────────────
  { path: '/auth/login', element: <LoginPage /> },
  { path: '/auth/magic-link', element: <MagicLinkRequestPage /> },
  { path: '/auth/magic-link/confirm', element: <MagicLinkConfirmPage /> },
  { path: '/auth/reset-password', element: <PasswordResetConfirmPage /> },
  { path: '/auth/forgot-password', element: <PasswordResetRequestPage /> },
  { path: '/auth/register', element: <RegisterPage /> },

  // ── Authenticated routes (all wrapped in AppLayout for global header) ─────
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppLayout />,
        children: [
          // Redirect root to dashboard
          { index: true, path: '/', element: <Navigate to="/dashboard" replace /> },
          { path: '/dashboard', element: <DashboardPage /> },
          { path: '/profile', element: <ProfilePage /> },
          { path: '/events', element: <EventList /> },
          { path: '/events/:id', element: <EventDetail /> },

          // Player-facing event view (all authenticated roles can access)
          { path: '/events/:id/player', element: <PlayerEventPage /> },

          // Event content pages (all authenticated users)
          { path: '/events/:id/briefing', element: <BriefingPage /> },
          { path: '/events/:id/maps', element: <MapResourcesPage /> },
          { path: '/events/:id/roster', element: <RosterView /> },
          { path: '/events/:id/notifications', element: <NotificationBlastPage /> },

          // Commander-only routes — redirect non-commanders to /dashboard
          {
            element: <ProtectedRoute requiredRole="faction_commander" />,
            children: [
              { path: '/events/:id/change-requests', element: <ChangeRequestsPage /> },
              { path: '/events/:id/hierarchy', element: <HierarchyBuilder /> },
              { path: '/events/:id/roster/import', element: <CsvImportPage /> },
            ],
          },
        ],
      },
    ],
  },
]);

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
      <Toaster />
    </QueryClientProvider>
  </React.StrictMode>
);
