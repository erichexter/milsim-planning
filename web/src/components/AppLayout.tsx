import { Outlet } from 'react-router';
import { AppHeader } from './AppHeader';

export function AppLayout() {
  return (
    <div className="flex flex-col min-h-dvh">
      <AppHeader />
      <main className="flex-1">
        <Outlet />
      </main>
    </div>
  );
}
