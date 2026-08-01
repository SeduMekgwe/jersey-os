import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { useAuth } from '@/auth/auth-context';
import { Button, Card } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type { PublishRunDto, SalesChannelDto } from '@/types/api';

export function PublishingPage() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const canManage = user?.permissions.includes('publishing.manage') ?? false;

  const channelsQuery = useQuery({
    queryKey: ['publishing', 'channels'],
    queryFn: () => apiRequest<SalesChannelDto[]>('/publishing/channels'),
  });
  const runsQuery = useQuery({
    queryKey: ['publishing', 'runs'],
    queryFn: () => apiRequest<PublishRunDto[]>('/publishing/runs'),
  });

  const toggleMutation = useMutation({
    mutationFn: ({ channelId, enabled }: { channelId: string; enabled: boolean }) =>
      apiRequest<SalesChannelDto>(`/publishing/channels/${channelId}`, {
        method: 'PUT',
        body: JSON.stringify({ enabled }),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['publishing', 'channels'] });
    },
  });

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">Publishing</h1>
        <p className="mt-2 text-muted-foreground">
          Sales channel sync status for Active catalog products.
        </p>
      </div>

      <section className="space-y-3">
        <h2 className="text-lg font-medium">Channels</h2>
        {(channelsQuery.data ?? []).map((channel) => (
          <Card key={channel.id} className="flex flex-wrap items-center justify-between gap-3 p-4">
            <div>
              <p className="font-medium">{channel.displayName}</p>
              <p className="text-sm text-muted-foreground">
                {channel.code} · {channel.enabled ? 'Enabled' : 'Disabled'}
              </p>
            </div>
            {canManage && (
              <Button
                variant="outline"
                disabled={toggleMutation.isPending}
                onClick={() => {
                  toggleMutation.mutate({ channelId: channel.id, enabled: !channel.enabled });
                }}
              >
                {channel.enabled ? 'Disable' : 'Enable'}
              </Button>
            )}
          </Card>
        ))}
        {channelsQuery.isSuccess && channelsQuery.data.length === 0 && (
          <p className="text-sm text-muted-foreground">No sales channels configured.</p>
        )}
      </section>

      <section className="space-y-3">
        <h2 className="text-lg font-medium">Recent publish runs</h2>
        {(runsQuery.data ?? []).map((run) => (
          <Card key={run.id} className="space-y-1 p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <Link to={`/products/${run.productId}`} className="font-medium hover:underline">
                Product {run.productId.slice(0, 8)}…
              </Link>
              <span className="text-sm text-muted-foreground">{run.status}</span>
            </div>
            <p className="text-sm text-muted-foreground">
              {run.channelCode}
              {run.completedAtUtc
                ? ` · completed ${new Date(run.completedAtUtc).toLocaleString()}`
                : ''}
            </p>
            {run.error && <p className="text-sm text-destructive">{run.error}</p>}
          </Card>
        ))}
        {runsQuery.isSuccess && runsQuery.data.length === 0 && (
          <p className="text-sm text-muted-foreground">No publish runs yet.</p>
        )}
      </section>
    </div>
  );
}
