import { LockKeyhole, Server, ShieldCheck, type LucideIcon } from 'lucide-react';
import { Card } from '@/components/ui';

const capabilities: readonly (readonly [LucideIcon, string, string])[] = [
  [
    ShieldCheck,
    'Authenticated session',
    'Access is protected by short-lived in-memory credentials.',
  ],
  [
    LockKeyhole,
    'Permission controls',
    'Routes enforce permissions supplied by the identity service.',
  ],
  [Server, 'Service readiness', 'Inspect live dependency status on System health.'],
];

export function DashboardPage() {
  return (
    <div className="mx-auto max-w-6xl">
      <header>
        <p className="text-sm font-medium text-primary">Jersey OS</p>
        <h1 className="mt-1 text-3xl font-semibold tracking-tight">Platform dashboard</h1>
        <p className="mt-2 max-w-2xl text-muted-foreground">
          The platform foundation is connected. Operational data appears only when provided by
          platform services.
        </p>
      </header>
      <section aria-labelledby="foundation-heading" className="mt-8">
        <h2 id="foundation-heading" className="text-lg font-semibold">
          Foundation capabilities
        </h2>
        <div className="mt-4 grid gap-4 md:grid-cols-3">
          {capabilities.map(([Icon, title, body]) => (
            <Card key={title} className="p-5">
              <Icon className="text-primary" />
              <h3 className="mt-4 font-semibold">{title}</h3>
              <p className="mt-2 text-sm text-muted-foreground">{body}</p>
            </Card>
          ))}
        </div>
      </section>
    </div>
  );
}
