import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '@/auth/auth-context';
import { Button, Card, Input } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type {
  AdjustInventoryDto,
  CreateProductDto,
  InventoryDto,
  ProductDto,
  PublishRunDto,
  TaxonomyItemDto,
  UpsertVariantDto,
} from '@/types/api';

export function ProductDetailPage() {
  const { productId } = useParams();
  const isNew = productId === 'new';
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const canWrite = user?.permissions.includes('catalog.write') ?? false;
  const canAdjust = user?.permissions.includes('inventory.adjust') ?? false;
  const canPublish = user?.permissions.includes('publishing.manage') ?? false;
  const canReadPublish = user?.permissions.includes('publishing.read') ?? false;

  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [styleCode, setStyleCode] = useState('');
  const [teamId, setTeamId] = useState('');
  const [seasonId, setSeasonId] = useState('');
  const [seoTitle, setSeoTitle] = useState('');
  const [seoDescription, setSeoDescription] = useState('');
  const [seoHandle, setSeoHandle] = useState('');
  const [sku, setSku] = useState('');
  const [size, setSize] = useState('M');
  const [priceAmount, setPriceAmount] = useState('');
  const [costAmount, setCostAmount] = useState('');
  const [compareAtAmount, setCompareAtAmount] = useState('');
  const [delta, setDelta] = useState('1');
  const [reason, setReason] = useState('manual adjust');
  const [error, setError] = useState<string | null>(null);

  const productQuery = useQuery({
    queryKey: ['catalog', 'product', productId],
    enabled: !isNew && !!productId,
    queryFn: () => {
      if (!productId) throw new Error('Product id is required.');
      return apiRequest<ProductDto>(`/catalog/products/${productId}`);
    },
  });
  const teamsQuery = useQuery({
    queryKey: ['catalog', 'teams'],
    queryFn: () => apiRequest<TaxonomyItemDto[]>('/catalog/teams'),
  });
  const seasonsQuery = useQuery({
    queryKey: ['catalog', 'seasons'],
    queryFn: () => apiRequest<TaxonomyItemDto[]>('/catalog/seasons'),
  });
  const publishRunsQuery = useQuery({
    queryKey: ['publishing', 'product-runs', productId],
    enabled: !isNew && !!productId && canReadPublish,
    queryFn: () => {
      if (!productId) throw new Error('Product id is required.');
      return apiRequest<PublishRunDto[]>(`/publishing/products/${productId}/runs`);
    },
  });

  const product = productQuery.data;
  const latestPublishRun = publishRunsQuery.data?.[0];
  useEffect(() => {
    if (!product) return;
    setName(product.name);
    setSlug(product.slug);
    setStyleCode(product.styleCode ?? '');
    setTeamId(product.teamId ?? '');
    setSeasonId(product.seasonId ?? '');
    setSeoTitle(product.seoTitle ?? '');
    setSeoDescription(product.seoDescription ?? '');
    setSeoHandle(product.seoHandle ?? '');
  }, [product]);

  const createMutation = useMutation({
    mutationFn: (body: CreateProductDto) =>
      apiRequest<ProductDto>('/catalog/products', { method: 'POST', body: JSON.stringify(body) }),
    onSuccess: (created) => {
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'products'] });
      void navigate(`/products/${created.id}`);
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const requireProductId = () => {
    if (!productId) throw new Error('Product id is required.');
    return productId;
  };
  const saveMutation = useMutation({
    mutationFn: () =>
      apiRequest<ProductDto>(`/catalog/products/${requireProductId()}`, {
        method: 'PUT',
        body: JSON.stringify({
          name,
          slug,
          styleCode: styleCode || null,
          teamId: teamId || null,
          seasonId: seasonId || null,
          categoryIds: product?.categoryIds ?? [],
          tagIds: product?.tagIds ?? [],
          seoTitle: seoTitle || null,
          seoDescription: seoDescription || null,
          seoHandle: seoHandle || null,
        }),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'product', productId] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const variantMutation = useMutation({
    mutationFn: (body: UpsertVariantDto) =>
      apiRequest<ProductDto>(`/catalog/products/${requireProductId()}/variants`, {
        method: 'PUT',
        body: JSON.stringify(body),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'product', productId] });
      setSku('');
      setPriceAmount('');
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const activateMutation = useMutation({
    mutationFn: () =>
      apiRequest<ProductDto>(`/catalog/products/${requireProductId()}/activate`, { method: 'POST' }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'product', productId] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const archiveMutation = useMutation({
    mutationFn: () =>
      apiRequest<ProductDto>(`/catalog/products/${requireProductId()}/archive`, { method: 'POST' }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'product', productId] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const republishMutation = useMutation({
    mutationFn: () =>
      apiRequest<PublishRunDto>(`/publishing/products/${requireProductId()}/republish`, {
        method: 'POST',
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['publishing', 'product-runs', productId] });
      void queryClient.invalidateQueries({ queryKey: ['publishing', 'runs'] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const adjustMutation = useMutation({
    mutationFn: ({ variantId, body }: { variantId: string; body: AdjustInventoryDto }) =>
      apiRequest<InventoryDto>(`/inventory/variants/${variantId}/adjust`, {
        method: 'POST',
        body: JSON.stringify(body),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'product', productId] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const imageMutation = useMutation({
    mutationFn: async (file: File) => {
      const form = new FormData();
      form.append('file', file);
      form.append('sortOrder', '0');
      return apiRequest<ProductDto>(`/catalog/products/${requireProductId()}/images`, {
        method: 'POST',
        body: form,
      });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'product', productId] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">
          {isNew ? 'New product' : (product?.name ?? 'Product')}
        </h1>
        <p className="mt-2 text-muted-foreground">
          {isNew ? 'Create a draft product.' : `Status: ${product?.status ?? '…'}`}
          {latestPublishRun
            ? ` · Publish: ${latestPublishRun.status}${latestPublishRun.error ? ` (${latestPublishRun.error})` : ''}`
            : ''}
        </p>
      </div>
      {error && (
        <Card className="border-destructive/50 p-4" role="alert">
          {error}
        </Card>
      )}
      <Card className="grid gap-4 p-5 md:grid-cols-2">
        <label className="grid gap-1 text-sm">
          Name
          <Input
            value={name}
            onChange={(e) => {
              setName(e.target.value);
            }}
            disabled={!canWrite}
          />
        </label>
        <label className="grid gap-1 text-sm">
          Slug
          <Input
            value={slug}
            onChange={(e) => {
              setSlug(e.target.value);
            }}
            disabled={!canWrite}
          />
        </label>
        <label className="grid gap-1 text-sm">
          Style code
          <Input
            value={styleCode}
            onChange={(e) => {
              setStyleCode(e.target.value);
            }}
            disabled={!canWrite}
          />
        </label>
        <label className="grid gap-1 text-sm">
          Team
          <select
            className="h-10 rounded-md border border-input bg-background px-3 text-sm"
            value={teamId}
            disabled={!canWrite}
            onChange={(e) => {
              setTeamId(e.target.value);
            }}
          >
            <option value="">Select team</option>
            {(teamsQuery.data ?? []).map((team) => (
              <option key={team.id} value={team.id}>
                {team.name}
              </option>
            ))}
          </select>
        </label>
        <label className="grid gap-1 text-sm">
          Season
          <select
            className="h-10 rounded-md border border-input bg-background px-3 text-sm"
            value={seasonId}
            disabled={!canWrite}
            onChange={(e) => {
              setSeasonId(e.target.value);
            }}
          >
            <option value="">Select season</option>
            {(seasonsQuery.data ?? []).map((season) => (
              <option key={season.id} value={season.id}>
                {season.name}
              </option>
            ))}
          </select>
        </label>
        <label className="grid gap-1 text-sm md:col-span-2">
          SEO title
          <Input
            value={seoTitle}
            onChange={(e) => {
              setSeoTitle(e.target.value);
            }}
            disabled={!canWrite}
            placeholder="Storefront title"
          />
        </label>
        <label className="grid gap-1 text-sm">
          SEO handle
          <Input
            value={seoHandle}
            onChange={(e) => {
              setSeoHandle(e.target.value);
            }}
            disabled={!canWrite}
            placeholder="optional-handle"
          />
        </label>
        <label className="grid gap-1 text-sm">
          SEO description
          <Input
            value={seoDescription}
            onChange={(e) => {
              setSeoDescription(e.target.value);
            }}
            disabled={!canWrite}
            placeholder="Meta description"
          />
        </label>
        {(canWrite || canPublish) && (
          <div className="flex flex-wrap items-end gap-2 md:col-span-2">
            {canWrite &&
              (isNew ? (
                <Button
                  onClick={() => {
                    setError(null);
                    createMutation.mutate({
                      name,
                      slug,
                      styleCode: styleCode || null,
                      teamId: teamId || null,
                      seasonId: seasonId || null,
                    });
                  }}
                >
                  Create draft
                </Button>
              ) : (
                <>
                  <Button
                    onClick={() => {
                      setError(null);
                      saveMutation.mutate();
                    }}
                  >
                    Save details
                  </Button>
                  <Button
                    variant="outline"
                    onClick={() => {
                      setError(null);
                      activateMutation.mutate();
                    }}
                  >
                    Activate
                  </Button>
                  <Button
                    variant="destructive"
                    onClick={() => {
                      setError(null);
                      archiveMutation.mutate();
                    }}
                  >
                    Archive
                  </Button>
                </>
              ))}
            {canPublish && !isNew && product?.status === 'Active' && (
              <Button
                variant="outline"
                disabled={republishMutation.isPending}
                onClick={() => {
                  setError(null);
                  republishMutation.mutate();
                }}
              >
                Republish
              </Button>
            )}
          </div>
        )}
      </Card>

      {!isNew && product && (
        <>
          <Card className="space-y-4 p-5">
            <h2 className="text-xl font-medium">Variants</h2>
            {product.variants.map((variant) => (
              <div
                key={variant.id}
                className="flex flex-wrap items-center justify-between gap-3 border-b border-border py-3 last:border-0"
              >
                <div>
                  <p className="font-medium">
                    {variant.sku} · {variant.size}
                    {variant.priceAmount != null
                      ? ` · ${String(variant.priceAmount)} ${product.currency}`
                      : ' · no price'}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    Cost {variant.costAmount ?? '—'} · Compare-at {variant.compareAtAmount ?? '—'} ·
                    On hand {variant.inventory.onHand} · Reserved {variant.inventory.reserved}
                  </p>
                </div>
                {canAdjust && (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => {
                      setError(null);
                      adjustMutation.mutate({
                        variantId: variant.id,
                        body: {
                          deltaOnHand: Number(delta),
                          reason,
                          expectedRowVersion: variant.inventory.rowVersion,
                        },
                      });
                    }}
                  >
                    Adjust
                  </Button>
                )}
              </div>
            ))}
            {canWrite && (
              <div className="grid gap-3 md:grid-cols-3">
                <Input
                  placeholder="SKU"
                  value={sku}
                  onChange={(e) => {
                    setSku(e.target.value);
                  }}
                />
                <Input
                  placeholder="Size"
                  value={size}
                  onChange={(e) => {
                    setSize(e.target.value);
                  }}
                />
                <Input
                  placeholder={`Price (${product.currency})`}
                  value={priceAmount}
                  inputMode="decimal"
                  onChange={(e) => {
                    setPriceAmount(e.target.value);
                  }}
                />
                <Input
                  placeholder={`Cost (${product.currency})`}
                  value={costAmount}
                  inputMode="decimal"
                  onChange={(e) => {
                    setCostAmount(e.target.value);
                  }}
                />
                <Input
                  placeholder={`Compare-at (${product.currency})`}
                  value={compareAtAmount}
                  inputMode="decimal"
                  onChange={(e) => {
                    setCompareAtAmount(e.target.value);
                  }}
                />
                <Button
                  onClick={() => {
                    setError(null);
                    const parseMoney = (value: string) => {
                      if (value.trim() === '') return null;
                      const parsed = Number(value);
                      if (Number.isNaN(parsed) || parsed < 0) {
                        throw new Error('Money fields must be non-negative numbers.');
                      }
                      return parsed;
                    };
                    try {
                      variantMutation.mutate({
                        sku,
                        size,
                        sortOrder: product.variants.length,
                        priceAmount: parseMoney(priceAmount),
                        costAmount: parseMoney(costAmount),
                        compareAtAmount: parseMoney(compareAtAmount),
                      });
                    } catch (err) {
                      setError(err instanceof Error ? err.message : 'Invalid variant values.');
                    }
                  }}
                >
                  Add variant
                </Button>
              </div>
            )}
            {canAdjust && (
              <div className="grid gap-3 md:grid-cols-2">
                <Input
                  value={delta}
                  onChange={(e) => {
                    setDelta(e.target.value);
                  }}
                  aria-label="Inventory delta"
                />
                <Input
                  value={reason}
                  onChange={(e) => {
                    setReason(e.target.value);
                  }}
                  aria-label="Adjust reason"
                />
              </div>
            )}
          </Card>
          <Card className="space-y-4 p-5">
            <h2 className="text-xl font-medium">Images</h2>
            <div className="grid gap-3 sm:grid-cols-3">
              {product.images.map((image) => (
                <figure key={image.id} className="overflow-hidden rounded-md border">
                  <img
                    src={image.url}
                    alt={image.altText ?? product.name}
                    className="h-40 w-full object-cover"
                  />
                </figure>
              ))}
            </div>
            {canWrite && (
              <Input
                type="file"
                accept="image/*"
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file) {
                    setError(null);
                    imageMutation.mutate(file);
                  }
                }}
              />
            )}
          </Card>
        </>
      )}
    </div>
  );
}
