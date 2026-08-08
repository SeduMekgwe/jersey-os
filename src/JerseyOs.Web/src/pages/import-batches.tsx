import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '@/auth/auth-context';
import { Button, Card, Input } from '@/components/ui';
import { apiRequest } from '@/lib/api';
import type {
  CreateSupplierDto,
  ImportBatchSummaryDto,
  SupplierDto,
} from '@/types/api';

export function ImportBatchesPage() {
  const { user } = useAuth();
  const canUpload = user?.permissions.includes('import.upload') ?? false;
  const queryClient = useQueryClient();
  const [supplierId, setSupplierId] = useState('');
  const [supplierName, setSupplierName] = useState('');
  const [supplierCode, setSupplierCode] = useState('');
  const [feedKind, setFeedKind] = useState<'upload' | 'http'>('upload');
  const [feedFormat, setFeedFormat] = useState<'csv' | 'json'>('csv');
  const [feedUrl, setFeedUrl] = useState('');
  const [feedBearerToken, setFeedBearerToken] = useState('');
  const [syncCron, setSyncCron] = useState('');
  const [error, setError] = useState<string | null>(null);

  const suppliersQuery = useQuery({
    queryKey: ['import', 'suppliers'],
    queryFn: () => apiRequest<SupplierDto[]>('/import/suppliers'),
  });
  const batchesQuery = useQuery({
    queryKey: ['import', 'batches'],
    queryFn: () => apiRequest<ImportBatchSummaryDto[]>('/import/batches'),
    refetchInterval: 5_000,
  });

  const createSupplier = useMutation({
    mutationFn: (body: CreateSupplierDto) =>
      apiRequest<SupplierDto>('/import/suppliers', { method: 'POST', body: JSON.stringify(body) }),
    onSuccess: (supplier) => {
      void queryClient.invalidateQueries({ queryKey: ['import', 'suppliers'] });
      setSupplierId(supplier.id);
      setSupplierName('');
      setSupplierCode('');
      setFeedUrl('');
      setFeedBearerToken('');
      setSyncCron('');
      setFeedKind('upload');
      setFeedFormat('csv');
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });

  const syncSupplier = useMutation({
    mutationFn: (id: string) =>
      apiRequest<SupplierDto>(`/import/suppliers/${id}/sync`, { method: 'POST', body: '{}' }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['import', 'suppliers'] });
      void queryClient.invalidateQueries({ queryKey: ['import', 'batches'] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });

  const upload = useMutation({
    mutationFn: async (file: File) => {
      if (!supplierId) throw new Error('Select a supplier first.');
      const form = new FormData();
      form.append('supplierId', supplierId);
      form.append('file', file);
      return apiRequest<ImportBatchSummaryDto>('/import/batches', { method: 'POST', body: form });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['import', 'batches'] });
    },
    onError: (err: Error) => {
      setError(err.message);
    },
  });

  const selected = (suppliersQuery.data ?? []).find((s) => s.id === supplierId);

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">Supplier import</h1>
        <p className="mt-2 text-muted-foreground">
          Upload CSV/JSON or sync HTTP supplier feeds into the review queue, then approve into draft catalog.
        </p>
      </div>
      {error && (
        <Card className="border-destructive/50 p-4" role="alert">
          {error}
        </Card>
      )}
      {canUpload && (
        <Card className="space-y-4 p-5">
          <h2 className="text-xl font-medium">Suppliers</h2>
          <label className="grid gap-1 text-sm">
            Active supplier
            <select
              className="h-10 rounded-md border border-input bg-background px-3 text-sm"
              value={supplierId}
              onChange={(e) => {
                setSupplierId(e.target.value);
              }}
            >
              <option value="">Select supplier</option>
              {(suppliersQuery.data ?? []).map((supplier) => (
                <option key={supplier.id} value={supplier.id}>
                  {supplier.name} ({supplier.feedKind}/{supplier.feedFormat})
                </option>
              ))}
            </select>
          </label>
          {selected && (
            <div className="space-y-2 rounded-md border border-border p-3 text-sm">
              <p>
                {selected.feedKind === 'http'
                  ? `HTTP feed: ${selected.feedUrl ?? '—'}`
                  : 'Upload feed (manual file)'}
              </p>
              {selected.lastSyncStatus && (
                <p className="text-muted-foreground">
                  Last sync: {selected.lastSyncStatus}
                  {selected.lastSyncAtUtc
                    ? ` · ${new Date(selected.lastSyncAtUtc).toLocaleString()}`
                    : ''}
                  {selected.lastSyncError ? ` · ${selected.lastSyncError}` : ''}
                </p>
              )}
              {selected.feedKind === 'http' && (
                <Button
                  variant="outline"
                  size="sm"
                  disabled={syncSupplier.isPending}
                  onClick={() => {
                    setError(null);
                    syncSupplier.mutate(selected.id);
                  }}
                >
                  Sync now
                </Button>
              )}
            </div>
          )}
          <div className="grid gap-3 md:grid-cols-3">
            <Input
              placeholder="New supplier name"
              value={supplierName}
              onChange={(e) => {
                setSupplierName(e.target.value);
              }}
            />
            <Input
              placeholder="code"
              value={supplierCode}
              onChange={(e) => {
                setSupplierCode(e.target.value);
              }}
            />
            <select
              className="h-10 rounded-md border border-input bg-background px-3 text-sm"
              value={feedKind}
              onChange={(e) => {
                setFeedKind(e.target.value as 'upload' | 'http');
              }}
            >
              <option value="upload">Upload</option>
              <option value="http">HTTP</option>
            </select>
          </div>
          <div className="grid gap-3 md:grid-cols-3">
            <select
              className="h-10 rounded-md border border-input bg-background px-3 text-sm"
              value={feedFormat}
              onChange={(e) => {
                setFeedFormat(e.target.value as 'csv' | 'json');
              }}
            >
              <option value="csv">CSV</option>
              <option value="json">JSON</option>
            </select>
            {feedKind === 'http' && (
              <>
                <Input
                  placeholder="https://supplier.example/feed.json"
                  value={feedUrl}
                  onChange={(e) => {
                    setFeedUrl(e.target.value);
                  }}
                />
                <Input
                  placeholder="Bearer token (optional)"
                  value={feedBearerToken}
                  onChange={(e) => {
                    setFeedBearerToken(e.target.value);
                  }}
                />
              </>
            )}
          </div>
          {feedKind === 'http' && (
            <Input
              placeholder="Hangfire cron (optional, e.g. 0 */6 * * *)"
              value={syncCron}
              onChange={(e) => {
                setSyncCron(e.target.value);
              }}
            />
          )}
          <Button
            variant="outline"
            onClick={() => {
              setError(null);
              createSupplier.mutate({
                name: supplierName,
                code: supplierCode,
                feedKind,
                feedFormat,
                feedUrl: feedKind === 'http' ? feedUrl : null,
                feedBearerToken: feedKind === 'http' && feedBearerToken ? feedBearerToken : null,
                syncCron: feedKind === 'http' && syncCron ? syncCron : null,
              });
            }}
          >
            Create supplier
          </Button>
          {(!selected || selected.feedKind === 'upload') && (
            <Input
              type="file"
              accept=".csv,.json,text/csv,application/json"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) {
                  setError(null);
                  upload.mutate(file);
                }
              }}
            />
          )}
        </Card>
      )}
      <div className="grid gap-3">
        {(batchesQuery.data ?? []).map((batch) => (
          <Card key={batch.id} className="flex items-center justify-between gap-4 p-5">
            <div>
              <Link to={`/import/${batch.id}`} className="text-lg font-medium hover:underline">
                {batch.fileName}
              </Link>
              <p className="text-sm text-muted-foreground">
                {batch.supplierName} · {batch.status} · {batch.pendingCount}/{batch.itemCount} pending
              </p>
            </div>
            <Button asChild variant="outline" size="sm">
              <Link to={`/import/${batch.id}`}>Review</Link>
            </Button>
          </Card>
        ))}
      </div>
    </div>
  );
}
