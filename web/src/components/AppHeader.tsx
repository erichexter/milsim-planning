import { Link } from 'react-router';
import { useAuth } from '../hooks/useAuth';
import { Badge } from './ui/badge';
import { Button } from './ui/button';
import { User } from 'lucide-react';

export function AppHeader() {
  const { user, logout } = useAuth();

  return (
    <header className="sticky top-0 z-50 border-b bg-background">
      <div className="mx-auto flex h-14 max-w-screen-xl items-center justify-between px-4">
        {/* Left: app name */}
        <Link
          to="/dashboard"
          className="text-base font-semibold tracking-tight hover:text-primary transition-colors"
        >
          Milsim Planning
        </Link>

        {/* Right: user info + actions */}
        <div className="flex items-center gap-3">
          {user && (
            <div className="hidden sm:flex items-center gap-2">
              <span className="text-sm font-medium">
                {user.callsign || user.email}
              </span>
              <Badge variant="secondary" className="text-xs">
                {user.role}
              </Badge>
            </div>
          )}

          <Link to="/profile">
            <Button variant="ghost" size="sm" className="gap-1.5">
              <User className="h-4 w-4" />
              <span className="hidden sm:inline">Profile</span>
            </Button>
          </Link>

          <Button variant="outline" size="sm" onClick={() => void logout()}>
            Logout
          </Button>
        </div>
      </div>
    </header>
  );
}
