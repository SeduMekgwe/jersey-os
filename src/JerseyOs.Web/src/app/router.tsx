import { createBrowserRouter } from 'react-router-dom';
import { RequireAuth, RequirePermission } from '@/auth/guards';
import { AppShell } from '@/components/app-shell';
import { DashboardPage } from '@/pages/dashboard';
import { LoginPage } from '@/pages/login';
import { NotFoundPage, UnauthorizedPage } from '@/pages/status-pages';
import { SystemHealthPage } from '@/pages/system-health';

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
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
]);
