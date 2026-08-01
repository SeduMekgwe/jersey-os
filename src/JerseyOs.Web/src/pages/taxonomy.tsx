import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useAuth } from '@/auth/auth-context';
import { Button, Card, Input } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type { CreateTaxonomyItemDto, TaxonomyItemDto } from '@/types/api';

type TaxonomyKind = 'teams' | 'seasons' | 'categories' | 'tags';

function TaxonomySection({ kind, title }: { kind: TaxonomyKind; title: string }) {
  const { user } = useAuth();
  const canWrite = user?.permissions.includes('catalog.write') ?? false;
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [error, setError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ['catalog', kind],
    queryFn: () => apiRequest<TaxonomyItemDto[]>(`/catalog/${kind}`),
  });
  const create = useMutation({
    mutationFn: (body: CreateTaxonomyItemDto) =>
      apiRequest<TaxonomyItemDto>(`/catalog/${kind}`, {
        method: 'POST',
        body: JSON.stringify(body),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['catalog', kind] });
      setName('');
      setSlug('');
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });

  return (
    <Card className="space-y-4 p-5">
      <h2 className="text-xl font-medium">{title}</h2>
      {error && (
        <p className="text-sm text-destructive" role="alert">
          {error}
        </p>
      )}
      <ul className="space-y-2 text-sm">
        {(query.data ?? []).map((item) => (
          <li key={item.id} className="flex justify-between gap-3">
            <span>{item.name}</span>
            <span className="text-muted-foreground">{item.slug}</span>
          </li>
        ))}
      </ul>
      {canWrite && (
        <div className="grid gap-3 md:grid-cols-3">
          <Input
            placeholder="Name"
            value={name}
            onChange={(e) => {
              setName(e.target.value);
            }}
          />
          <Input
            placeholder="slug"
            value={slug}
            onChange={(e) => {
              setSlug(e.target.value);
            }}
          />
          <Button
            onClick={() => {
              setError(null);
              create.mutate({ name, slug });
            }}
          >
            Add
          </Button>
        </div>
      )}
    </Card>
  );
}

export function TaxonomyPage() {
  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">Taxonomy</h1>
        <p className="mt-2 text-muted-foreground">
          Teams, seasons, categories, and tags used to classify products.
        </p>
      </div>
      <TaxonomySection kind="teams" title="Teams" />
      <TaxonomySection kind="seasons" title="Seasons" />
      <TaxonomySection kind="categories" title="Categories" />
      <TaxonomySection kind="tags" title="Tags" />
    </div>
  );
}
