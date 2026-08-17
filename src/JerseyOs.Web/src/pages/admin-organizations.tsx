import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Button, Card, Input } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type { OrganizationQuotaDto, OrganizationSummaryDto } from '@/types/api';

export function AdminOrganizationsPage() {
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [adminEmail, setAdminEmail] = useState('');
  const [adminPassword, setAdminPassword] = useState('');
  const [currency, setCurrency] = useState('ZAR');
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<OrganizationQuotaDto | null>(null);

  const orgsQuery = useQuery({
    queryKey: ['admin', 'organizations'],
    queryFn: () => apiRequest<OrganizationSummaryDto[]>('/admin/organizations'),
  });

  const provision = useMutation({
    mutationFn: () =>
      apiRequest<OrganizationSummaryDto>('/admin/organizations', {
        method: 'POST',
        body: JSON.stringify({
          name,
          slug,
          adminEmail,
          adminPassword,
          defaultCurrency: currency,
        }),
      }),
    onSuccess: () => {
      setError(null);
      setName('');
      setSlug('');
      setAdminEmail('');
      setAdminPassword('');
      void queryClient.invalidateQueries({ queryKey: ['admin', 'organizations'] });
    },
    onError: (err: Error) => setError(err.message),
  });

  const loadQuota = async (organizationId: string) => {
    setError(null);
    try {
      setSelected(
        await apiRequest<OrganizationQuotaDto>(`/admin/organizations/${organizationId}/quotas`),
      );
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load quotas.');
    }
  };

  const saveQuota = useMutation({
    mutationFn: (quota: OrganizationQuotaDto) =>
      apiRequest<OrganizationQuotaDto>(`/admin/organizations/${quota.organizationId}/quotas`, {
        method: 'PUT',
        body: JSON.stringify({
          maxProducts: quota.maxProducts,
          maxMembers: quota.maxMembers,
          maxImportBatchesPerDay: quota.maxImportBatchesPerDay,
          maxAiGenerationsPerDay: quota.maxAiGenerationsPerDay,
        }),
      }),
    onSuccess: (quota) => {
      setSelected(quota);
      setError(null);
      void queryClient.invalidateQueries({ queryKey: ['admin', 'organizations'] });
    },
    onError: (err: Error) => setError(err.message),
  });

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">Organizations</h1>
        <p className="mt-2 text-muted-foreground">
          Platform admin provisioning and quota limits. No cross-tenant impersonation.
        </p>
      </div>
      {error && (
        <Card className="border-destructive/50 p-4" role="alert">
          {error}
        </Card>
      )}
      <Card className="space-y-3 p-4">
        <h2 className="font-medium">Provision organization</h2>
        <label className="block text-sm">
          Name
          <Input className="mt-1" value={name} onChange={(e) => setName(e.target.value)} />
        </label>
        <label className="block text-sm">
          Slug
          <Input className="mt-1" value={slug} onChange={(e) => setSlug(e.target.value)} />
        </label>
        <label className="block text-sm">
          Admin email
          <Input
            className="mt-1"
            type="email"
            value={adminEmail}
            onChange={(e) => setAdminEmail(e.target.value)}
          />
        </label>
        <label className="block text-sm">
          Admin password
          <Input
            className="mt-1"
            type="password"
            value={adminPassword}
            onChange={(e) => setAdminPassword(e.target.value)}
          />
        </label>
        <label className="block text-sm">
          Currency
          <Input className="mt-1" value={currency} onChange={(e) => setCurrency(e.target.value)} />
        </label>
        <Button
          disabled={provision.isPending || !name || !slug || !adminEmail || !adminPassword}
          onClick={() => provision.mutate()}
        >
          Provision
        </Button>
      </Card>
      <div className="space-y-3">
        {(orgsQuery.data ?? []).map((org) => (
          <Card key={org.id} className="flex flex-wrap items-center justify-between gap-3 p-4">
            <div>
              <p className="font-medium">
                {org.name} · {org.slug}
              </p>
              <p className="text-sm text-muted-foreground">
                {org.productCount} products · {org.memberCount} members · {org.defaultCurrency}
              </p>
            </div>
            <Button variant="outline" onClick={() => void loadQuota(org.id)}>
              Quotas
            </Button>
          </Card>
        ))}
      </div>
      {selected && (
        <Card className="space-y-3 p-4">
          <h2 className="font-medium">Quota limits</h2>
          <p className="text-sm text-muted-foreground">
            Used: {selected.productCount}/{selected.maxProducts} products ·{' '}
            {selected.memberCount}/{selected.maxMembers} members ·{' '}
            {selected.importBatchesToday}/{selected.maxImportBatchesPerDay} imports today ·{' '}
            {selected.aiGenerationsToday}/{selected.maxAiGenerationsPerDay} AI today
          </p>
          {(['maxProducts', 'maxMembers', 'maxImportBatchesPerDay', 'maxAiGenerationsPerDay'] as const).map(
            (field) => (
              <label key={field} className="block text-sm">
                {field}
                <Input
                  className="mt-1"
                  type="number"
                  min={1}
                  value={selected[field]}
                  onChange={(e) =>
                    setSelected({ ...selected, [field]: Number(e.target.value) })
                  }
                />
              </label>
            ),
          )}
          <Button disabled={saveQuota.isPending} onClick={() => saveQuota.mutate(selected)}>
            Save quotas
          </Button>
        </Card>
      )}
    </div>
  );
}
