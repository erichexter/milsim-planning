import { Link } from 'react-router';
import { Sun, Moon } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';
import { useTheme } from '../hooks/useTheme';

function getInitials(callsign: string | null | undefined, email: string | null | undefined) {
  const name = callsign ?? email ?? '?';
  return name.slice(0, 2).toUpperCase();
}

export function AppHeader() {
  const { user, logout } = useAuth();
  const { isDark, setTheme } = useTheme();

  return (
    <header className="sticky top-0 z-50 h-12 border-b bg-card flex items-center px-4 gap-3">
      {/* Left: logo pill + app name + live dot */}
      <Link
        to="/dashboard"
        className="flex items-center gap-2 min-w-0"
        aria-label="Rally Point Zero — Dashboard"
      >
        {/* RP0 logo pill */}
        <span
          className="font-mono text-[10px] font-medium tracking-widest px-2 py-0.5 rounded-full"
          style={{
            backgroundColor: 'oklch(var(--primary-soft))',
            border: '1px solid oklch(var(--primary-border))',
            color: 'oklch(var(--primary))',
          }}
        >
          RP0
        </span>

        {/* App name */}
        <span className="font-medium text-sm tracking-tight hidden sm:block">
          Rally Point Zero
        </span>

        {/* Pulsing live dot */}
        <span
          className="rp0-dot-pulse h-1.5 w-1.5 rounded-full shrink-0"
          style={{ backgroundColor: 'oklch(var(--primary))' }}
          aria-hidden
        />
      </Link>

      {/* Spacer */}
      <div className="flex-1" />

      {/* Right: avatar chip */}
      {user && (
        <Link
          to="/profile"
          className="flex items-center gap-2 rounded-full px-2 py-1 hover:bg-muted transition-colors"
          aria-label="Profile"
        >
          {/* Avatar circle with initials */}
          <span
            className="h-7 w-7 rounded-full flex items-center justify-center text-[10px] font-mono font-medium shrink-0"
            style={{
              backgroundColor: 'oklch(var(--primary-soft))',
              border: '1px solid oklch(var(--primary-border))',
              color: 'oklch(var(--primary))',
            }}
          >
            {getInitials(user.callsign, user.email)}
          </span>

          {/* Callsign or email */}
          <span className="hidden sm:block font-mono text-xs font-medium truncate max-w-[120px]">
            {user.callsign ?? user.email}
          </span>
        </Link>
      )}

      {/* Theme toggle */}
      <button
        onClick={() => setTheme(isDark ? 'light' : 'dark')}
        className="h-8 w-8 flex items-center justify-center rounded-[8px] text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
        aria-label={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
      >
        {isDark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
      </button>

      {/* Logout — subtle text button */}
      <button
        onClick={() => void logout()}
        className="text-xs text-muted-foreground hover:text-foreground transition-colors px-2 py-1 rounded-[8px] hover:bg-muted"
      >
        Sign out
      </button>
    </header>
  );
}
