import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Link, useNavigate } from 'react-router';
import { toast } from 'sonner';
import { api } from '../../lib/api';
import type { RegisterResponse } from '../../lib/api';
import { useAuth } from '../../hooks/useAuth';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '../../components/ui/card';

// Per D-10: password confirm is client-side only via zod .refine() -- no API call on mismatch (AC-10)
const schema = z.object({
  displayName: z.string().min(1, 'Display name is required'),
  email: z.string().email('Invalid email address'),
  password: z.string().min(6, 'Password must be at least 6 characters'),
  confirmPassword: z.string().min(1, 'Please confirm your password'),
}).refine((data) => data.password === data.confirmPassword, {
  message: 'Passwords do not match',
  path: ['confirmPassword'],
});

type RegisterForm = z.infer<typeof schema>;

export function RegisterPage() {
  const navigate = useNavigate();
  const { login, isAuthenticated } = useAuth();

  // Per D-14 / AC-9: auth guard -- redirect authenticated users to /dashboard
  useEffect(() => {
    if (isAuthenticated) navigate('/dashboard', { replace: true });
  }, [isAuthenticated, navigate]);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterForm>({ resolver: zodResolver(schema) });

  const onSubmit = async (data: RegisterForm) => {
    try {
      // Per D-10: strip confirmPassword -- do NOT send to API
      const result = await api.post<RegisterResponse>('/auth/register', {
        displayName: data.displayName,
        email: data.email,
        password: data.password,
      });
      // Per D-11: call login(token) from useAuth, then navigate to /dashboard
      login(result.token);
      navigate('/dashboard');
    } catch (err) {
      // Per D-12: on 409, show "An account with this email already exists" inline on email field
      if ((err as Error & { status?: number }).status === 409) {
        setError('email', { message: 'An account with this email already exists' });
      } else {
        // Per D-13: on 400/other, show error from API via toast
        toast.error((err as Error).message ?? 'Registration failed');
      }
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Create Account</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div className="space-y-1">
              <Label htmlFor="displayName">Display Name</Label>
              <Input
                id="displayName"
                type="text"
                autoComplete="name"
                {...register('displayName')}
              />
              {errors.displayName && (
                <p className="text-sm text-red-500">{errors.displayName.message}</p>
              )}
            </div>
            <div className="space-y-1">
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                autoComplete="email"
                {...register('email')}
              />
              {errors.email && (
                <p className="text-sm text-red-500">{errors.email.message}</p>
              )}
            </div>
            <div className="space-y-1">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                autoComplete="new-password"
                {...register('password')}
              />
              {errors.password && (
                <p className="text-sm text-red-500">{errors.password.message}</p>
              )}
            </div>
            <div className="space-y-1">
              <Label htmlFor="confirmPassword">Confirm Password</Label>
              <Input
                id="confirmPassword"
                type="password"
                autoComplete="new-password"
                {...register('confirmPassword')}
              />
              {errors.confirmPassword && (
                <p className="text-sm text-red-500">{errors.confirmPassword.message}</p>
              )}
            </div>
            <Button type="submit" className="w-full" disabled={isSubmitting}>
              {isSubmitting ? 'Creating account...' : 'Create Account'}
            </Button>
          </form>
        </CardContent>
        <CardFooter className="flex flex-col gap-2 text-sm">
          <Link to="/auth/login" className="text-primary hover:underline">
            Already have an account? Sign in
          </Link>
        </CardFooter>
      </Card>
    </div>
  );
}
