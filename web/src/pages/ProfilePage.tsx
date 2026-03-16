import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { ChevronRight } from 'lucide-react';
import { api } from '../lib/api';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Badge } from '../components/ui/badge';

export function ProfilePage() {
  const { data: profile, isLoading } = useQuery({
    queryKey: ['profile'],
    queryFn: () => api.getProfile(),
  });

  return (
    <div className="mx-auto max-w-2xl space-y-6 p-6">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-1.5 text-sm text-muted-foreground">
        <Link to="/dashboard" className="hover:text-foreground transition-colors">
          Dashboard
        </Link>
        <ChevronRight className="h-3.5 w-3.5 shrink-0" />
        <span className="text-foreground font-medium">Profile</span>
      </nav>

      <h1 className="text-2xl font-bold">Profile</h1>

      {isLoading && (
        <div className="space-y-3">
          <div className="h-20 rounded-lg bg-muted animate-pulse" />
        </div>
      )}

      {profile && (
        <Card>
          <CardHeader className="pb-2">
            <div className="flex items-center gap-2">
              <CardTitle className="text-base">
                {profile.displayName || profile.callsign || profile.email}
              </CardTitle>
              <Badge variant="secondary">{profile.role}</Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="grid grid-cols-[120px_1fr] gap-y-2">
              <span className="text-muted-foreground">Email</span>
              <span>{profile.email}</span>

              <span className="text-muted-foreground">Callsign</span>
              <span className="font-mono font-medium text-orange-500">
                {profile.callsign ? `[${profile.callsign}]` : '—'}
              </span>

              <span className="text-muted-foreground">Display name</span>
              <span>{profile.displayName || '—'}</span>

              <span className="text-muted-foreground">Role</span>
              <span>{profile.role}</span>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
