import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '@/auth/auth-context';
import { Button, Card, Input } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type { CollectionDto, CreateCollectionDto, TaxonomyItemDto } from '@/types/api';

const kinds = ['Manual', 'Taxonomy'] as const;

export function CollectionsPage() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const canWrite = user?.permissions.includes('catalog.write') ?? false;
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [kind, setKind] = useState<(typeof kinds)[number]>('Manual');
  const [description, setDescription] = useState('');
  const [teamId, setTeamId] = useState('');
  const [seasonId, setSeasonId] = useState('');
  const [error, setError] = useState<string | null>(null);

  const collectionsQuery = useQuery({
    queryKey: ['catalog', 'collections'],
    queryFn: () => apiRequest<CollectionDto[]>('/catalog/collections'),
  });
  const teamsQuery = useQuery({
    queryKey: ['catalog', 'teams'],
    queryFn: () => apiRequest<TaxonomyItemDto[]>('/catalog/teams'),
  });
  const seasonsQuery = useQuery({
    queryKey: ['catalog', 'seasons'],
    queryFn: () => apiRequest<TaxonomyItemDto[]>('/catalog/seasons'),
  });

  const createMutation = useMutation({
    mutationFn: (body: CreateCollectionDto) =>
      apiRequest<CollectionDto>('/catalog/collections', {
        method: 'POST',
        body: JSON.stringify(body),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'collections'] });
      setName('');
      setSlug('');
      setDescription('');
      setError(null);
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const deleteMutation = useMutation({
    mutationFn: (id: string) => apiRequest<void>(`/catalog/collections/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'collections'] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">Collections</h1>
        <p className="mt-2 text-muted-foreground">
          Manual merchandising groups or taxonomy rules. Publish maps members to Shopify collections and
          WooCommerce categories.
        </p>
      </div>
      {error && (
        <p className="text-sm text-destructive" role="alert">
          {error}
        </p>
      )}

      <Card className="space-y-4 p-5">
        {(collectionsQuery.data ?? []).map((collection) => (
          <div
            key={collection.id}
            className="flex flex-wrap items-center justify-between gap-3 border-b border-border py-3 last:border-0"
          >
            <div>
              <p className="font-medium">
                {collection.name} · {collection.slug}
              </p>
              <p className="text-sm text-muted-foreground">
                {collection.membershipKind} · {collection.memberCount} product
                {collection.memberCount === 1 ? '' : 's'}
              </p>
              {collection.productIds.slice(0, 4).map((productId) => (
                <Link
                  key={productId}
                  to={`/products/${productId}`}
                  className="mr-2 text-xs text-primary hover:underline"
                >
                  {productId.slice(0, 8)}
                </Link>
              ))}
            </div>
            {canWrite && (
              <Button
                variant="outline"
                size="sm"
                disabled={deleteMutation.isPending}
                onClick={() => {
                  deleteMutation.mutate(collection.id);
                }}
              >
                Delete
              </Button>
            )}
          </div>
        ))}
        {collectionsQuery.isSuccess && collectionsQuery.data.length === 0 && (
          <p className="text-sm text-muted-foreground">No collections yet.</p>
        )}

        {canWrite && (
          <div className="grid gap-3 md:grid-cols-2">
            <Input
              placeholder="Name"
              value={name}
              onChange={(e) => {
                setName(e.target.value);
              }}
            />
            <Input
              placeholder="handle-slug"
              value={slug}
              onChange={(e) => {
                setSlug(e.target.value);
              }}
            />
            <select
              className="h-10 rounded-md border border-input bg-background px-3 text-sm"
              value={kind}
              onChange={(e) => {
                setKind(e.target.value as (typeof kinds)[number]);
              }}
              aria-label="Membership kind"
            >
              {kinds.map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </select>
            <Input
              placeholder="Description"
              value={description}
              onChange={(e) => {
                setDescription(e.target.value);
              }}
            />
            {kind === 'Taxonomy' && (
              <>
                <select
                  className="h-10 rounded-md border border-input bg-background px-3 text-sm"
                  value={teamId}
                  onChange={(e) => {
                    setTeamId(e.target.value);
                  }}
                  aria-label="Team filter"
                >
                  <option value="">Any team</option>
                  {(teamsQuery.data ?? []).map((team) => (
                    <option key={team.id} value={team.id}>
                      {team.name}
                    </option>
                  ))}
                </select>
                <select
                  className="h-10 rounded-md border border-input bg-background px-3 text-sm"
                  value={seasonId}
                  onChange={(e) => {
                    setSeasonId(e.target.value);
                  }}
                  aria-label="Season filter"
                >
                  <option value="">Any season</option>
                  {(seasonsQuery.data ?? []).map((season) => (
                    <option key={season.id} value={season.id}>
                      {season.name}
                    </option>
                  ))}
                </select>
              </>
            )}
            <Button
              onClick={() => {
                if (!name.trim() || !slug.trim()) {
                  setError('Name and slug are required.');
                  return;
                }
                const body: CreateCollectionDto = {
                  name,
                  slug,
                  membershipKind: kind,
                  description: description || null,
                  teamId: kind === 'Taxonomy' && teamId ? teamId : null,
                  seasonId: kind === 'Taxonomy' && seasonId ? seasonId : null,
                };
                if (kind === 'Manual') {
                  body.productIds = [];
                }
                createMutation.mutate(body);
              }}
            >
              Add collection
            </Button>
          </div>
        )}
      </Card>
    </div>
  );
}
