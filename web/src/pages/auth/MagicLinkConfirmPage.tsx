import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { api } from '../../lib/api';
import { useAuth } from '../../hooks/useAuth';
import { Button } from '../../components/ui/button';
import { Card, CardHeader, CardTitle, CardContent } from '../../components/ui/card';

export function MagicLinkConfirmPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { login } = useAuth();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const token = searchParams.get('token') ?? '';
  const userId = searchParams.get('userId') ?? '';

  // NOTE: No useEffect here — email scanner protection requires explicit button click
  // to prevent email scanners from consuming the magic link token automatically.

  const handleConfirm = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.post<{ token: string; expiresIn: number }>(
        '/auth/magic-link/confirm',
        { token, userId }
      );
      login(result.token);
      navigate('/dashboard');
    } catch {
      setError('This link has expired or already been used.');
      setIsLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Complete Sign-In</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {error ? (
            <>
              <p className="text-sm text-red-500">{error}</p>
              <Link to="/auth/magic-link" className="text-sm text-primary hover:underline">
                Request a new magic link
              </Link>
            </>
          ) : (
            <>
              <p className="text-sm text-muted-foreground">
                Click the button below to complete your sign-in.
              </p>
              <Button
                onClick={handleConfirm}
                disabled={isLoading}
                className="w-full"
              >
                {isLoading ? 'Signing in…' : 'Complete Sign-In'}
              </Button>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
