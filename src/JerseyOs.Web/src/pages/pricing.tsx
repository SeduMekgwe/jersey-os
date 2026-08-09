import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useAuth } from '@/auth/auth-context';
import { Button, Card, Input } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type {
  CreatePricingRuleDto,
  PricePreviewDto,
  PricingRuleDto,
  SalesChannelDto,
} from '@/types/api';

const kinds = ['MarkupPercent', 'MarginPercent', 'CompareAtPercent'] as const;

export function PricingPage() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const canWrite = user?.permissions.includes('pricing.write') ?? false;

  const [name, setName] = useState('Default markup');
  const [kind, setKind] = useState<(typeof kinds)[number]>('MarkupPercent');
  const [percentRate, setPercentRate] = useState('40');
  const [priority, setPriority] = useState('10');
  const [salesChannelId, setSalesChannelId] = useState('');
  const [previewCost, setPreviewCost] = useState('100');
  const [previewChannelId, setPreviewChannelId] = useState('');
  const [preview, setPreview] = useState<PricePreviewDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const rulesQuery = useQuery({
    queryKey: ['catalog', 'pricing-rules'],
    queryFn: () => apiRequest<PricingRuleDto[]>('/catalog/pricing-rules'),
  });
  const canReadChannels = user?.permissions.includes('publishing.read') ?? false;
  const channelsQuery = useQuery({
    queryKey: ['publishing', 'channels'],
    enabled: canReadChannels,
    queryFn: () => apiRequest<SalesChannelDto[]>('/publishing/channels'),
  });

  const createMutation = useMutation({
    mutationFn: (body: CreatePricingRuleDto) =>
      apiRequest<PricingRuleDto>('/catalog/pricing-rules', {
        method: 'POST',
        body: JSON.stringify(body),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'pricing-rules'] });
      setError(null);
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const deleteMutation = useMutation({
    mutationFn: (ruleId: string) =>
      apiRequest<void>(`/catalog/pricing-rules/${ruleId}`, { method: 'DELETE' }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'pricing-rules'] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const previewMutation = useMutation({
    mutationFn: () =>
      apiRequest<PricePreviewDto>('/catalog/pricing-rules/preview', {
        method: 'POST',
        body: JSON.stringify({
          costAmount: Number(previewCost),
          salesChannelId: previewChannelId || null,
        }),
      }),
    onSuccess: (result) => {
      setPreview(result);
      setError(null);
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">Pricing</h1>
        <p className="mt-2 text-muted-foreground">
          Org markup/margin and compare-at rules. Applied on import approve and catalog upsert when
          cost is present; publish resolves channel overrides.
        </p>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}

      <Card className="space-y-4 p-5">
        <h2 className="text-lg font-medium">Rules</h2>
        {(rulesQuery.data ?? []).map((rule) => (
          <div
            key={rule.id}
            className="flex flex-wrap items-center justify-between gap-3 border-b border-border py-3 last:border-0"
          >
            <div>
              <p className="font-medium">
                {rule.name} · {rule.kind} {String(rule.percentRate)}%
              </p>
              <p className="text-sm text-muted-foreground">
                Priority {rule.priority}
                {rule.salesChannelId ? ` · channel ${rule.salesChannelId}` : ' · org default'}
                {rule.isEnabled ? '' : ' · disabled'}
              </p>
            </div>
            {canWrite && (
              <Button
                variant="outline"
                size="sm"
                disabled={deleteMutation.isPending}
                onClick={() => {
                  deleteMutation.mutate(rule.id);
                }}
              >
                Delete
              </Button>
            )}
          </div>
        ))}
        {rulesQuery.isSuccess && rulesQuery.data.length === 0 && (
          <p className="text-sm text-muted-foreground">No pricing rules yet.</p>
        )}

        {canWrite && (
          <div className="grid gap-3 md:grid-cols-2">
            <Input
              value={name}
              onChange={(e) => {
                setName(e.target.value);
              }}
              aria-label="Rule name"
            />
            <select
              className="h-10 rounded-md border border-input bg-background px-3 text-sm"
              value={kind}
              onChange={(e) => {
                setKind(e.target.value as (typeof kinds)[number]);
              }}
              aria-label="Rule kind"
            >
              {kinds.map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </select>
            <Input
              value={percentRate}
              onChange={(e) => {
                setPercentRate(e.target.value);
              }}
              aria-label="Percent rate"
            />
            <Input
              value={priority}
              onChange={(e) => {
                setPriority(e.target.value);
              }}
              aria-label="Priority"
            />
            <select
              className="h-10 rounded-md border border-input bg-background px-3 text-sm md:col-span-2"
              value={salesChannelId}
              onChange={(e) => {
                setSalesChannelId(e.target.value);
              }}
              aria-label="Sales channel override"
            >
              <option value="">Org default (no channel override)</option>
              {(channelsQuery.data ?? []).map((channel) => (
                <option key={channel.id} value={channel.id}>
                  {channel.displayName} ({channel.code})
                </option>
              ))}
            </select>
            <Button
              onClick={() => {
                const rate = Number(percentRate);
                const prio = Number(priority);
                if (Number.isNaN(rate) || rate < 0 || Number.isNaN(prio)) {
                  setError('Percent rate and priority must be valid numbers.');
                  return;
                }
                createMutation.mutate({
                  name,
                  kind,
                  percentRate: rate,
                  priority: prio,
                  salesChannelId: salesChannelId || null,
                  isEnabled: true,
                });
              }}
            >
              Add rule
            </Button>
          </div>
        )}
      </Card>

      <Card className="space-y-4 p-5">
        <h2 className="text-lg font-medium">Preview</h2>
        <div className="grid gap-3 md:grid-cols-3">
          <Input
            value={previewCost}
            onChange={(e) => {
              setPreviewCost(e.target.value);
            }}
            aria-label="Preview cost"
            placeholder="Cost"
          />
          <select
            className="h-10 rounded-md border border-input bg-background px-3 text-sm"
            value={previewChannelId}
            onChange={(e) => {
              setPreviewChannelId(e.target.value);
            }}
            aria-label="Preview channel"
          >
            <option value="">Catalog (org rules)</option>
            {(channelsQuery.data ?? []).map((channel) => (
              <option key={channel.id} value={channel.id}>
                {channel.displayName}
              </option>
            ))}
          </select>
          <Button
            variant="outline"
            disabled={previewMutation.isPending}
            onClick={() => {
              previewMutation.mutate();
            }}
          >
            Resolve
          </Button>
        </div>
        {preview && (
          <p className="text-sm">
            Sell {preview.priceAmount ?? '—'} · Compare-at {preview.compareAtAmount ?? '—'}
          </p>
        )}
      </Card>
    </div>
  );
}
