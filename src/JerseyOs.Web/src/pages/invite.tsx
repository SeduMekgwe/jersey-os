import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { Button, Card, Input } from '@/components/ui';
import { ApiError, apiRequest, tokenStore } from '@/lib/api';
import type { SessionDto } from '@/types/api';

export function InvitePage() {
  const { token } = useParams<{ token: string }>();
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const accept = async () => {
    if (!token) return;
    setPending(true);
    setError(null);
    try {
      const session = await apiRequest<SessionDto>(
        '/auth/invitations/accept',
        {
          method: 'POST',
          body: JSON.stringify({ token, password: password || null }),
        },
        false,
      );
      tokenStore.set(session.accessToken);
      window.location.assign('/');
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Invitation could not be accepted.');
    } finally {
      setPending(false);
    }
  };

  return (
    <main className="grid min-h-screen place-items-center bg-background p-4">
      <Card className="w-full max-w-md space-y-4 p-6">
        <h1 className="text-2xl font-semibold">Accept invitation</h1>
        <p className="text-sm text-muted-foreground">
          New accounts need a password of at least 12 characters. Existing accounts can leave it blank.
        </p>
        {error && (
          <div role="alert" className="rounded-md border border-destructive/50 p-3 text-sm">
            {error}
          </div>
        )}
        <label className="block text-sm">
          Password
          <Input
            className="mt-1"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        <Button disabled={pending || !token} onClick={() => void accept()}>
          Join organization
        </Button>
      </Card>
    </main>
  );
}
