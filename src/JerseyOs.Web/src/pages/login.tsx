import { zodResolver } from '@hookform/resolvers/zod';
import { ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { useAuth } from '@/auth/auth-context';
import { Button, Card, Input } from '@/components/ui';
import { ApiError } from '@/lib/api';

const schema = z.object({
  email: z.email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
});
type FormData = z.infer<typeof schema>;
export function LoginPage() {
  const { user, isRestoring, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<string>();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  });
  if (isRestoring)
    return (
      <main className="grid min-h-screen place-items-center" aria-live="polite">
        Restoring your session…
      </main>
    );
  if (user) return <Navigate to="/" replace />;
  const submit = async (data: FormData) => {
    setError(undefined);
    try {
      await login(data);
      const state = location.state as { from?: string } | null;
      void navigate(state?.from ?? '/', { replace: true });
    } catch (reason) {
      setError(
        reason instanceof ApiError ? reason.message : 'Unable to sign in. Please try again.',
      );
    }
  };
  return (
    <main className="grid min-h-screen place-items-center bg-background p-4">
      <Card className="w-full max-w-md p-6 sm:p-8">
        <div className="mb-8 flex items-center gap-3">
          <span className="rounded-lg bg-primary p-2 text-primary-foreground">
            <ShieldCheck />
          </span>
          <div>
            <h1 className="text-2xl font-semibold">Sign in to Jersey OS</h1>
            <p className="text-sm text-muted-foreground">Use your organization account.</p>
          </div>
        </div>
        <form
          className="space-y-5"
          onSubmit={(event) => void handleSubmit(submit)(event)}
          noValidate
        >
          {error && (
            <div
              role="alert"
              className="rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm"
            >
              {error}
            </div>
          )}
          <div>
            <label htmlFor="email" className="mb-2 block text-sm font-medium">
              Email
            </label>
            <Input
              id="email"
              type="email"
              autoComplete="username"
              aria-invalid={Boolean(errors.email)}
              aria-describedby={errors.email ? 'email-error' : undefined}
              {...register('email')}
            />
            {errors.email && (
              <p id="email-error" className="mt-1 text-sm text-destructive">
                {errors.email.message}
              </p>
            )}
          </div>
          <div>
            <label htmlFor="password" className="mb-2 block text-sm font-medium">
              Password
            </label>
            <Input
              id="password"
              type="password"
              autoComplete="current-password"
              aria-invalid={Boolean(errors.password)}
              aria-describedby={errors.password ? 'password-error' : undefined}
              {...register('password')}
            />
            {errors.password && (
              <p id="password-error" className="mt-1 text-sm text-destructive">
                {errors.password.message}
              </p>
            )}
          </div>
          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? 'Signing in…' : 'Sign in'}
          </Button>
        </form>
      </Card>
    </main>
  );
}
