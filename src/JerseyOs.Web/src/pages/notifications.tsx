import { useQuery } from '@tanstack/react-query';
import { Card } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type { NotificationMessageDto } from '@/types/api';

export function NotificationsPage() {
  const query = useQuery({
    queryKey: ['notifications', 'messages'],
    queryFn: () => apiRequest<NotificationMessageDto[]>('/notifications/messages'),
    refetchInterval: (q) =>
      q.state.data?.some((item) => item.status === 'Pending') ? 2_000 : 15_000,
  });

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">Notifications</h1>
        <p className="mt-2 text-muted-foreground">
          Delivery attempts for import ready, publish failed, and scrape failed. Default channel is
          Fixture.
        </p>
      </div>
      {query.isError && (
        <Card className="border-destructive/50 p-4" role="alert">
          {query.error instanceof Error ? query.error.message : 'Notifications unavailable.'}
        </Card>
      )}
      <div className="space-y-3">
        {(query.data ?? []).map((message) => (
          <Card key={message.id} className="space-y-2 p-5">
            <p className="font-medium">
              {message.kind} · {message.status}
            </p>
            <p className="text-sm">{message.subject}</p>
            <p className="text-sm text-muted-foreground">{message.body}</p>
            {(message.deliveries ?? []).map((delivery) => (
              <p key={delivery.id} className="text-xs text-muted-foreground">
                {delivery.channel} → {delivery.destination} · {delivery.status}
                {delivery.error ? ` (${delivery.error})` : ''}
              </p>
            ))}
          </Card>
        ))}
        {query.isSuccess && query.data.length === 0 && (
          <p className="text-sm text-muted-foreground">No notification messages yet.</p>
        )}
      </div>
    </div>
  );
}
