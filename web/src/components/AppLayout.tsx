import { useEffect, useState } from 'react';
import { Outlet } from 'react-router';
import { useIsFetching } from '@tanstack/react-query';
import { AppHeader } from './AppHeader';

export function AppLayout() {
  const inFlightQueries = useIsFetching();
  const [showWarmupBanner, setShowWarmupBanner] = useState(false);

  useEffect(() => {
    if (inFlightQueries > 0) {
      const timer = window.setTimeout(() => setShowWarmupBanner(true), 700);
      return () => window.clearTimeout(timer);
    }

    setShowWarmupBanner(false);
    return undefined;
  }, [inFlightQueries]);

  return (
    <div className="flex flex-col min-h-dvh">
      <AppHeader />
      {showWarmupBanner && (
        <div className="border-b bg-amber-soft/60 border-amber-border/60 px-4 py-2">
          <p className="mx-auto max-w-5xl text-xs font-medium text-muted-foreground">
            Waking up services... this can take up to 20 seconds after inactivity.
          </p>
        </div>
      )}
      <main className="flex-1">
        <Outlet />
      </main>
    </div>
  );
}
