import { createBrowserRouter } from 'react-router-dom';
import { RequireAuth, RequirePermission } from '@/auth/guards';
import { AppShell } from '@/components/app-shell';
import { DashboardPage } from '@/pages/dashboard';
import { LoginPage } from '@/pages/login';
import { ProductDetailPage } from '@/pages/product-detail';
import { ProductsListPage } from '@/pages/products-list';
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
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
]);
