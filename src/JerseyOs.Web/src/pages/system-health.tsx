import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, CircleX } from 'lucide-react';
import { Card } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type { SystemHealthDto } from '@/types/api';

const statusStyle = {
  Healthy: 'text-emerald-400',
  Degraded: 'text-amber-400',
  Unhealthy: 'text-destructive',
} as const;
const statusIcon = { Healthy: CheckCircle2, Degraded: AlertTriangle, Unhealthy: CircleX } as const;
export function SystemHealthPage() {
  const query = useQuery({
    queryKey: ['system-health'],
    queryFn: () => apiRequest<SystemHealthDto>('/system/health'),
    refetchInterval: 30_000,
  });
  return (
    <div className="mx-auto max-w-5xl">
      <h1 className="text-3xl font-semibold tracking-tight">System health</h1>
      <p className="mt-2 text-muted-foreground">
        Live readiness reported by platform services. Refreshes every 30 seconds.
      </p>
      {query.isPending && (
        <p className="mt-8" aria-live="polite">
          Loading service health…
        </p>
      )}
      {query.isError && (
        <Card className="mt-8 border-destructive/50 p-5" role="alert">
          <h2 className="font-semibold">Health data unavailable</h2>
          <p className="mt-2 text-sm text-muted-foreground">
            {query.error instanceof Error
              ? query.error.message
              : 'The health endpoint could not be reached.'}
          </p>
        </Card>
      )}
      {query.data && (
        <>
          <div className="mt-8 flex items-center gap-3">
            <span className={statusStyle[query.data.status]}>{query.data.status}</span>
            <span className="text-sm text-muted-foreground">
              Checked {new Date(query.data.checkedAt).toLocaleString()}
            </span>
          </div>
          <div className="mt-4 grid gap-3">
            {query.data.checks.map((check) => {
              const Icon = statusIcon[check.status];
              return (
                <Card key={check.name} className="flex items-start gap-4 p-5">
                  <Icon className={statusStyle[check.status]} aria-hidden />
                  <div>
                    <h2 className="font-medium">{check.name}</h2>
                    <p className="text-sm text-muted-foreground">
                      {check.description ?? check.status}
                    </p>
                  </div>
                </Card>
              );
            })}
          </div>
        </>
      )}
    </div>
  );
}
