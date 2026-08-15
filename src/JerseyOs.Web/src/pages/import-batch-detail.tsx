import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '@/auth/auth-context';
import { Button, Card } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type { AiGenerationDto, ImportBatchDetailDto, ImportItemDto } from '@/types/api';
import { useState } from 'react';

const importAiKinds = [
  { kind: 'title', label: 'Title' },
  { kind: 'description', label: 'Description' },
  { kind: 'seo-title', label: 'SEO title' },
  { kind: 'seo-description', label: 'SEO description' },
] as const;

export function ImportBatchDetailPage() {
  const { batchId } = useParams();
  const { user } = useAuth();
  const canReview = user?.permissions.includes('import.review') ?? false;
  const canAiRead = user?.permissions.includes('ai.read') ?? false;
  const canAiWrite = user?.permissions.includes('ai.write') ?? false;
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const batchQuery = useQuery({
    queryKey: ['import', 'batch', batchId],
    enabled: !!batchId,
    queryFn: () => {
      if (!batchId) throw new Error('Batch id is required.');
      return apiRequest<ImportBatchDetailDto>(`/import/batches/${batchId}`);
    },
    refetchInterval: (query) =>
      query.state.data?.status === 'Uploaded' || query.state.data?.status === 'Parsing' ? 2_000 : false,
  });

  const approve = useMutation({
    mutationFn: (itemId: string) =>
      apiRequest<ImportItemDto>(`/import/items/${itemId}/approve`, {
        method: 'POST',
        body: JSON.stringify({}),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['import', 'batch', batchId] });
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'products'] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const reject = useMutation({
    mutationFn: (itemId: string) =>
      apiRequest<ImportItemDto>(`/import/items/${itemId}/reject`, {
        method: 'POST',
        body: JSON.stringify({ note: 'rejected in review' }),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['import', 'batch', batchId] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const generationsQuery = useQuery({
    queryKey: ['ai', 'generations', 'import', batchId],
    enabled: !!batchId && canAiRead,
    queryFn: () => apiRequest<AiGenerationDto[]>('/ai/generations?targetType=import-item'),
    refetchInterval: (query) =>
      query.state.data?.some((item) => item.status === 'Pending') ? 2_000 : false,
  });
  const generateMutation = useMutation({
    mutationFn: (body: { kind: string; targetId: string }) =>
      apiRequest<AiGenerationDto>('/ai/generations', {
        method: 'POST',
        body: JSON.stringify({
          kind: body.kind,
          targetType: 'import-item',
          targetId: body.targetId,
        }),
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['ai', 'generations', 'import', batchId] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });
  const approveGenerationMutation = useMutation({
    mutationFn: (id: string) =>
      apiRequest<AiGenerationDto>(`/ai/generations/${id}/approve`, { method: 'POST' }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['ai', 'generations', 'import', batchId] });
      void queryClient.invalidateQueries({ queryKey: ['import', 'batch', batchId] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });

  const batch = batchQuery.data;
  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div>
        <p className="text-sm text-muted-foreground">
          <Link to="/import" className="hover:underline">
            Imports
          </Link>
        </p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight">
          {batch?.fileName ?? 'Import batch'}
        </h1>
        <p className="mt-2 text-muted-foreground">
          {batch ? `${batch.supplierName} · ${batch.status}` : 'Loading…'}
        </p>
      </div>
      {error && (
        <Card className="border-destructive/50 p-4" role="alert">
          {error}
        </Card>
      )}
      {batch?.errorSummary && (
        <Card className="border-destructive/50 p-4" role="alert">
          {batch.errorSummary}
        </Card>
      )}
      <Card className="overflow-x-auto p-0">
        <table className="w-full text-left text-sm">
          <thead className="border-b bg-muted/40">
            <tr>
              <th className="px-4 py-3">SKU</th>
              <th className="px-4 py-3">Name</th>
              <th className="px-4 py-3">Size</th>
              <th className="px-4 py-3">Match</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody>
            {(batch?.items ?? []).map((item) => (
              <tr key={item.id} className="border-b last:border-0">
                <td className="px-4 py-3 font-medium">{item.sku}</td>
                <td className="px-4 py-3">
                  {item.name}
                  <div className="text-xs text-muted-foreground">
                    {item.teamName ?? '—'} / {item.seasonName ?? '—'}
                  </div>
                  {(item.description || item.seoTitle) && (
                    <div className="mt-1 text-xs text-muted-foreground">
                      {item.seoTitle ?? item.description}
                    </div>
                  )}
                </td>
                <td className="px-4 py-3">{item.size}</td>
                <td className="px-4 py-3">{item.matchHint}</td>
                <td className="px-4 py-3">
                  {item.status}
                  {item.appliedProductId && (
                    <div>
                      <Link
                        className="text-xs text-primary hover:underline"
                        to={`/products/${item.appliedProductId}`}
                      >
                        Open product
                      </Link>
                    </div>
                  )}
                </td>
                <td className="px-4 py-3">
                  {canReview && item.status === 'Pending' && (
                    <div className="flex flex-col gap-2">
                      <div className="flex gap-2">
                        <Button
                          size="sm"
                          disabled={item.matchHint === 'Ambiguous'}
                          onClick={() => {
                            setError(null);
                            approve.mutate(item.id);
                          }}
                        >
                          Approve
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            setError(null);
                            reject.mutate(item.id);
                          }}
                        >
                          Reject
                        </Button>
                      </div>
                      {canAiWrite && (
                        <div className="flex flex-wrap gap-1">
                          {importAiKinds.map((kind) => (
                            <Button
                              key={kind.kind}
                              size="sm"
                              variant="ghost"
                              disabled={generateMutation.isPending}
                              onClick={() => {
                                setError(null);
                                generateMutation.mutate({ kind: kind.kind, targetId: item.id });
                              }}
                            >
                              AI {kind.label}
                            </Button>
                          ))}
                        </div>
                      )}
                      {canAiRead &&
                        (generationsQuery.data ?? [])
                          .filter((generation) => generation.targetId === item.id)
                          .slice(0, 3)
                          .map((generation) => (
                            <div key={generation.id} className="text-xs text-muted-foreground">
                              {generation.kind}: {generation.status}
                              {generation.outputText ? ` — ${generation.outputText}` : ''}
                              {canAiWrite && generation.status === 'Succeeded' && (
                                <Button
                                  size="sm"
                                  variant="outline"
                                  className="ml-2"
                                  onClick={() => {
                                    setError(null);
                                    approveGenerationMutation.mutate(generation.id);
                                  }}
                                >
                                  Apply
                                </Button>
                              )}
                            </div>
                          ))}
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </div>
  );
}
