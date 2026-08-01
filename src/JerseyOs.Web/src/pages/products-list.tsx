import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { useAuth } from '@/auth/auth-context';
import { Button, Card, Input } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type { PagedProductsDto } from '@/types/api';
import { useState } from 'react';

export function ProductsListPage() {
  const { user } = useAuth();
  const canWrite = user?.permissions.includes('catalog.write') ?? false;
  const [search, setSearch] = useState('');
  const query = useQuery({
    queryKey: ['catalog', 'products', search],
    queryFn: () => {
      const params = new URLSearchParams();
      if (search.trim()) params.set('search', search.trim());
      const qs = params.toString();
      return apiRequest<PagedProductsDto>(`/catalog/products${qs ? `?${qs}` : ''}`);
    },
  });

  return (
    <div className="mx-auto max-w-5xl">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-3xl font-semibold tracking-tight">Products</h1>
          <p className="mt-2 text-muted-foreground">
            Org-scoped catalog with variants, taxonomy, and inventory.
          </p>
        </div>
        {canWrite && (
          <Button asChild>
            <Link to="/products/new">New product</Link>
          </Button>
        )}
      </div>
      <div className="mt-6 max-w-md">
        <Input
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
          }}
          placeholder="Search name, slug, or SKU"
          aria-label="Search products"
        />
      </div>
      {query.isPending && (
        <p className="mt-8" aria-live="polite">
          Loading products…
        </p>
      )}
      {query.isError && (
        <Card className="mt-8 border-destructive/50 p-5" role="alert">
          <p className="text-sm">
            {query.error instanceof Error ? query.error.message : 'Products could not be loaded.'}
          </p>
        </Card>
      )}
      {query.data && (
        <div className="mt-6 grid gap-3">
          {query.data.items.length === 0 && (
            <p className="text-muted-foreground">No products match this search.</p>
          )}
          {query.data.items.map((product) => (
            <Card key={product.id} className="flex items-center justify-between gap-4 p-5">
              <div>
                <Link
                  to={`/products/${product.id}`}
                  className="text-lg font-medium hover:underline"
                >
                  {product.name}
                </Link>
                <p className="text-sm text-muted-foreground">
                  {product.slug} · {product.status} · {product.variantCount} variants
                </p>
              </div>
              <Button asChild variant="outline" size="sm">
                <Link to={`/products/${product.id}`}>Open</Link>
              </Button>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
