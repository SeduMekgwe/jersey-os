import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Button, Card, Input } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type { ApiKeyDto, CreatedApiKeyDto } from '@/types/api';

export function IntegrationsPage() {
  const queryClient = useQueryClient();
  const [name, setName] = useState('Partner feed');
  const [scopes, setScopes] = useState('import.upload,catalog.read');
  const [plaintext, setPlaintext] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const keysQuery = useQuery({
    queryKey: ['integrations', 'api-keys'],
    queryFn: () => apiRequest<ApiKeyDto[]>('/integrations/api-keys'),
  });

  const create = useMutation({
    mutationFn: () =>
      apiRequest<CreatedApiKeyDto>('/integrations/api-keys', {
        method: 'POST',
        body: JSON.stringify({
          name,
          scopes: scopes
            .split(',')
            .map((s) => s.trim())
            .filter(Boolean),
        }),
      }),
    onSuccess: (created) => {
      setPlaintext(created.plaintext);
      setError(null);
      void queryClient.invalidateQueries({ queryKey: ['integrations', 'api-keys'] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });

  const revoke = useMutation({
    mutationFn: (id: string) =>
      apiRequest<ApiKeyDto>(`/integrations/api-keys/${id}/revoke`, { method: 'POST' }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['integrations', 'api-keys'] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">API keys</h1>
        <p className="mt-2 text-muted-foreground">
          Machine credentials for supplier and partner HTTP calls. Plaintext is shown once.
        </p>
      </div>
      {error && (
        <Card className="border-destructive/50 p-4" role="alert">
          {error}
        </Card>
      )}
      {plaintext && (
        <Card className="space-y-2 p-4" role="status">
          <p className="font-medium">Copy this key now. It will not be shown again.</p>
          <code className="block break-all text-sm">{plaintext}</code>
        </Card>
      )}
      <Card className="space-y-3 p-4">
        <label className="block text-sm">
          Name
          <Input className="mt-1" value={name} onChange={(e) => setName(e.target.value)} />
        </label>
        <label className="block text-sm">
          Scopes (comma-separated permissions)
          <Input className="mt-1" value={scopes} onChange={(e) => setScopes(e.target.value)} />
        </label>
        <Button disabled={create.isPending} onClick={() => create.mutate()}>
          Create key
        </Button>
      </Card>
      <div className="space-y-3">
        {(keysQuery.data ?? []).map((key) => (
          <Card key={key.id} className="flex flex-wrap items-center justify-between gap-3 p-4">
            <div>
              <p className="font-medium">
                {key.name} · {key.prefix}
              </p>
              <p className="text-sm text-muted-foreground">
                {(key.scopes ?? []).join(', ')}
                {key.revokedAtUtc ? ' · revoked' : ''}
              </p>
            </div>
            {!key.revokedAtUtc && (
              <Button
                variant="outline"
                disabled={revoke.isPending}
                onClick={() => revoke.mutate(key.id)}
              >
                Revoke
              </Button>
            )}
          </Card>
        ))}
        {keysQuery.isSuccess && keysQuery.data.length === 0 && (
          <p className="text-sm text-muted-foreground">No API keys yet.</p>
        )}
      </div>
    </div>
  );
}
