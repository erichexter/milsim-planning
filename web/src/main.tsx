import React from 'react';
import ReactDOM from 'react-dom/client';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Toaster } from 'sonner';
import { ProtectedRoute } from './components/ProtectedRoute';
import { LoginPage } from './pages/auth/LoginPage';
import { MagicLinkRequestPage } from './pages/auth/MagicLinkRequestPage';
import { MagicLinkConfirmPage } from './pages/auth/MagicLinkConfirmPage';
import { PasswordResetRequestPage } from './pages/auth/PasswordResetRequestPage';
import { PasswordResetConfirmPage } from './pages/auth/PasswordResetConfirmPage';
import { DashboardPage } from './pages/DashboardPage';
import { EventList } from './pages/events/EventList';
import { EventDetail } from './pages/events/EventDetail';
import { CsvImportPage } from './pages/roster/CsvImportPage';
import { HierarchyBuilder } from './pages/roster/HierarchyBuilder';
import { RosterView } from './pages/roster/RosterView';
import './index.css';

const queryClient = new QueryClient();

const router = createBrowserRouter([
  { path: '/auth/login', element: <LoginPage /> },
  { path: '/auth/magic-link', element: <MagicLinkRequestPage /> },
  { path: '/auth/magic-link/confirm', element: <MagicLinkConfirmPage /> },
  { path: '/auth/reset-password', element: <PasswordResetConfirmPage /> },
  { path: '/auth/forgot-password', element: <PasswordResetRequestPage /> },
  {
    element: <ProtectedRoute />,
    children: [
      { index: true, path: '/', element: <DashboardPage /> },
      { path: '/dashboard', element: <DashboardPage /> },
      { path: '/events', element: <EventList /> },
      { path: '/events/:id', element: <EventDetail /> },
      { path: '/events/:id/roster/import', element: <CsvImportPage /> },
      { path: '/events/:id/hierarchy', element: <HierarchyBuilder /> },
      { path: '/events/:id/roster', element: <RosterView /> },
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
