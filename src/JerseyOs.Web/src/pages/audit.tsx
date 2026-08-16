import { useQuery } from '@tanstack/react-query';
import { Card } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type { AuditEventDto } from '@/types/api';

export function AuditPage() {
  const query = useQuery({
    queryKey: ['audit', 'events'],
    queryFn: () => apiRequest<AuditEventDto[]>('/audit/events'),
    refetchInterval: 15_000,
  });

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">Audit</h1>
        <p className="mt-2 text-muted-foreground">
          Append-only security and business events. Approve, publish, config, and authorization denials.
        </p>
      </div>
      {query.isError && (
        <Card className="border-destructive/50 p-4" role="alert">
          {query.error instanceof Error ? query.error.message : 'Audit events unavailable.'}
        </Card>
      )}
      <Card className="overflow-x-auto p-0">
        <table className="w-full text-left text-sm">
          <thead className="border-b bg-muted/40">
            <tr>
              <th className="px-4 py-3">When</th>
              <th className="px-4 py-3">Action</th>
              <th className="px-4 py-3">Entity</th>
              <th className="px-4 py-3">Actor</th>
            </tr>
          </thead>
          <tbody>
            {(query.data ?? []).map((event) => (
              <tr key={event.id} className="border-b last:border-0">
                <td className="px-4 py-3 text-muted-foreground">
                  {new Date(event.createdAtUtc).toLocaleString()}
                </td>
                <td className="px-4 py-3 font-medium">{event.action}</td>
                <td className="px-4 py-3">
                  {event.entityType} · {event.entityId}
                </td>
                <td className="px-4 py-3">{event.actor}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {query.isSuccess && query.data.length === 0 && (
          <p className="p-4 text-sm text-muted-foreground">No audit events yet.</p>
        )}
      </Card>
    </div>
  );
}
