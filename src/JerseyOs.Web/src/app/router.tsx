import { createBrowserRouter } from 'react-router-dom';
import { RequireAuth, RequirePermission } from '@/auth/guards';
import { AppShell } from '@/components/app-shell';
import { DashboardPage } from '@/pages/dashboard';
import { ImportBatchDetailPage } from '@/pages/import-batch-detail';
import { ImportBatchesPage } from '@/pages/import-batches';
import { LoginPage } from '@/pages/login';
import { ProductDetailPage } from '@/pages/product-detail';
import { ProductsListPage } from '@/pages/products-list';
import { PublishingPage } from '@/pages/publishing';
import { NotFoundPage, UnauthorizedPage } from '@/pages/status-pages';
import { SystemHealthPage } from '@/pages/system-health';
import { TaxonomyPage } from '@/pages/taxonomy';

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/unauthorized', element: <UnauthorizedPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <DashboardPage /> },
          {
            element: <RequirePermission permission="system.health.read" />,
            children: [{ path: 'system-health', element: <SystemHealthPage /> }],
          },
          {
            element: <RequirePermission permission="catalog.read" />,
            children: [
              { path: 'products', element: <ProductsListPage /> },
              { path: 'products/:productId', element: <ProductDetailPage /> },
              { path: 'taxonomy', element: <TaxonomyPage /> },
            ],
          },
          {
            element: <RequirePermission permission="import.read" />,
            children: [
              { path: 'import', element: <ImportBatchesPage /> },
              { path: 'import/:batchId', element: <ImportBatchDetailPage /> },
            ],
          },
          {
            element: <RequirePermission permission="publishing.read" />,
            children: [{ path: 'publishing', element: <PublishingPage /> }],
          },
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
]);
