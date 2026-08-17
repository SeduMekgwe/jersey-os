import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '@/auth/auth-context';
import { Button, Card, Input } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type {
  OrganizationIntegrationSettingsDto,
  PublishRunDto,
  SalesChannelDto,
  WebhookDeliveryDto,
} from '@/types/api';

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
    refetchInterval: 10_000,
  });
  const deliveriesQuery = useQuery({
    queryKey: ['publishing', 'webhook-deliveries'],
    queryFn: () => apiRequest<WebhookDeliveryDto[]>('/publishing/webhook-deliveries'),
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
          Multi-channel sync (Shopify, WooCommerce) for Active catalog products. Enable a channel
          only when its credentials are configured.
        </p>
      </div>

      {canManage && <ChannelSettings />}

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

      <section className="space-y-3">
        <h2 className="text-lg font-medium">Shopify webhook deliveries</h2>
        <p className="text-sm text-muted-foreground">
          Inventory topics: orders/create, orders/cancelled, orders/fulfilled, fulfillments/create,
          refunds/create.
        </p>
        {(deliveriesQuery.data ?? []).map((delivery) => (
          <Card key={delivery.id} className="space-y-1 p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="font-medium">{delivery.topic}</p>
              <span className="text-sm text-muted-foreground">{delivery.status}</span>
            </div>
            <p className="text-sm text-muted-foreground">
              {delivery.webhookId}
              {` · ${new Date(delivery.createdAtUtc).toLocaleString()}`}
            </p>
            {delivery.error && <p className="text-sm text-destructive">{delivery.error}</p>}
          </Card>
        ))}
        {deliveriesQuery.isSuccess && deliveriesQuery.data.length === 0 && (
          <p className="text-sm text-muted-foreground">No webhook deliveries yet.</p>
        )}
      </section>
    </div>
  );
}

function ChannelSettings() {
  const queryClient = useQueryClient();
  const [shopDomain, setShopDomain] = useState('');
  const [shopifyToken, setShopifyToken] = useState('');
  const [shopifyWebhook, setShopifyWebhook] = useState('');
  const [shopifyVersion, setShopifyVersion] = useState('');
  const [wooUrl, setWooUrl] = useState('');
  const [wooKey, setWooKey] = useState('');
  const [wooSecret, setWooSecret] = useState('');
  const [wooVersion, setWooVersion] = useState('');
  const [error, setError] = useState<string | null>(null);

  const settingsQuery = useQuery({
    queryKey: ['publishing', 'settings'],
    queryFn: () => apiRequest<OrganizationIntegrationSettingsDto>('/publishing/settings'),
  });

  useEffect(() => {
    if (!settingsQuery.data) return;
    setShopDomain(settingsQuery.data.shopifyShopDomain ?? '');
    setShopifyVersion(settingsQuery.data.shopifyApiVersion ?? '');
    setWooUrl(settingsQuery.data.wooStoreBaseUrl ?? '');
    setWooVersion(settingsQuery.data.wooApiVersion ?? '');
  }, [settingsQuery.data]);

  const save = useMutation({
    mutationFn: () =>
      apiRequest<OrganizationIntegrationSettingsDto>('/publishing/settings', {
        method: 'PUT',
        body: JSON.stringify({
          shopifyShopDomain: shopDomain || null,
          shopifyAccessToken: shopifyToken || null,
          shopifyWebhookSecret: shopifyWebhook || null,
          shopifyApiVersion: shopifyVersion || null,
          wooStoreBaseUrl: wooUrl || null,
          wooConsumerKey: wooKey || null,
          wooConsumerSecret: wooSecret || null,
          wooApiVersion: wooVersion || null,
        }),
      }),
    onSuccess: () => {
      setError(null);
      setShopifyToken('');
      setShopifyWebhook('');
      setWooKey('');
      setWooSecret('');
      void queryClient.invalidateQueries({ queryKey: ['publishing', 'settings'] });
    },
    onError: (err: Error) => setError(err.message),
  });

  const settings = settingsQuery.data;
  return (
    <section className="space-y-3">
      <h2 className="text-lg font-medium">Channel credentials</h2>
      <p className="text-sm text-muted-foreground">
        Per-organization overlay. Leave secrets blank to keep the current value. Global config is
        used when a field is empty.
      </p>
      {error && (
        <Card className="border-destructive/50 p-4" role="alert">
          {error}
        </Card>
      )}
      <Card className="grid gap-3 p-4 md:grid-cols-2">
        <label className="block text-sm">
          Shopify shop domain
          <Input className="mt-1" value={shopDomain} onChange={(e) => setShopDomain(e.target.value)} />
        </label>
        <label className="block text-sm">
          Shopify API version
          <Input
            className="mt-1"
            value={shopifyVersion}
            onChange={(e) => setShopifyVersion(e.target.value)}
          />
        </label>
        <label className="block text-sm">
          Shopify access token {settings?.shopifyAccessTokenConfigured ? '(configured)' : ''}
          <Input
            className="mt-1"
            type="password"
            value={shopifyToken}
            onChange={(e) => setShopifyToken(e.target.value)}
          />
        </label>
        <label className="block text-sm">
          Shopify webhook secret {settings?.shopifyWebhookSecretConfigured ? '(configured)' : ''}
          <Input
            className="mt-1"
            type="password"
            value={shopifyWebhook}
            onChange={(e) => setShopifyWebhook(e.target.value)}
          />
        </label>
        <label className="block text-sm">
          WooCommerce store URL
          <Input className="mt-1" value={wooUrl} onChange={(e) => setWooUrl(e.target.value)} />
        </label>
        <label className="block text-sm">
          WooCommerce API version
          <Input className="mt-1" value={wooVersion} onChange={(e) => setWooVersion(e.target.value)} />
        </label>
        <label className="block text-sm">
          WooCommerce consumer key {settings?.wooConsumerKeyConfigured ? '(configured)' : ''}
          <Input className="mt-1" type="password" value={wooKey} onChange={(e) => setWooKey(e.target.value)} />
        </label>
        <label className="block text-sm">
          WooCommerce consumer secret {settings?.wooConsumerSecretConfigured ? '(configured)' : ''}
          <Input
            className="mt-1"
            type="password"
            value={wooSecret}
            onChange={(e) => setWooSecret(e.target.value)}
          />
        </label>
        <div className="md:col-span-2">
          <Button disabled={save.isPending} onClick={() => save.mutate()}>
            Save channel settings
          </Button>
        </div>
      </Card>
    </section>
  );
}
