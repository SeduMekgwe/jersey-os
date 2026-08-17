import { createBrowserRouter } from 'react-router-dom';
import { RequireAuth, RequirePermission } from '@/auth/guards';
import { AppShell } from '@/components/app-shell';
import { AdminOrganizationsPage } from '@/pages/admin-organizations';
import { AuditPage } from '@/pages/audit';
import { CollectionsPage } from '@/pages/collections';
import { DashboardPage } from '@/pages/dashboard';
import { ImportBatchDetailPage } from '@/pages/import-batch-detail';
import { ImportBatchesPage } from '@/pages/import-batches';
import { IntegrationsPage } from '@/pages/integrations';
import { InvitePage } from '@/pages/invite';
import { LoginPage } from '@/pages/login';
import { MembersPage } from '@/pages/members';
import { NotificationsPage } from '@/pages/notifications';
import { ProductDetailPage } from '@/pages/product-detail';
import { ProductsListPage } from '@/pages/products-list';
import { PricingPage } from '@/pages/pricing';
import { PublishingPage } from '@/pages/publishing';
import { NotFoundPage, UnauthorizedPage } from '@/pages/status-pages';
import { SystemHealthPage } from '@/pages/system-health';
import { TaxonomyPage } from '@/pages/taxonomy';

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/invite/:token', element: <InvitePage /> },
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
              { path: 'collections', element: <CollectionsPage /> },
            ],
          },
          {
            element: <RequirePermission permission="pricing.read" />,
            children: [{ path: 'pricing', element: <PricingPage /> }],
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
          {
            element: <RequirePermission permission="audit.read" />,
            children: [{ path: 'audit', element: <AuditPage /> }],
          },
          {
            element: <RequirePermission permission="notifications.read" />,
            children: [{ path: 'notifications', element: <NotificationsPage /> }],
          },
          {
            element: <RequirePermission permission="integrations.manage" />,
            children: [{ path: 'integrations', element: <IntegrationsPage /> }],
          },
          {
            element: <RequirePermission permission="org.members.manage" />,
            children: [{ path: 'members', element: <MembersPage /> }],
          },
          {
            element: <RequirePermission permission="platform.admin" />,
            children: [{ path: 'admin/organizations', element: <AdminOrganizationsPage /> }],
          },
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
]);
